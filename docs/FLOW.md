# Anvil.Http Flow Architecture

This document explains how **`HttpRequestAccumulator`** and **`SpanBasedHttpParser`** work together to process HTTP requests from a socket.

---

## High-Level Overview

The library uses a **two-phase approach** to handle streaming HTTP data:

```
┌─────────────────────────────────────────────────────────┐
│  Phase 1: Buffering (HttpRequestAccumulator)            │
│  "Do I have a complete request?"                         │
└─────────────────────────────────────────────────────────┘
                          ↓
         Accumulate chunks until complete
                          ↓
          ✓ Headers received ✓ Body complete
                          ↓
┌─────────────────────────────────────────────────────────┐
│  Phase 2: Parsing (SpanBasedHttpParser)                 │
│  "Extract method, path, headers, body"                  │
└─────────────────────────────────────────────────────────┘
                          ↓
       RequestLine + Headers + Body
```

---

## Phase 1: HttpRequestAccumulator (Buffering)

### Purpose
Buffer incoming TCP/socket data chunks and determine **when a complete HTTP request has arrived**.

### Input
- Byte chunks arriving from the network (via `NetworkStream.ReadAsync()`)

### Output
- `AccumulatorResult.NeedMoreData` — waiting for more bytes
- `AccumulatorResult.Complete` — full request ready to parse

### State Machine

The accumulator has two states:

```
┌──────────────────┐
│ ReadingHeaders   │  ← Initial state
└────────┬─────────┘
         │
    [Found \r\n\r\n]
         │
         ↓
┌──────────────────┐
│ ReadingBody      │  ← Parsing Content-Length, waiting for body
└─────────────────┘
```

#### State 1: `ReadingHeaders`
- **Looking for**: The header terminator `\r\n\r\n` (CRLF CRLF)
- **When found**: 
  - Extract `Content-Length` header if present
  - Transition to `ReadingBody` state
  - Return `AccumulatorResult.Complete` if no body expected (Content-Length: 0 or absent)
  - Return `AccumulatorResult.NeedMoreData` if body is expected but not yet received

#### State 2: `ReadingBody`
- **Waiting for**: Enough bytes to satisfy `Content-Length`
- **When complete**: Return `AccumulatorResult.Complete`
- **If still waiting**: Return `AccumulatorResult.NeedMoreData`

### Example: Accumulating a POST Request

```
Socket sends 3 chunks:
┌────────────────────────────────────────────┐
│ Chunk 1: "POST /api/users HTTP/1.1\r\n"   │
│          "Content-Type: application/json\r\n"
│          "Content-Lengt" [incomplete]      │
└────────────────────────────────────────────┘
    ↓
[Accumulator.Accumulate(chunk1)]
    → Still in ReadingHeaders (no \r\n\r\n yet)
    → Result: NeedMoreData
    
┌────────────────────────────────────────────┐
│ Chunk 2: "h: 16\r\n\r\n"                   │
│          "{\"name\":\"Rou" [partial body]  │
└────────────────────────────────────────────┘
    ↓
[Accumulator.Accumulate(chunk2)]
    → Found \r\n\r\n! Extracted Content-Length: 16
    → Moved to ReadingBody state
    → Have 9 bytes of body, need 16
    → Result: NeedMoreData
    
┌────────────────────────────────────────────┐
│ Chunk 3: "is Abdelkader\"}"                │
│          [complete body - 7 more bytes]    │
└────────────────────────────────────────────┘
    ↓
[Accumulator.Accumulate(chunk3)]
    → Now have all 16 body bytes
    → Result: Complete
```

### Key Methods

| Method | Purpose |
|--------|---------|
| `Accumulate(chunk)` | Add new chunk, return complete/need-more status |
| `GetAccumulatedData()` | Get all buffered bytes as a span |
| `Reset()` | Clear buffer and state for next request |
| `Dispose()` | Release internal MemoryStream |

### Properties

