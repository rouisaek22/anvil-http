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

```csharp
using System.Text;
using Anvil.Http.Parsing;

// Suppose `raw` is a byte[] containing a full HTTP request
ReadOnlySpan<byte> rawSpan = raw;

var parsed = SpanBasedHttpParser.Parse(rawSpan);

Console.WriteLine(parsed.RequestLine.MethodString());
Console.WriteLine(parsed.RequestLine.PathString());
Console.WriteLine(parsed.RequestLine.VersionString());

foreach (var h in parsed.Headers)
{
    Console.WriteLine($"{Encoding.ASCII.GetString(h.Name)}: {Encoding.ASCII.GetString(h.Value)}");
}

if (!parsed.Body.IsEmpty)
{
    // handle body (e.g., convert to string for text payloads)
    var bodyText = Encoding.UTF8.GetString(parsed.Body);
    Console.WriteLine(bodyText);
}
```

## Build

From the `src/Anvil.Http` directory:

```bash
dotnet build -c Release
```

## Notes

- The parser expects a full HTTP request with CRLF (`\r\n`) line endings and a header terminator (`\r\n\r\n`).
- `Content-Length` is used to decide the body length when present.

## License

MIT — see `LICENSE` file.

## Contributing

Contributions are welcome. Please open issues or pull requests on the repository:

https://github.com/rouisaek22/anvil-http