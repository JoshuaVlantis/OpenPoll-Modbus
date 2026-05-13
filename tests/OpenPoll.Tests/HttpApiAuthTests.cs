using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using FluentAssertions;
using OpenPoll.Models;
using OpenPoll.Services;

namespace OpenPoll.Tests;

/// <summary>
/// Verifies the bearer-token gate on the embedded HTTP API.
/// No token configured = backwards-compatible (no auth check).
/// Token configured  = /api/* requires `Authorization: Bearer <t>` or `?token=<t>`.
/// </summary>
public sealed class HttpApiAuthTests
{
    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static Workspace SeededWorkspace()
    {
        var ws = new Workspace();
        ws.AddNew(new PollDefinition { Name = "demo" });
        return ws;
    }

    [Fact]
    public async Task NoToken_AllowsAllRequests()
    {
        var port = FreePort();
        using var host = new HttpApiHost(SeededWorkspace());
        await host.StartAsync(port);
        try
        {
            using var http = new HttpClient();
            var res = await http.GetAsync($"http://localhost:{port}/api/polls");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task TokenSet_RejectsRequestWithoutAuth()
    {
        var port = FreePort();
        using var host = new HttpApiHost(SeededWorkspace()) { AuthToken = "s3cret" };
        await host.StartAsync(port);
        try
        {
            using var http = new HttpClient();
            var res = await http.GetAsync($"http://localhost:{port}/api/polls");
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            res.Headers.WwwAuthenticate.ToString().Should().Contain("Bearer");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task TokenSet_RejectsWrongAuth()
    {
        var port = FreePort();
        using var host = new HttpApiHost(SeededWorkspace()) { AuthToken = "s3cret" };
        await host.StartAsync(port);
        try
        {
            using var http = new HttpClient();
            var msg = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{port}/api/polls");
            msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "wrong");
            var res = await http.SendAsync(msg);
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task TokenSet_AcceptsCorrectBearerHeader()
    {
        var port = FreePort();
        using var host = new HttpApiHost(SeededWorkspace()) { AuthToken = "s3cret" };
        await host.StartAsync(port);
        try
        {
            using var http = new HttpClient();
            var msg = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:{port}/api/polls");
            msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "s3cret");
            var res = await http.SendAsync(msg);
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task TokenSet_AcceptsTokenInQueryString()
    {
        var port = FreePort();
        using var host = new HttpApiHost(SeededWorkspace()) { AuthToken = "s3cret" };
        await host.StartAsync(port);
        try
        {
            using var http = new HttpClient();
            var res = await http.GetAsync($"http://localhost:{port}/api/polls?token=s3cret");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task TokenSet_RootPathStillAccessibleWithoutAuth()
    {
        // The root cheat-sheet is documentation, not data — keep it open even with auth on.
        var port = FreePort();
        using var host = new HttpApiHost(SeededWorkspace()) { AuthToken = "s3cret" };
        await host.StartAsync(port);
        try
        {
            using var http = new HttpClient();
            var res = await http.GetAsync($"http://localhost:{port}/");
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally { await host.StopAsync(); }
    }
}