| Property | Meaning |
|----------|---------|
| `HasHeaders` | True if headers section is complete |
| `CurrentState` | Current state: `ReadingHeaders` or `ReadingBody` |
| `ExpectedBodyLength` | Content-Length value, or -1 if absent |
| `BytesAccumulated` | Total bytes buffered so far |

---

## Phase 2: SpanBasedHttpParser (Parsing)

### Purpose
Parse a **complete HTTP request** and extract structured components.

### Input
- Complete HTTP request as byte array/span (from `Accumulator.GetAccumulatedData()`)

### Output
- `ParsedHttpRequest` containing:
  - `RequestLine` (Method, Path, Version)
  - `Headers` (list of `HttpHeader` key-value pairs)
  - `Body` (message body bytes)

### Parsing Steps

```csharp
var parsed = SpanBasedHttpParser.Parse(completeRequestBytes);

// Step 1: Find header/body separator (\r\n\r\n)
// Step 2: Extract request line (first line)
// Step 3: Parse all header lines into HttpHeader objects
// Step 4: Extract body using Content-Length
```

### Example: Parsing a POST Request

Input bytes:
```
POST /api/users HTTP/1.1\r\n
Content-Type: application/json\r\n
Content-Length: 16\r\n
\r\n
{"name":"Rouis"}
```

Parser extracts:

```csharp
parsed.RequestLine.Method    // "POST"
parsed.RequestLine.Path      // "/api/users"
parsed.RequestLine.Version   // "HTTP/1.1"

parsed.Headers[0].Name       // "Content-Type"
parsed.Headers[0].Value      // "application/json"
parsed.Headers[1].Name       // "Content-Length"
parsed.Headers[1].Value      // "16"

parsed.Body                  // {"name":"Rouis"}
```

---

## Complete End-to-End Flow

Here's how the two components work together in a server loop:

```csharp
using var accumulator = new HttpRequestAccumulator();
byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

while (true)
{
    // 1. Read a chunk from socket
    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
    if (bytesRead == 0) break; // Connection closed
    
    // 2. Accumulate the chunk
    var result = accumulator.Accumulate(buffer.AsSpan(0, bytesRead));
    
    // 3. Check if we have a complete request
    if (result == AccumulatorResult.Complete)
    {
        // 4. Get the complete request bytes
        byte[] completeRequest = accumulator.GetAccumulatedData().ToArray();
        
        // 5. Parse the complete request
        var parsed = SpanBasedHttpParser.Parse(completeRequest);
        
        // 6. Work with the parsed request
        Console.WriteLine($"Method: {parsed.RequestLine.MethodString()}");
        Console.WriteLine($"Path: {parsed.RequestLine.PathString()}");
        foreach (var header in parsed.Headers)
        {
            Console.WriteLine($"{header.Name}: {header.Value}");
        }
        
        // 7. Reset for the next request
        accumulator.Reset();
    }
}
```

### Flow Diagram

```
┌────────────────────────────────────────────────┐
│ Socket receives data (TCP chunks)              │
└─────────────────┬──────────────────────────────┘
                  │
                  ↓
        ┌─────────────────────┐
        │ Read chunk from      │
        │ NetworkStream        │
        └────────┬────────────┘
                 │
                 ↓
        ┌─────────────────────────────────────┐
        │ accumulator.Accumulate(chunk)       │
        │                                     │
        │ • Add bytes to buffer               │
        │ • Look for \r\n\r\n (headers end)   │
        │ • Parse Content-Length if found     │
        │ • Check if body is complete         │
        └────────┬────────────────────────────┘
                 │
    ┌────────────┴────────────┐
    │                         │
    ↓                         ↓
NeedMoreData           Complete
    │                    │
    │ Wait for         ✓ Have full
    │ more chunks       request
    │                   │
    │                   ↓
    │          ┌──────────────────────┐
    │          │ GetAccumulatedData()  │
    │          │ → byte[] of request   │
    │          └──────────┬───────────┘
    │                     │
    │                     ↓
    │          ┌─────────────────────────┐
    │          │ SpanBasedHttpParser.     │
    │          │ Parse(request bytes)     │
    │          │                         │
    │          │ → RequestLine           │
    │          │ → Headers[]             │
    │          │ → Body                  │
    │          └──────────┬──────────────┘
    │                     │
    │                     ↓
    │          ┌──────────────────────┐
    │          │ Process the request   │
    │          │ Send response         │
    │          └──────────┬───────────┘
    │                     │
    │                     ↓
    │          ┌──────────────────────┐
    │          │ accumulator.Reset()   │
    │          │ Clear for next req    │
    │          └──────────┬───────────┘
    │                     │
    └─────────────────────┴─→ Loop for next request
```

