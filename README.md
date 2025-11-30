# Anvil.Http

<img src="assets/anvil-http-logo.png" alt="Anvil.Http logo" width="50%" />

High-performance HTTP request parser for .NET using zero-copy, span-based parsing.

## Overview

`Anvil.Http` provides a small, fast HTTP request parser that operates on byte spans to avoid unnecessary allocations. It parses the request line, headers, and body with a focus on performance and minimal allocations.

## Features

- Zero-copy parsing using `ReadOnlySpan<byte>` and `ref struct` types
- Parses request line (method/path/version), headers, and body
- No dependencies; targets .NET 9.0

## Intended Use

- This project was built for educational purposes and as a learning exercise. It is intended for use in personal projects, demos, and experimentation.
- It is not guaranteed to be production-ready; if you plan to use it in production systems, review it carefully and adapt it to your needs.


## Quick Usage

`SpanBasedHttpParser` and `HttpRequestAccumulator` work together as a system:
- **HttpRequestAccumulator**: Buffers incoming data chunks and tracks parsing state until a complete HTTP request is received
- **SpanBasedHttpParser**: Parses the accumulated complete request into structured components

```csharp
// Server Loop - Accumulator buffers data, Parser processes complete requests
private async Task ProcessRequestAsync(NetworkStream stream)
    {
        using var accumulator = new HttpRequestAccumulator();
        byte[] buffer = _pool.Rent(4096);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer)) != 0)
            {
                // Accumulator buffers incoming chunks and detects when complete
                var result = accumulator.Accumulate(buffer.AsSpan(0, read));
                if (result == AccumulatorResult.Complete)
                {
                    // Get the complete accumulated request
                    var completeRequest = accumulator.GetAccumulatedData().ToArray();
                    Console.WriteLine($"Complete request: {completeRequest.Length} bytes");

                    // Parser processes the complete request
                    var parsedRequest = SpanBasedHttpParser.Parse(completeRequest);
                    
                    // Map to Request object and handle
                    Request request = MapSpanToRequest(parsedRequest);
                    await HandleRouteAsync(request, stream);

                    // Reset accumulator for next request
                    accumulator.Reset();
                }
            }
        }
        finally
        {
            _pool.Return(buffer);
        }
    }
```

## Build

From the `src/Anvil.Http` directory:

```bash
dotnet build -c Release
```

## Documentation

For a detailed explanation of how the two-phase architecture works (buffering and parsing), see:

- **[FLOW.md](./FLOW.md)** — Comprehensive guide to the accumulator and parser flow, state machine transitions, and end-to-end examples with ASCII diagrams.
- **[CHANGELOG.md](./CHANGELOG.md)** — Version history and feature tracking.

## Notes

- The parser expects a full HTTP request with CRLF (`\r\n`) line endings and a header terminator (`\r\n\r\n`).
- `Content-Length` is used to decide the body length when present.

## License

MIT — see `LICENSE` file.

## Contributing

Contributions are welcome. Please open issues or pull requests on the repository:

https://github.com/rouisaek22/anvil-http