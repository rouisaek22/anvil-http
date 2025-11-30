using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Anvil.Http.Parsing;

// Simple TCP server that demonstrates using HttpRequestAccumulator + SpanBasedHttpParser
// Run: dotnet run --project examples/PrintRequestToConsole

const int Port = 3333;
var listener = new TcpListener(IPAddress.Loopback, Port);
listener.Start();
Console.WriteLine($"Listening on 127.0.0.1:{Port}. Connect with: 'curl -v http://127.0.0.1:{Port}/'\nCtrl+C to stop.");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("Stopping...");
};

var pool = ArrayPool<byte>.Shared;

while (!cts.IsCancellationRequested)
{
    try
    {
        var client = await listener.AcceptTcpClientAsync(cts.Token).ConfigureAwait(false);
        _ = HandleClientAsync(client, pool); // fire-and-forget
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

listener.Stop();

async Task HandleClientAsync(TcpClient client, ArrayPool<byte> _pool)
{
    using (client)
    {
        using var stream = client.GetStream();
        using var accumulator = new HttpRequestAccumulator();
        var buffer = _pool.Rent(4096);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) != 0)
            {
                var result = accumulator.Accumulate(buffer.AsSpan(0, read));
                if (result == AccumulatorResult.Complete)
                {
                    var completeRequest = accumulator.GetAccumulatedData().ToArray();
                    Console.WriteLine($"Complete request: {completeRequest.Length} bytes from {client.Client.RemoteEndPoint}");

                    var parsed = SpanBasedHttpParser.Parse(completeRequest);
                    var request = MapSpanToRequest(parsed);

                    await HandleRouteAsync(request, stream);

                    accumulator.Reset();
                    break; // This simple example handles only a single request per connection
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex}");
        }
        finally
        {
            _pool.Return(buffer);
        }
    }
}

Request MapSpanToRequest(SpanBasedHttpParser.ParsedHttpRequest parsed)
{
    var req = new Request
    {
        Method = parsed.RequestLine.MethodString(),
        Path = parsed.RequestLine.PathString(),
        Version = parsed.RequestLine.VersionString(),
        Body = parsed.Body.ToArray()
    };

    foreach (var h in parsed.Headers)
    {
        var name = Encoding.UTF8.GetString(h.Name);
        var value = Encoding.UTF8.GetString(h.Value);
        req.Headers[name] = value;
    }

    return req;
}

async Task HandleRouteAsync(Request request, NetworkStream stream)
{
    Console.WriteLine("=== Request Line ===");
    Console.WriteLine($"{request.Method} {request.Path} {request.Version}");
    Console.WriteLine("\n=== Headers ===");
    foreach (var kv in request.Headers)
    {
        Console.WriteLine($"{kv.Key}: {kv.Value}");
    }
    Console.WriteLine("\n=== Body ===");
    Console.WriteLine(Encoding.UTF8.GetString(request.Body));

    // Simple response
    var body = Encoding.UTF8.GetBytes($"Hello from Anvil.Http example! You requested {request.Path}");
    var resp = new StringBuilder();
    resp.AppendLine("HTTP/1.1 200 OK");
    resp.AppendLine($"Content-Length: {body.Length}");
    resp.AppendLine("Content-Type: text/plain; charset=utf-8");
    resp.AppendLine();

    var headerBytes = Encoding.UTF8.GetBytes(resp.ToString());
    await stream.WriteAsync(headerBytes.AsMemory(0, headerBytes.Length));
    await stream.WriteAsync(body.AsMemory(0, body.Length));
}

class Request
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public byte[] Body { get; set; } = Array.Empty<byte>();
}
