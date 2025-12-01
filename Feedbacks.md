# Feedbacks

I have dedicated this special file for everyone that help with a feedback on any social media platform, So **Thanks for all your beautiful feedbacks**.

## Reddit:

hoodoo##:

```txt
First, the server should handle bare LF. The specification explicitly allows it. I write HTTP clients that send only LF and I’ve never seen a server fail on it. If I ever meet one that can’t, I simply avoid it.

Also, avoid anti-patterns in example code: you’re using ArrayPool<byte>.Shared inside an async method. This can break. The shared pool is TLS-based, so you might rent on one thread and return on another. That mismatch can cause the pool to reject the buffer or behave inconsistently, eventually exhausting buffers on one thread in edge cases.

Date: Mon 1 Dec 2025
```

#iserable##:
```txt
This is not a zero-copy library. MemoryStream.Write performs a copy.  
There are also several avoidable allocations.

It’s a nice project, but achieving true zero-copy would require rethinking the accumulator design. Instead of accumulating into a buffer, expose a stream that the parser reads from. That’s how you get an actual zero-copy pipeline.

Will it be faster? That depends on size and cache locality.

Date: Mon 1 Dec 2025
```

All#AreBurgers:
```txt
Interesting! Have you benchmarked it against whatever Kestrel uses?

Date: Sun 30 Nov 2025
```

Church#NewEpoch:
```txt
Why not use System.IO.Pipelines for zero-copy buffering?

In your example you call:

    accumulator.GetAccumulatedData().ToArray()

Based on the source, this retrieves a span from a MemoryStream, but .ToArray() still performs a copy.

Inside Accumulate(), you call MemoryStream.Write() with a ReadOnlySpan. That also allocates and copies.

System.IO.Pipelines was designed specifically to solve this situation, so it may suit your needs better.

Date: Sun 30 Nov 2025
```