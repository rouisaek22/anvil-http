using System.Text;
using Anvil.Http.Exceptions;

namespace Anvil.Http.Parsing;

/// <summary>
/// A high-performance HTTP request parser that uses Span&lt;T&gt; for zero-copy parsing of raw HTTP requests.
/// This parser efficiently parses HTTP request lines, headers, and body using stack-allocated spans and avoids
/// unnecessary string allocations for better memory efficiency.
/// </summary>
public static class SpanBasedHttpParser
{
    /// <summary>
    /// Represents an HTTP request line containing the method, path, and HTTP version.
    /// This is a ref struct to enable stack allocation and span usage without heap overhead.
    /// </summary>
    public readonly ref struct HttpRequestLine
    {
        /// <summary>The HTTP method (e.g., GET, POST, PUT) as a raw byte span.</summary>
        public readonly ReadOnlySpan<byte> Method;
        /// <summary>The request path/URI as a raw byte span.</summary>
        public readonly ReadOnlySpan<byte> Path;
        /// <summary>The HTTP version (e.g., HTTP/1.1) as a raw byte span.</summary>
        public readonly ReadOnlySpan<byte> Version;

        /// <summary>
        /// Initializes a new instance of the HttpRequestLine struct with method, path, and version.
        /// </summary>
        public HttpRequestLine(ReadOnlySpan<byte> method, ReadOnlySpan<byte> path, ReadOnlySpan<byte> version)
        {
            Method = method;
            Path = path;
            Version = version;
        }

        /// <summary>Converts the HTTP method bytes to a string using ASCII encoding.</summary>
        public string MethodString() => Encoding.ASCII.GetString(Method);
        /// <summary>Converts the request path bytes to a string using ASCII encoding.</summary>
        public string PathString() => Encoding.ASCII.GetString(Path);
        /// <summary>Converts the HTTP version bytes to a string using ASCII encoding.</summary>
        public string VersionString() => Encoding.ASCII.GetString(Version);
    }

    /// <summary>
    /// Represents a single HTTP header with name and value pairs.
    /// Values are stored as byte arrays (not spans) since headers are extracted from the parsed request.
    /// </summary>
    public readonly struct HttpHeader
    {
        /// <summary>The header name as a byte array (e.g., "Content-Type").</summary>
        public readonly byte[] NameBytes;
        /// <summary>The header value as a byte array (e.g., "application/json").</summary>
        public readonly byte[] ValueBytes;

        /// <summary>
        /// Initializes a new instance of the HttpHeader struct with name and value spans.
        /// The spans are converted to arrays for storage.
        /// </summary>
        public HttpHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            NameBytes = name.ToArray();
            ValueBytes = value.ToArray();
        }

