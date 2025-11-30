# Changelog

All notable changes to this project will be documented in this file.

The format is based on "Keep a Changelog" and follows a simple release-per-section approach.

## [2025-11-30] - Added `HttpRequestAccumulator`

### Added
- `HttpRequestAccumulator`: streaming buffer that accumulates incoming TCP chunks, detects header terminator, and tracks body reads using Content-Length.
- XML documentation and public API polish for `HttpRequestAccumulator` and parser types.
- README clarification showing how `HttpRequestAccumulator` and `SpanBasedHttpParser` are used together.

### Notes
- This commit focused on streaming/accumulation support and documentation; the accumulator is intended to be used alongside the parser when receiving chunked data from sockets.

## [2025-11-27] - Initial commit / Beta artifacts

### Added
- Initial beta release artifacts and packaging `AnvilHttp 1.0.0-beta-1`.
- High-performance span-based HTTP request parser (`SpanBasedHttpParser`): zero-copy parsing of request line, headers and body.
- Custom parsing exception hierarchy for clearer error handling:
  - `HttpParsingException` (base)
  - `InvalidRequestLineException`
  - `MissingHeaderTerminatorException`
  - `EmptyRequestException`
- Benchmark suite using `BenchmarkDotNet` with multiple scenarios (simple GET, POST with body, large body, many headers, header iteration, etc.).
- Unit tests covering parser behaviors and error cases.
- README with project overview, intended use, and usage examples.

### Notes
- This project is intended primarily for educational and experimental use. Review carefully before using in production environments.
