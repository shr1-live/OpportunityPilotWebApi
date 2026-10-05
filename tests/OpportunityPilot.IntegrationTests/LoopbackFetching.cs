using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OpportunityPilot.Infrastructure.Research;

namespace OpportunityPilot.IntegrationTests;

/// <summary>
/// Test-only: loopback and any port. The real policy is <see cref="StrictFetchAddressPolicy"/>; this class exists only in
/// the test project and no configuration can enable it, so production can never reach loopback.
/// </summary>
internal sealed class LoopbackForTestsPolicy : IFetchAddressPolicy
{
    public bool IsAllowed(IPAddress address) => IPAddress.IsLoopback(address) || AddressClassifier.IsPublic(address);
    public bool IsPortAllowed(int port) => true;
}

/// <summary>Production address rules, but any port, so the only thing standing between the fetcher and the server is the address check.</summary>
internal sealed class StrictAddressesAnyPortPolicy : IFetchAddressPolicy
{
    public bool IsAllowed(IPAddress address) => AddressClassifier.IsPublic(address);
    public bool IsPortAllowed(int port) => true;
}

/// <summary>A tiny HTTP/1.1 server on loopback answering from a script; records every request path (with query).</summary>
internal sealed class TinyHttpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<string, (int Status, string ContentType, string Body)> _respond;
    private int _connections;

    public TinyHttpServer(Func<string, (string ContentType, string Body)> respond) : this(path =>
    {
        var (type, body) = respond(path);
        return (200, type, body);
    })
    {
    }

    public TinyHttpServer(Func<string, (int Status, string ContentType, string Body)> respond)
    {
        _respond = respond;
        _listener.Start();
        _ = Task.Run(AcceptLoop);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public string BaseUrl => $"http://127.0.0.1:{Port}";
    public int Connections => Volatile.Read(ref _connections);
    public ConcurrentQueue<string> Paths { get; } = new();

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_stop.Token); }
            catch (Exception) { return; }
            Interlocked.Increment(ref _connections);
            _ = Task.Run(async () =>
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var buffer = new byte[8192];
                    var request = new StringBuilder();
                    while (!request.ToString().Contains("\r\n\r\n"))
                    {
                        var read = await stream.ReadAsync(buffer);
                        if (read == 0) return;
                        request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    }
                    var path = request.ToString().Split(' ')[1];
                    Paths.Enqueue(path);
                    var (status, type, body) = _respond(path);
                    var bytes = Encoding.UTF8.GetBytes(body);
                    var head = $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\nContent-Type: {type}; charset=utf-8\r\n" +
                               $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
                    await stream.WriteAsync(bytes);
                }
            });
        }
    }

    public ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        return ValueTask.CompletedTask;
    }
}
