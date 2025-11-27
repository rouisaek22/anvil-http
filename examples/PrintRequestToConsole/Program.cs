using System.Text;
using Anvil.Http.Parsing;

var bodyContent = "Rouis Abdelkader";
var bodyText = $@"{{""name"":""{bodyContent}""}}";
var bodyBytesCount = Encoding.UTF8.GetByteCount(bodyText);

var rawRequest =
    $"POST /api/users HTTP/1.1\r\n" +
    $"host: localhost:3333\r\n" +
    $"Content-Type: application/json\r\n" +
    $"Content-Length: {bodyBytesCount}\r\n" +
    $"\r\n" + bodyText;

Span<byte> requestInBytes = Encoding.UTF8.GetBytes(rawRequest).AsSpan();

var request = SpanBasedHttpParser.Parse(requestInBytes);

Console.WriteLine("=== Request Line ===");
Console.WriteLine($"Method: {request.RequestLine.MethodString()}");
Console.WriteLine($"Path: {request.RequestLine.PathString()}");
Console.WriteLine($"Version: {request.RequestLine.VersionString()}");

Console.WriteLine("\n=== Headers ===");
foreach (var header in request.Headers)
{
        Console.WriteLine($"{Encoding.UTF8.GetString(header.Name)}: {Encoding.UTF8.GetString(header.Value)}");
}

Console.WriteLine("\n=== Body ===");
Console.WriteLine(Encoding.UTF8.GetString(request.Body));