        /// <summary>Gets the header name as a read-only span.</summary>
        public ReadOnlySpan<byte> Name => NameBytes;
        /// <summary>Gets the header value as a read-only span.</summary>
        public ReadOnlySpan<byte> Value => ValueBytes;
    }

    /// <summary>
    /// Represents a fully parsed HTTP request containing the request line, headers, and body.
    /// This is a ref struct to maintain zero-copy semantics with the input span.
    /// </summary>
    public readonly ref struct ParsedHttpRequest
    {
        /// <summary>The parsed HTTP request line (method, path, version).</summary>
        public readonly HttpRequestLine RequestLine;
        /// <summary>The collection of parsed HTTP headers.</summary>
        public readonly ReadOnlySpan<HttpHeader> Headers;
        /// <summary>The request body as a byte span.</summary>
        public readonly ReadOnlySpan<byte> Body;

        /// <summary>
        /// Initializes a new instance of the ParsedHttpRequest struct with request line, headers, and body.
        /// </summary>
        public ParsedHttpRequest(HttpRequestLine requestLine, ReadOnlySpan<HttpHeader> headers, ReadOnlySpan<byte> body)
        {
            RequestLine = requestLine;
            Headers = headers;
            Body = body;
        }
    }

    /// <summary>
    /// Parses a raw HTTP request into its component parts: request line, headers, and body.
    /// This method performs zero-copy parsing by working directly with byte spans.
    /// </summary>
    /// <param name="rawRequest">The raw HTTP request data as a byte span.</param>
    /// <returns>A ParsedHttpRequest containing the parsed components.</returns>
    /// <exception cref="EmptyRequestException">Thrown if the request is empty.</exception>
    /// <exception cref="MissingHeaderTerminatorException">Thrown if the header/body separator is not found.</exception>
    /// <exception cref="InvalidRequestLineException">Thrown if the request line is malformed.</exception>
    public static ParsedHttpRequest Parse(ReadOnlySpan<byte> rawRequest)
    {
        if (rawRequest.IsEmpty)
            throw new EmptyRequestException();

        // Find the header/body separator (CRLF CRLF: \r\n\r\n)
        int headerEnd = rawRequest.IndexOf("\r\n\r\n"u8);
        if (headerEnd < 0)
            throw new MissingHeaderTerminatorException();

        // Split the request into header and body sections
        var headerSpan = rawRequest.Slice(0, headerEnd);
        var bodySpan = rawRequest.Slice(headerEnd + 4);

        // Extract the request line (first line of headers)
        int firstLineEnd = headerSpan.IndexOf("\r\n"u8);
        if (firstLineEnd < 0)
            firstLineEnd = headerSpan.Length;

        var requestLineSpan = headerSpan.Slice(0, firstLineEnd);

        if (requestLineSpan.IsEmpty)
            throw new EmptyRequestException();

        // Parse the request line
        HttpRequestLine requestLine = ParseRequestLine(requestLineSpan);

        // Parse all header lines (skip the request line)
        var headers = ParseHeaders(headerSpan.Slice(firstLineEnd + 2));

        // Extract the body length from Content-Length header if present
        int contentLength = 0;
        foreach (var h in headers)
        {
            if (h.Name.SequenceEqual("Content-Length"u8) &&
                int.TryParse(Encoding.ASCII.GetString(h.Value), out int len))
            {
                contentLength = len;
                break;
            }
        }

        // Use the content length to determine the actual body span
        var body = contentLength > 0 && contentLength <= bodySpan.Length
            ? bodySpan.Slice(0, contentLength)
            : (contentLength == 0 ? ReadOnlySpan<byte>.Empty : bodySpan);

        return new ParsedHttpRequest(requestLine, headers, body);
    }

    /// <summary>
    /// Parses the HTTP request line to extract the method, path, and HTTP version.
    /// The request line format is: METHOD PATH HTTP_VERSION
    /// </summary>
    /// <param name="line">The request line as a byte span.</param>
    /// <returns>An HttpRequestLine containing the parsed method, path, and version.</returns>
    /// <exception cref="InvalidRequestLineException">Thrown if the request line format is invalid.</exception>
    private static HttpRequestLine ParseRequestLine(ReadOnlySpan<byte> line)
    {
        // Find the first space (separator between method and path)
        int firstSpace = line.IndexOf((byte)' ');
        if (firstSpace < 0)
            throw new InvalidRequestLineException("Invalid request line: missing method");

        // Find the second space (separator between path and version)
        int secondSpace = line.Slice(firstSpace + 1).IndexOf((byte)' ');
        if (secondSpace < 0)
            throw new InvalidRequestLineException("Invalid request line: missing path or version");
        secondSpace += firstSpace + 1;

        // Extract the three components
        var method = line.Slice(0, firstSpace);
        var path = line.Slice(firstSpace + 1, secondSpace - firstSpace - 1);
        var version = line.Slice(secondSpace + 1);

        return new HttpRequestLine(method, path, version);
    }

    /// <summary>
    /// Parses all HTTP headers from the header section into individual HttpHeader objects.
    /// Headers are expected to be in the format: "Name: Value" separated by CRLF.
    /// </summary>
    /// <param name="headersSpan">The headers section as a byte span (excluding the request line).</param>
    /// <returns>A span of parsed HttpHeader objects.</returns>
    private static ReadOnlySpan<HttpHeader> ParseHeaders(ReadOnlySpan<byte> headersSpan)
    {
        var headerList = new List<HttpHeader>();
        int start = 0;

        // Process each header line
        while (start < headersSpan.Length)
        {
            // Find the next CRLF to determine the line end
            int lineEnd = headersSpan.Slice(start).IndexOf("\r\n"u8);
            if (lineEnd < 0) lineEnd = headersSpan.Length - start;

            var line = headersSpan.Slice(start, lineEnd);
            start += lineEnd + 2;

            // Empty line indicates the end of headers
            if (line.IsEmpty) break;

            // Parse the header name and value separated by a colon
            int colonIndex = line.IndexOf((byte)':');
            if (colonIndex > 0)
            {
                var name = line.Slice(0, colonIndex).Trim();
                var value = line.Slice(colonIndex + 1).Trim();
                headerList.Add(new HttpHeader(name, value));
            }
        }

        return headerList.ToArray();
    }

    /// <summary>
    /// Trims whitespace (spaces and tabs) from both ends of a byte span.
    /// This is used to clean up header names and values during parsing.
    /// </summary>
    /// <param name="span">The byte span to trim.</param>
    /// <returns>A new span with leading and trailing whitespace removed.</returns>
    private static ReadOnlySpan<byte> Trim(this ReadOnlySpan<byte> span)
    {
        int start = 0;
        int end = span.Length - 1;

        // Skip leading whitespace (spaces and tabs)
        while (start <= end && (span[start] == ' ' || span[start] == '\t')) start++;
        // Skip trailing whitespace (spaces and tabs)
        while (end >= start && (span[end] == ' ' || span[end] == '\t')) end--;

        return span.Slice(start, end - start + 1);
    }
}
