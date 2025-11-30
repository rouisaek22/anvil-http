using System.Text;
using Anvil.Http.Parsing;
using Xunit;

namespace Anvil.Http.Tests;

public class HttpRequestAccumulatorTests
{
    [Fact]
    public void Accumulate_SimpleGetRequest_ReturnsCompleteWhenHeadersTerminated()
    {
        var accumulator = new HttpRequestAccumulator();
        var request = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";
        var result = accumulator.Accumulate(Encoding.UTF8.GetBytes(request).AsSpan());
        Assert.Equal(AccumulatorResult.Complete, result);
        Assert.True(accumulator.HasHeaders);
    }

    [Fact]
    public void Accumulate_IncompleteHeaders_ReturnsNeedMoreData()
    {
        var accumulator = new HttpRequestAccumulator();
        var partial = "GET / HTTP/1.1\r\nHost: localhost";
        var result = accumulator.Accumulate(Encoding.UTF8.GetBytes(partial).AsSpan());
        Assert.Equal(AccumulatorResult.NeedMoreData, result);
        Assert.False(accumulator.HasHeaders);
    }

    [Fact]
    public void Accumulate_HeadersInMultipleChunks_ReturnsCompleteWhenDone()
    {
        var accumulator = new HttpRequestAccumulator();
        var chunk1 = "GET / HTTP/1.1\r\n";
        var result1 = accumulator.Accumulate(Encoding.UTF8.GetBytes(chunk1).AsSpan());
        var chunk2 = "Host: localhost\r\n\r\n";
        var result2 = accumulator.Accumulate(Encoding.UTF8.GetBytes(chunk2).AsSpan());
        Assert.Equal(AccumulatorResult.NeedMoreData, result1);
        Assert.Equal(AccumulatorResult.Complete, result2);
    }

    [Fact]
    public void Reset_ClearsBufferAndState()
    {
        var accumulator = new HttpRequestAccumulator();
        var request = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";
        accumulator.Accumulate(Encoding.UTF8.GetBytes(request).AsSpan());
        accumulator.Reset();
        Assert.Equal(0, accumulator.BytesAccumulated);
        Assert.False(accumulator.HasHeaders);
        Assert.Equal(HttpRequestAccumulator.State.ReadingHeaders, accumulator.CurrentState);
    }

    [Fact]
    public void Reset_AllowsAccumulatingNextRequest()
    {
        var accumulator = new HttpRequestAccumulator();
        var request1 = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";
        var request2 = "POST /api HTTP/1.1\r\nContent-Length: 4\r\n\r\nTest";

        var result1 = accumulator.Accumulate(Encoding.UTF8.GetBytes(request1).AsSpan());
        accumulator.Reset();
        var result2 = accumulator.Accumulate(Encoding.UTF8.GetBytes(request2).AsSpan());

        Assert.Equal(AccumulatorResult.Complete, result1);
        Assert.Equal(AccumulatorResult.Complete, result2);
    }

    [Fact]
    public void Accumulate_OneByteAtATime_EventuallyCompletes()
    {
        var accumulator = new HttpRequestAccumulator();
        var request = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";
        var bytes = Encoding.UTF8.GetBytes(request);

        AccumulatorResult lastResult = AccumulatorResult.NeedMoreData;
        for (int i = 0; i < bytes.Length; i++)
        {
            lastResult = accumulator.Accumulate(bytes.AsSpan().Slice(i, 1));
        }

        Assert.Equal(AccumulatorResult.Complete, lastResult);
        Assert.True(accumulator.HasHeaders);
    }

    [Fact]
    public void BytesAccumulated_TracksCorrectly()
    {
        var accumulator = new HttpRequestAccumulator();
        var chunk1 = "GET / HTTP/1.1\r\n";
        var chunk2 = "Host: localhost\r\n\r\n";

        accumulator.Accumulate(Encoding.UTF8.GetBytes(chunk1).AsSpan());
        var bytesAfterChunk1 = accumulator.BytesAccumulated;

        accumulator.Accumulate(Encoding.UTF8.GetBytes(chunk2).AsSpan());
        var bytesAfterChunk2 = accumulator.BytesAccumulated;

        Assert.Equal(chunk1.Length, bytesAfterChunk1);
        Assert.Equal(chunk1.Length + chunk2.Length, bytesAfterChunk2);
    }

    [Fact]
    public void CurrentState_TransitionsCorrectly()
    {
        var accumulator = new HttpRequestAccumulator();
        Assert.Equal(HttpRequestAccumulator.State.ReadingHeaders, accumulator.CurrentState);

        var request = "POST / HTTP/1.1\r\nContent-Length: 5\r\n\r\nXYZ";
        accumulator.Accumulate(Encoding.UTF8.GetBytes(request).AsSpan());
        Assert.Equal(HttpRequestAccumulator.State.ReadingBody, accumulator.CurrentState);
    }

