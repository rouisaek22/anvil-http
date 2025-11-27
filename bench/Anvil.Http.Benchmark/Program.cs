using BenchmarkDotNet.Running;
using Anvil.Http.Benchmark;

var summary = BenchmarkRunner.Run<SpanBasedHttpParserBenchmark>();
