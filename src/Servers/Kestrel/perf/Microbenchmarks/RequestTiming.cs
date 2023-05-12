// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace Microsoft.AspNetCore.Server.Kestrel.Microbenchmarks;

public class RequestTiming
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly long[] _values = new long[30];

    public RequestTiming()
    {
        var values = Enumerable.Range(1, 30).Select(i => long.MaxValue - (i * 100)).ToArray();
        _data = new ReadOnlyMemory<byte>(MemoryMarshal.AsBytes(values.AsSpan()).ToArray());
    }

    [Params(1, 6, 18, 30)]
    public int GetTimestampCallCount { get; set; }

    [Benchmark]
    public long[] GetTimestampMarshal()
    {
        for (int i = 0; i < GetTimestampCallCount; i++)
        {
            var valuesSpan = MemoryMarshal.Cast<byte, long>(_data.Span);
            _values[i] = valuesSpan[i];
        }

        return _values;
    }

    [Benchmark]
    public long[] GetTimestampCached()
    {
        long[] valuesArray = MemoryMarshal.Cast<byte, long>(_data.Span).ToArray();

        for (int i = 0; i < GetTimestampCallCount; i++)
        {
            _values[i] = valuesArray[i];
        }

        return _values;
    }

    [Benchmark]
    public long[] GetTimestampUnsafe()
    {
        for (int i = 0; i < GetTimestampCallCount; i++)
        {
            _values[i] = UnsafeValuesSpan[i];
        }

        return _values;
    }

    private ReadOnlySpan<long> UnsafeValuesSpan => MemoryMarshal.CreateSpan(
        ref Unsafe.As<byte, long>(ref MemoryMarshal.GetReference(_data.Span)),
        30);
}
