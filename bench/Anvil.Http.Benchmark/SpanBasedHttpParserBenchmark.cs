using System.Text;
using BenchmarkDotNet.Attributes;
using Anvil.Http.Parsing;

namespace Anvil.Http.Benchmark;

[MemoryDiagnoser]
[ShortRunJob]
public class SpanBasedHttpParserBenchmark
{
    private byte[] _simpleGetRequest = null!;
    private byte[] _postRequestWithBody = null!;
    private byte[] _postRequestWithLargeBody = null!;
    private byte[] _requestWithManyHeaders = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Simple GET request
        _simpleGetRequest = Encoding.UTF8.GetBytes(
            "GET /api/users HTTP/1.1\r\n" +
            "Host: example.com\r\n" +
            "User-Agent: TestClient/1.0\r\n" +
            "\r\n");

        // POST request with small body
        var postBody = "{\"name\":\"John\",\"age\":30}";
        _postRequestWithBody = Encoding.UTF8.GetBytes(
            "POST /api/users HTTP/1.1\r\n" +
            "Host: example.com\r\n" +
            "Content-Type: application/json\r\n" +
            $"Content-Length: {postBody.Length}\r\n" +
            "\r\n" + postBody);

        // POST request with large body (10KB)
        var largeBody = new string('x', 10240);
        _postRequestWithLargeBody = Encoding.UTF8.GetBytes(
            "POST /api/upload HTTP/1.1\r\n" +
            "Host: example.com\r\n" +
            "Content-Type: application/octet-stream\r\n" +
            $"Content-Length: {largeBody.Length}\r\n" +
            "\r\n" + largeBody);

        // Request with many headers (20 headers)
        var headerBuilder = new StringBuilder();
        headerBuilder.Append("GET /api/data HTTP/1.1\r\n");
        for (int i = 0; i < 20; i++)
        {
            headerBuilder.Append($"X-Header-{i}: value-{i}\r\n");
        }
        headerBuilder.Append("Host: example.com\r\n");
        headerBuilder.Append("\r\n");
        _requestWithManyHeaders = Encoding.UTF8.GetBytes(headerBuilder.ToString());
    }

    [Benchmark(Description = "Parse simple GET request")]
    public void ParseSimpleGetRequest()
    {
        var request = SpanBasedHttpParser.Parse(_simpleGetRequest.AsSpan());
        _ = request.RequestLine.MethodString();
        _ = request.RequestLine.PathString();
    }

    [Benchmark(Description = "Parse POST request with body")]
    public void ParsePostRequestWithBody()
    {
        var request = SpanBasedHttpParser.Parse(_postRequestWithBody.AsSpan());
        _ = request.RequestLine.MethodString();
        _ = Encoding.UTF8.GetString(request.Body);
    }

    [Benchmark(Description = "Parse POST request with large body (10KB)")]
    public void ParsePostRequestWithLargeBody()
    {
        var request = SpanBasedHttpParser.Parse(_postRequestWithLargeBody.AsSpan());
        _ = request.RequestLine.MethodString();
        _ = request.Body.Length;
    }

    [Benchmark(Description = "Parse request with many headers (20 headers)")]
    public void ParseRequestWithManyHeaders()
    {
        var request = SpanBasedHttpParser.Parse(_requestWithManyHeaders.AsSpan());
        _ = request.Headers.Length;
        foreach (var header in request.Headers)
        {
            _ = Encoding.UTF8.GetString(header.Name);
            _ = Encoding.UTF8.GetString(header.Value);
        }
    }

    [Benchmark(Description = "Parse and access all components")]
    public void ParseCompleteRequest()
    {
        var request = SpanBasedHttpParser.Parse(_postRequestWithBody.AsSpan());
        
        // Access request line
        _ = request.RequestLine.MethodString();
        _ = request.RequestLine.PathString();
        _ = request.RequestLine.VersionString();
        
        // Access headers
        foreach (var header in request.Headers)
        {
            _ = Encoding.UTF8.GetString(header.Name);
            _ = Encoding.UTF8.GetString(header.Value);
        }
        
        // Access body
        _ = Encoding.UTF8.GetString(request.Body);
    }

    [Benchmark(Description = "Parse request line only")]
    public void ParseRequestLineOnly()
    {
        var request = SpanBasedHttpParser.Parse(_simpleGetRequest.AsSpan());
        _ = request.RequestLine.Method;
        _ = request.RequestLine.Path;
        _ = request.RequestLine.Version;
    }

    [Benchmark(Description = "Iterate through headers")]
    public void IterateThroughHeaders()
    {
        var request = SpanBasedHttpParser.Parse(_requestWithManyHeaders.AsSpan());
        foreach (var header in request.Headers)
        {
            _ = header.Name;
            _ = header.Value;
        }
    }
}