---

## Key Concepts

### 1. Why Separate Buffering from Parsing?

**Reason**: HTTP data arrives in unpredictable chunks from the network.

- **Chunks are unpredictable**: A chunk might contain part of the request line, or it might contain multiple full headers and partial body.
- **Parsing needs complete data**: `SpanBasedHttpParser` expects a complete, well-formed HTTP request to parse safely.
- **Buffering guarantees completeness**: `HttpRequestAccumulator` waits until a complete request is available, then the parser knows it has all the data.

### 2. Content-Length is Critical

The accumulator uses the `Content-Length` header to know **exactly how many body bytes to expect**:

```
If Content-Length: 100
  → Accumulator waits until it has headers + 100 body bytes
  
If Content-Length is absent
  → Accumulator considers the request complete as soon as headers arrive
```

### 3. Zero-Copy Semantics

Both components use `Span<T>` and `ReadOnlySpan<byte>` to avoid allocations:

- Accumulator stores bytes in a `MemoryStream` but exposes them as a `ReadOnlySpan<byte>`
- Parser works directly with the span; it does not copy the entire request
- Only header names/values are converted to byte arrays (necessary for storage in the `HttpHeader` struct)

### 4. State Reset is Essential

After processing a request, call `accumulator.Reset()`:

```csharp
accumulator.Reset();
// Clears: buffer, state (→ ReadingHeaders), ContentLength (→ -1)
```

If you forget to reset, the next chunk will be appended to the old buffer, corrupting the state machine.

---

## Error Handling

The parser throws custom exceptions:

| Exception | Cause |
|-----------|-------|
| `EmptyRequestException` | Request is empty or request line is empty |
| `InvalidRequestLineException` | Request line missing method, path, or version |
| `MissingHeaderTerminatorException` | No `\r\n\r\n` found (incomplete headers) |

The accumulator throws:

| Exception | Cause |
|-----------|-------|
| `InvalidOperationException` | Request exceeds max size (10 MB default) |
| `ArgumentOutOfRangeException` | Constructor receives invalid `maxRequestSize` |

---

## Example: Handling Multiple Requests on One Connection

```csharp
using var client = await listener.AcceptTcpClientAsync();
using var stream = client.GetStream();
using var accumulator = new HttpRequestAccumulator();

while (true)
{
    var buffer = ArrayPool<byte>.Shared.Rent(4096);
    try
    {
        int read = await stream.ReadAsync(buffer, 0, buffer.Length);
        if (read == 0) break; // Client closed connection
        
        var result = accumulator.Accumulate(buffer.AsSpan(0, read));
        
        if (result == AccumulatorResult.Complete)
        {
            var parsed = SpanBasedHttpParser.Parse(accumulator.GetAccumulatedData().ToArray());
            
            // Handle request
            await RespondToClient(stream, parsed);
            
            // Reset for next request on same connection
            accumulator.Reset();
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
```

---

## Summary

| Component | Role | Input | Output |
|-----------|------|-------|--------|
| **HttpRequestAccumulator** | Buffering & state tracking | Byte chunks from network | "Complete" / "Need more" |
| **SpanBasedHttpParser** | Structured extraction | Complete request bytes | RequestLine + Headers + Body |

**They work together**: Accumulator tells you *when* you have a complete request; Parser tells you *what* that request contains.