    [Fact]
    public void Accumulate_ExceedsMaxSize_ThrowsInvalidOperationException()
    {
        var maxSize = 100;
        var accumulator = new HttpRequestAccumulator(maxSize);
        var largeData = new byte[maxSize + 1];
        Assert.Throws<InvalidOperationException>(() =>
            accumulator.Accumulate(largeData.AsSpan()));
    }

    [Fact]
    public void GetAccumulatedData_ReturnsCorrectBytes()
    {
        var accumulator = new HttpRequestAccumulator();
        var request = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";
        accumulator.Accumulate(Encoding.UTF8.GetBytes(request).AsSpan());
        var accumulated = accumulator.GetAccumulatedData();
        Assert.Equal(request, Encoding.UTF8.GetString(accumulated));
    }

    [Fact]
    public void Accumulate_ZeroContentLength_IsComplete()
    {
        var accumulator = new HttpRequestAccumulator();
        var request = "POST / HTTP/1.1\r\nContent-Length: 0\r\n\r\n";
        var result = accumulator.Accumulate(Encoding.UTF8.GetBytes(request).AsSpan());
        Assert.Equal(AccumulatorResult.Complete, result);
    }

    [Fact]
    public void Accumulate_MultipleHeadersWithBody_ParsesCorrectly()
    {
        var accumulator = new HttpRequestAccumulator();
        var sb = new StringBuilder();
        sb.Append("POST /api/users HTTP/1.1\r\n");
        sb.Append("Host: localhost\r\n");
        sb.Append("Content-Type: application/json\r\n");
        sb.Append("Content-Length: 15\r\n");
        sb.Append("X-Custom: value\r\n");
        sb.Append("\r\n");
        sb.Append("{\"name\":\"John\"}");

        var result = accumulator.Accumulate(Encoding.UTF8.GetBytes(sb.ToString()).AsSpan());
        Assert.Equal(AccumulatorResult.Complete, result);
        Assert.True(accumulator.HasHeaders);
    }

    [Fact]
    public void Constructor_CustomMaxRequestSize_EnforcesLimit()
    {
        var accumulator = new HttpRequestAccumulator(5000);
        var tooLarge = new byte[5001];
        Assert.Throws<InvalidOperationException>(() =>
            accumulator.Accumulate(tooLarge.AsSpan()));
    }

    [Fact]
    public void HasHeaders_ReflectsState()
    {
        var accumulator = new HttpRequestAccumulator();
        var partialHeaders = "GET / HTTP/1.1\r\nHost:";
        var completeRequest = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";

        Assert.False(accumulator.HasHeaders);
        accumulator.Accumulate(Encoding.UTF8.GetBytes(partialHeaders).AsSpan());
        Assert.False(accumulator.HasHeaders);
        accumulator.Reset();
        accumulator.Accumulate(Encoding.UTF8.GetBytes(completeRequest).AsSpan());
        Assert.True(accumulator.HasHeaders);
    }

    [Fact]
    public void Accumulate_MalformedContentLength_TreatsAsAbsent()
    {
        var accumulator = new HttpRequestAccumulator();
        var request = "POST / HTTP/1.1\r\nContent-Length: not-a-number\r\n\r\n";
        var result = accumulator.Accumulate(Encoding.UTF8.GetBytes(request).AsSpan());
        Assert.Equal(-1, accumulator.ExpectedBodyLength);
        Assert.Equal(AccumulatorResult.Complete, result);
    }

    [Fact]
    public void Dispose_NoExceptions()
    {
        var accumulator = new HttpRequestAccumulator();
        var request = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";
        accumulator.Accumulate(Encoding.UTF8.GetBytes(request).AsSpan());
        accumulator.Dispose();
        accumulator.Dispose();
    }

    [Fact]
    public void Accumulate_LargeBody_HandlesCorrectly()
    {
        var accumulator = new HttpRequestAccumulator();
        var largeBody = new string('X', 50000);
        var request = $"POST / HTTP/1.1\r\nContent-Length: {largeBody.Length}\r\n\r\n{largeBody}";
        var result = accumulator.Accumulate(Encoding.UTF8.GetBytes(request).AsSpan());
        Assert.Equal(AccumulatorResult.Complete, result);
        Assert.True(accumulator.HasHeaders);
    }

    [Fact]
    public void Accumulate_NoContentLength_ReturnsNegativeOne()
    {
        var accumulator = new HttpRequestAccumulator();
        var request = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";
        accumulator.Accumulate(Encoding.UTF8.GetBytes(request).AsSpan());
        Assert.Equal(-1, accumulator.ExpectedBodyLength);
    }
}
