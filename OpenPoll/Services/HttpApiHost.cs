using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OpenPoll.Models;

namespace OpenPoll.Services;

/// <summary>
/// Minimal embedded HTTP server that exposes the live state of every open poll.
/// External scripts (Python, Node, curl) can subscribe to register values and write back.
/// </summary>
public sealed class HttpApiHost : IDisposable
{
    private readonly Workspace _workspace;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _task;

    public bool IsRunning => _task is { IsCompleted: false };
    public int Port { get; private set; }
    public string BaseUrl => $"http://localhost:{Port}";

    /// <summary>
    /// Optional bearer token. When non-empty, every request to `/api/*` must either send
    /// `Authorization: Bearer &lt;token&gt;` or pass `?token=&lt;token&gt;` in the query string.
    /// </summary>
    public string? AuthToken { get; set; }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public HttpApiHost(Workspace workspace) => _workspace = workspace;

    public Task StartAsync(int port)
    {
        if (IsRunning) return Task.CompletedTask;
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _task = Task.Run(() => LoopAsync(token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        if (_task is not null) { try { await _task; } catch { } }
        _listener?.Close();
        _listener = null;
        _cts = null;
        _task = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        try
        {
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            var path = req.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            var method = req.HttpMethod;

            if (method == "OPTIONS") { res.StatusCode = 204; return; }

            // Auth gate — only enforced for /api/* paths so the root HTML cheat sheet
            // stays reachable without a token (it's just docs).
            if (!string.IsNullOrEmpty(AuthToken) && path.StartsWith("/api/", StringComparison.Ordinal)
                && !IsAuthorized(req, AuthToken))
            {
                res.StatusCode = 401;
                res.AddHeader("WWW-Authenticate", "Bearer realm=\"openpoll\"");
                await WriteJsonAsync(res, new { ok = false, error = "Unauthorized" });
                return;
            }

            // Routes
            if (path == "/api/ws" && req.IsWebSocketRequest)
            {
                await HandleWebSocketAsync(ctx);
                return;
            }
            if (path == "/api/polls" && method == "GET")
            {
                await WriteJsonAsync(res, _workspace.Documents.Select(SummarizeDoc).ToArray());
                return;
            }
            if (path.StartsWith("/api/polls/") && path.EndsWith("/values") && method == "GET")
            {
                var name = Uri.UnescapeDataString(path.Substring("/api/polls/".Length, path.Length - "/api/polls/".Length - "/values".Length));
                var doc = _workspace.Documents.FirstOrDefault(d => d.Definition.Name == name);
                if (doc is null) { res.StatusCode = 404; return; }
                await WriteJsonAsync(res, doc.Rows.Select(r => new
                {
                    address = r.Address,
                    displayAddress = r.DisplayAddress,
                    value = r.Value,
                    dataType = r.DataType.ToString(),
                    rawWords = r.RawWords,
                }).ToArray());
                return;
            }
            if (path.StartsWith("/api/polls/") && path.EndsWith("/write") && method == "POST")
            {
                var name = Uri.UnescapeDataString(path.Substring("/api/polls/".Length, path.Length - "/api/polls/".Length - "/write".Length));
                var doc = _workspace.Documents.FirstOrDefault(d => d.Definition.Name == name);
                if (doc is null) { res.StatusCode = 404; return; }
                using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                var body = await reader.ReadToEndAsync();
                WriteRequest? wr;
                try { wr = JsonSerializer.Deserialize<WriteRequest>(body, Json); }
                catch (Exception ex) { res.StatusCode = 400; await WriteJsonAsync(res, new { ok = false, error = $"Invalid JSON: {ex.Message}" }); return; }
                if (wr is null) { res.StatusCode = 400; await WriteJsonAsync(res, new { ok = false, error = "Missing body" }); return; }

                var session = doc.Session;
                var fnRaw = wr.Function?.ToLowerInvariant();
                var ok = false; string? error = null;
                if (fnRaw is "5" or "05" or "coil")
                {
                    var r = session.WriteSingleCoil(wr.Address, wr.Bool);
                    ok = r.Success; error = r.Error;
                }
                else if (fnRaw is "6" or "06" or "register")
                {
                    var r = session.WriteSingleRegister(wr.Address, wr.Value);
                    ok = r.Success; error = r.Error;
                }
                else if (fnRaw is "15" or "coils")
                {
                    var r = session.WriteMultipleCoils(wr.Address, wr.Bools ?? Array.Empty<bool>());
                    ok = r.Success; error = r.Error;
                }
                else if (fnRaw is "16" or "registers")
                {
                    var r = session.WriteMultipleRegisters(wr.Address, wr.Values ?? Array.Empty<int>());
                    ok = r.Success; error = r.Error;
                }
                else
                {
                    res.StatusCode = 400;
                    await WriteJsonAsync(res, new { ok = false, error = $"Unknown function: {wr.Function}" });
                    return;
                }
                await WriteJsonAsync(res, new { ok, function = fnRaw, address = wr.Address, error });
                return;
            }
            if ((path == "" || path == "/") && method == "GET")
            {
                var html = "<!doctype html><html><body style='font-family:monospace;background:#0E1117;color:#E5E8EE'><h1>OpenPoll API</h1>" +
                           "<p>GET  /api/polls — list polls</p>" +
                           "<p>GET  /api/polls/{name}/values — current values</p>" +
                           "<p>POST /api/polls/{name}/write {function:'06', address:0, value:42}</p></body></html>";
                res.ContentType = "text/html; charset=utf-8";
                var bytes = Encoding.UTF8.GetBytes(html);
                await res.OutputStream.WriteAsync(bytes);
                return;
            }
            res.StatusCode = 404;
        }
        catch (Exception ex)
        {
            try { res.StatusCode = 500; await WriteJsonAsync(res, new { error = ex.Message }); } catch { }
        }
        finally { try { res.Close(); } catch { } }
    }

    private static object SummarizeDoc(PollDocument d) => new
    {
        name = d.Definition.Name,
        title = d.Title,
        status = d.Status.ToString(),
        statusMessage = d.StatusMessage,
        function = d.Definition.Function.ToString(),
        address = d.Definition.Address,
        amount = d.Definition.Amount,
        pollCount = d.PollCount,
        connectionMode = d.Definition.ConnectionMode.ToString(),
        ipAddress = d.Definition.IpAddress,
        port = d.Definition.ServerPort,
    };

    private static async Task WriteJsonAsync(HttpListenerResponse res, object payload)
    {
        res.ContentType = "application/json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        await res.OutputStream.WriteAsync(bytes);
    }

    /// <summary>
    /// Check a request against a configured bearer token. Accepts either an
    /// `Authorization: Bearer &lt;token&gt;` header or a `?token=&lt;token&gt;` query parameter
    /// (the latter for quick curl tests; production callers should prefer the header).
    /// </summary>
    private static bool IsAuthorized(HttpListenerRequest req, string token)
    {
        var header = req.Headers["Authorization"];
        if (!string.IsNullOrEmpty(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var provided = header.AsSpan(7).Trim().ToString();
            return ConstantTimeEquals(provided, token);
        }
        var query = req.QueryString["token"];
        if (!string.IsNullOrEmpty(query))
        {
            return ConstantTimeEquals(query, token);
        }
        return false;
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    public void Dispose() => _ = StopAsync();

    /// <summary>
    /// WebSocket endpoint at /api/ws. Subscribes to every open poll document and pushes a JSON
    /// snapshot of its current row values whenever the poll completes a tick. Clients receive
    /// roughly real-time updates without polling REST.
    /// </summary>
    private async Task HandleWebSocketAsync(HttpListenerContext ctx)
    {
        WebSocketContext wsCtx;
        try { wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null); }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            try { ctx.Response.Close(); } catch { }
            FileLogger.Error("WebSocket accept failed: " + ex.Message);
            return;
        }

        var ws = wsCtx.WebSocket;
        var outbox = System.Threading.Channels.Channel.CreateUnbounded<string>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });

        // Subscribe to every existing document and any added/removed later. Filter on PollCount
        // change so we emit at the natural poll cadence rather than per cell.
        var attached = new Dictionary<PollDocument, PropertyChangedEventHandler>();
        void Attach(PollDocument d)
        {
            void Handler(object? _, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(PollDocument.PollCount))
                    outbox.Writer.TryWrite(BuildSnapshot(d));
            }
            d.PropertyChanged += Handler;
            attached[d] = Handler;
            outbox.Writer.TryWrite(BuildSnapshot(d));
        }
        foreach (var d in _workspace.Documents) Attach(d);

        try
        {
            await foreach (var msg in outbox.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if (ws.State != WebSocketState.Open) break;
                var bytes = Encoding.UTF8.GetBytes(msg);
                await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex) { FileLogger.Error("WebSocket send loop ended: " + ex.Message); }
        finally
        {
            foreach (var (d, h) in attached) d.PropertyChanged -= h;
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
            try { ws.Dispose(); } catch { }
        }
    }

    private static string BuildSnapshot(PollDocument d)
    {
        return JsonSerializer.Serialize(new
        {
            type = "snapshot",
            name = d.Definition.Name,
            pollCount = d.PollCount,
            status = d.Status.ToString(),
            statusMessage = d.StatusMessage,
            rows = d.Rows.Select(r => new
            {
                address = r.Address,
                displayAddress = r.DisplayAddress,
                value = r.Value,
                foregroundHex = r.ForegroundHex,
            }).ToArray(),
        }, Json);
    }

    private sealed class WriteRequest
    {
        public string? Function { get; set; }
        public int Address { get; set; }
        public int Value { get; set; }
        public bool Bool { get; set; }
        public int[]? Values { get; set; }
        public bool[]? Bools { get; set; }
    }
}
