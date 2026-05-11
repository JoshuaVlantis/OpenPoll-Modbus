using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OpenPoll.Models;

namespace OpenPoll.Services;

/// <summary>
/// Minimal embedded HTTP server that exposes the live state of every open poll.
/// External scripts (Python, Node, curl) can subscribe to register values and write back —
/// modern replacement for Modbus Poll's VBA / OLE automation.
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
            var path = req.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            var method = req.HttpMethod;

            // Routes
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
            if ((path == "" || path == "/") && method == "GET")
            {
                var html = "<!doctype html><html><body style='font-family:monospace;background:#0E1117;color:#E5E8EE'><h1>OpenPoll API</h1>" +
                           "<p>GET /api/polls — list polls</p>" +
                           "<p>GET /api/polls/{name}/values — current values</p></body></html>";
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

    public void Dispose() => _ = StopAsync();
}
