using System.Text;

namespace Anvil.Http.Parsing;

/// <summary>
/// Accumulates HTTP request data in chunks and tracks parsing state until a complete request is received.
/// </summary>
/// <remarks>
/// This class is designed for streaming scenarios where HTTP request data arrives in multiple chunks.
/// It maintains an internal buffer and tracks whether headers and body have been fully received.
/// The accumulator enforces a maximum request size limit to prevent denial-of-service attacks.
/// </remarks>
public sealed class HttpRequestAccumulator : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the HTTP headers have been completely received.
    /// </summary>
    /// <value>
    /// <c>true</c> if headers are complete and the accumulator is in the <see cref="State.ReadingBody"/> or completed state;
    /// <c>false</c> if still reading headers.
    /// </value>
    public bool HasHeaders => _state != State.ReadingHeaders;

    /// <summary>
    /// Gets the total number of bytes accumulated in the internal buffer.
    /// </summary>
    /// <value>The number of bytes currently stored in the buffer.</value>
    public int BytesAccumulated => (int)_buffer.Length;

    /// <summary>
    /// Gets the expected body length as determined by the <c>Content-Length</c> header.
    /// </summary>
    /// <value>
    /// The content length value if present and valid; <c>-1</c> if no <c>Content-Length</c> header exists or parsing failed.
    /// </value>
    public int ExpectedBodyLength => _contentLength;

    /// <summary>
    /// Gets the current parsing state of the accumulator.
    /// </summary>
    /// <value>The current <see cref="State"/> indicating whether reading headers or body.</value>
    public State CurrentState => _state;

    private readonly MemoryStream _buffer;
    private readonly int _maxRequestSize;
    private State _state;
    private int _contentLength;
    private int _headerEndIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestAccumulator"/> class.
    /// </summary>
    /// <param name="maxRequestSize">
    /// The maximum allowed size of the entire HTTP request in bytes. Defaults to 10 MB.
    /// If a request exceeds this limit, an <see cref="InvalidOperationException"/> is thrown.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxRequestSize"/> is less than or equal to zero.</exception>
    public HttpRequestAccumulator(int maxRequestSize = 10 * 1024 * 1024) // 10MB default
    {
        _maxRequestSize = maxRequestSize;
        _buffer = new MemoryStream(4096); // Initial capacity
        _state = State.ReadingHeaders;
        _contentLength = -1;
        _headerEndIndex = -1;
    }

    /// <summary>
    /// Accumulates a chunk of data and processes it according to the current parsing state.
    /// </summary>
    /// <param name="chunk">
    /// A span of bytes representing a new chunk of HTTP request data to accumulate.
    /// </param>
    /// <returns>
    /// An <see cref="AccumulatorResult"/> indicating whether the complete request has been received
    /// or if more data is needed.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if accumulating the chunk would exceed the maximum request size limit,
    /// or if the accumulator is in an invalid state.
    /// </exception>
    public AccumulatorResult Accumulate(ReadOnlySpan<byte> chunk)
    {
        // Check size limits
        if (_buffer.Length + chunk.Length > _maxRequestSize)
        {
            throw new InvalidOperationException($"Request size exceeds maximum limit of {_maxRequestSize} bytes");
        }

        // Write the new chunk
        _buffer.Write(chunk);

        return _state switch
        {
            State.ReadingHeaders => ProcessHeaders(),
            State.ReadingBody => ProcessBody(),
            _ => throw new InvalidOperationException("Invalid accumulator state")
        };
    }

    /// <summary>
    /// Gets a read-only span of all accumulated data in the internal buffer.
    /// </summary>
    /// <returns>
    /// A <see cref="ReadOnlySpan{T}"/> containing all accumulated bytes.
    /// </returns>
    /// <remarks>
    /// The returned span is valid only until the next call to <see cref="Accumulate"/> or <see cref="Reset"/>.
    /// </remarks>
    public ReadOnlySpan<byte> GetAccumulatedData()
    {
        return _buffer.GetBuffer().AsSpan(0, (int)_buffer.Length);
    }

    /// <summary>
    /// Resets the accumulator to its initial state, clearing all buffered data and resetting parsing state.
    /// </summary>
    /// <remarks>
    /// Call this method after successfully processing a complete request to prepare for the next request.
    /// </remarks>
    public void Reset()
    {
        _buffer.SetLength(0);
        _state = State.ReadingHeaders;
        _contentLength = -1;
        _headerEndIndex = -1;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="HttpRequestAccumulator"/> instance.
    /// </summary>
    /// <remarks>
    /// Call this method when the accumulator is no longer needed to release the underlying <see cref="MemoryStream"/>.
    /// </remarks>
    public void Dispose()
    {
        _buffer?.Dispose();
    }

    private AccumulatorResult ProcessHeaders()
    {
        var data = GetAccumulatedData();
        int headerEnd = data.IndexOf("\r\n\r\n"u8);

        if (headerEnd == -1)
            return AccumulatorResult.NeedMoreData;

        _headerEndIndex = headerEnd;
        _state = State.ReadingBody;
        _contentLength = ParseContentLength(data.Slice(0, headerEnd));

        // Check if we already have the complete body (or no body needed)
        return ProcessBody();
    }

    private AccumulatorResult ProcessBody()
    {
        // No body expected
        if (_contentLength < 0)
            return AccumulatorResult.Complete;

        var data = GetAccumulatedData();
        int bodyStart = _headerEndIndex + 4; // After \r\n\r\n
        int totalNeeded = bodyStart + _contentLength;

        return data.Length >= totalNeeded
            ? AccumulatorResult.Complete
            : AccumulatorResult.NeedMoreData;
    }

    private static int ParseContentLength(ReadOnlySpan<byte> headers)
    {
        ReadOnlySpan<byte> contentLengthHeader = "Content-Length: "u8;
        int clIndex = headers.IndexOf(contentLengthHeader);

        if (clIndex == -1)
            return -1;

        var valueStart = clIndex + contentLengthHeader.Length;
        var valueSpan = headers.Slice(valueStart);

        int valueEnd = valueSpan.IndexOf((byte)'\r');
        if (valueEnd == -1)
            return -1;

        var valueBytes = valueSpan.Slice(0, valueEnd);
        var valueString = Encoding.ASCII.GetString(valueBytes).Trim();

        return int.TryParse(valueString, out int length) ? length : -1;
    }


    /// <summary>
    /// Represents the current state of HTTP request parsing in the accumulator.
    /// </summary>
    /// <remarks>
    /// The state machine transitions from <see cref="State.ReadingHeaders"/> to <see cref="State.ReadingBody"/>
    /// after the complete HTTP headers have been received and parsed.
    /// </remarks>
    public enum State
    {
        /// <summary>
        /// The accumulator is waiting for the complete HTTP request headers (up to the CRLF CRLF sequence).
        /// </summary>
        ReadingHeaders,
        
        /// <summary>
        /// The accumulator is reading the HTTP message body data after the headers have been parsed.
        /// </summary>
        ReadingBody
    }
}

/// <summary>
/// Indicates the result of accumulating HTTP request data.
/// </summary>
/// <remarks>
/// This enum is returned by <see cref="HttpRequestAccumulator.Accumulate"/> to indicate whether
/// the complete HTTP request has been received or if additional data is required.
/// </remarks>
public enum AccumulatorResult
{
    /// <summary>
    /// The complete HTTP request (headers and body) has been successfully received and parsed.
    /// The accumulated request is ready for processing.
    /// </summary>
    Complete,
    
    /// <summary>
    /// The complete HTTP request has not yet been received.
    /// More data must be accumulated before the request can be considered complete.
    /// </summary>
    NeedMoreData
}