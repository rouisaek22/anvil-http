using System.Text;
using Anvil.Http.Parsing;
using Anvil.Http.Exceptions;

namespace Anvil.Http.Tests;

public class SpanBasedHttpParserTests
{
        [Fact]
        public void ParseHttpRequest_ValidGetRequest_ParsesMethodPathAndVersion()
        {
                // Arrange
                var sb = new StringBuilder();
                sb.Append("GET /api/users HTTP/1.1\r\n")
                  .Append("Host: localhost:8080\r\n")
                  .Append("\r\n");

                Span<byte> bytes = Encoding.UTF8.GetBytes(sb.ToString()).AsSpan();

                // Act
                var request = SpanBasedHttpParser.Parse(bytes);

                // Assert
                Assert.Equal("GET", request.RequestLine.MethodString());
                Assert.Equal("/api/users", request.RequestLine.PathString());
                Assert.Equal("HTTP/1.1", request.RequestLine.VersionString());
        }

        [Fact]
        public void ParseHttpRequest_PostRequestWithBody_ParsesCorrectly()
        {
                // Arrange
                var sb = new StringBuilder();
                sb.Append("POST /api/users HTTP/1.1\r\n")
                  .Append("Host: localhost:8080\r\n")
                  .Append("Content-Type: application/json\r\n")
                  .Append("Content-Length: 28\r\n")
                  .Append("\r\n")
                  .Append("{\"name\":\"John\",\"age\":30}");

                Span<byte> bytes = Encoding.UTF8.GetBytes(sb.ToString()).AsSpan();

                // Act
                var request = SpanBasedHttpParser.Parse(bytes);

                // Assert
                Assert.Equal("POST", request.RequestLine.MethodString());
                Assert.Equal("/api/users", request.RequestLine.PathString());
                Assert.Equal("HTTP/1.1", request.RequestLine.VersionString());
                Assert.Equal("{\"name\":\"John\",\"age\":30}", Encoding.UTF8.GetString(request.Body));
                Assert.Equal("application/json", Encoding.UTF8.GetString(request.Headers[1].Value));
        }

        [Fact]
        public void ParseHttpRequest_WithMultipleHeaders_ParsesAllHeaders()
        {
                // Arrange
                var sb = new StringBuilder();
                sb.Append("GET / HTTP/1.1\r\n")
                  .Append("Host: example.com\r\n")
                  .Append("User-Agent: Test-Agent/1.0\r\n")
                  .Append("Accept: application/json\r\n")
                  .Append("Authorization: Bearer token123\r\n")
                  .Append("\r\n");

                Span<byte> bytes = Encoding.UTF8.GetBytes(sb.ToString()).AsSpan();

                // Act
                var request = SpanBasedHttpParser.Parse(bytes);

                // Assert
                Assert.Equal(4, request.Headers.Length);
                Assert.Equal("Host", Encoding.UTF8.GetString(request.Headers[0].Name));
                Assert.Equal("example.com", Encoding.UTF8.GetString(request.Headers[0].Value));
                Assert.Equal("User-Agent", Encoding.UTF8.GetString(request.Headers[1].Name));
                Assert.Equal("Test-Agent/1.0", Encoding.UTF8.GetString(request.Headers[1].Value));
                Assert.Equal("Accept", Encoding.UTF8.GetString(request.Headers[2].Name));
                Assert.Equal("application/json", Encoding.UTF8.GetString(request.Headers[2].Value));
                Assert.Equal("Authorization", Encoding.UTF8.GetString(request.Headers[3].Name));
                Assert.Equal("Bearer token123", Encoding.UTF8.GetString(request.Headers[3].Value));
        }

        [Fact]
        public void ParseHttpRequest_HeadersCaseInsensitive_WorksCorrectly()
        {
                // Arrange
                var sb = new StringBuilder();
                sb.Append("GET / HTTP/1.1\r\n")
                  .Append("HOST: example.com\r\n")
                  .Append("user-AGENT: TestAgent\r\n")
                  .Append("CONTENT-TYPE: application/json\r\n")
                  .Append("\r\n");

                Span<byte> bytes = Encoding.UTF8.GetBytes(sb.ToString()).AsSpan();

                // Act
                var request = SpanBasedHttpParser.Parse(bytes);

                // Assert
                Assert.Equal(3, request.Headers.Length);
                Assert.Equal("example.com", Encoding.UTF8.GetString(request.Headers[0].Value));
        }

        [Fact]
        public void ParseHttpRequest_MissingHeaderTerminator_ThrowsArgumentException()
        {
                // Arrange
                var sb = new StringBuilder();
                sb.Append("GET /api/users HTTP/1.1\r\n")
                  .Append("Host: localhost:8080");
                // Missing \r\n\r\n

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());

                // Act & Assert
                var exception = Assert.Throws<MissingHeaderTerminatorException>(() =>
                    SpanBasedHttpParser.Parse(bytes.AsSpan()));
                Assert.Contains("missing header terminator", exception.Message);
        }

        [Fact]
        public void ParseHttpRequest_EmptyRequest_ThrowsArgumentException()
        {
                // Arrange
                var sb = new StringBuilder();
                sb.Append("\r\n\r\n");

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());

                // Act & Assert
                var exception = Assert.Throws<EmptyRequestException>(() =>
                    SpanBasedHttpParser.Parse(bytes.AsSpan()));
                Assert.Contains("Empty", exception.Message);
        }

        [Fact]
        public void ParseHttpRequest_BodyWithContentLength_RespectsContentLength()
        {
                // Arrange
                var sb = new StringBuilder();
                sb.Append("POST /api/test HTTP/1.1\r\n")
                  .Append("Content-Type: text/plain\r\n")
                  .Append("Content-Length: 5\r\n")
                  .Append("\r\n")
                  .Append("Hello, World!"); // Extra text that should be ignored

                Span<byte> bytes = Encoding.UTF8.GetBytes(sb.ToString()).AsSpan();

                // Act
                var request = SpanBasedHttpParser.Parse(bytes);

                // Assert
                Assert.Equal("Hello", Encoding.UTF8.GetString(request.Body)); // Only first 5 characters
        }

        [Fact]
        public void ParseHttpRequest_EmptyBodyWithContentLength_ReturnsEmptyBody()
        {
                // Arrange
                var sb = new StringBuilder();
                sb.Append("POST /api/test HTTP/1.1\r\n")
                  .Append("Content-Length: 0\r\n")
                  .Append("\r\n")
                  .Append("This should be ignored");

                Span<byte> bytes = Encoding.UTF8.GetBytes(sb.ToString()).AsSpan();

                // Act
                var request = SpanBasedHttpParser.Parse(bytes);

                // Assert
                Assert.Equal("", Encoding.UTF8.GetString(request.Body));
        }
}