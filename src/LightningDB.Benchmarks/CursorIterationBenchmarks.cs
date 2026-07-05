using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace LightningDB.Benchmarks;

[MemoryDiagnoser]
public class CursorIterationBenchmarks : BenchmarksBase
{
    [Params(1000)]
    public int Entries { get; set; }

    [Params(8, 256)]
    public int ValueSize { get; set; }

    private byte[] ValueBuffer { get; set; }
    private KeyBatch KeyBuffers { get; set; }

    public override void RunSetup()
    {
        ValueBuffer = new byte[ValueSize];
        KeyBuffers = KeyBatch.Generate(Entries, KeyOrdering.Sequential);

        using var tx = Env.BeginTransaction();
        for (var i = 0; i < KeyBuffers.Count; i++)
            tx.Put(DB, KeyBuffers[i], ValueBuffer);

        tx.Commit();
    }

    [Benchmark(Baseline = true)]
    public long EnumerateForeach()
    {
        using var transaction = Env.BeginTransaction(beginFlags: TransactionBeginFlags.ReadOnly);
        using var cursor = transaction.CreateCursor(DB);

        var total = 0L;
        foreach (var (_, value) in cursor.AsEnumerable())
            total += value.AsSpan().Length;

        return total;
    }

    //the pre-struct-enumerator cost: enumerating through the interface boxes the enumerator
    [Benchmark]
    public long EnumerateBoxed()
    {
        using var transaction = Env.BeginTransaction(beginFlags: TransactionBeginFlags.ReadOnly);
        using var cursor = transaction.CreateCursor(DB);

        var total = 0L;
        foreach (var (_, value) in (IEnumerable<(MDBValue key, MDBValue value)>)cursor.AsEnumerable())
            total += value.AsSpan().Length;

        return total;
    }

    [Benchmark]
    public long TryGetSpanDestination()
    {
        using var transaction = Env.BeginTransaction(beginFlags: TransactionBeginFlags.ReadOnly);

        Span<byte> destination = stackalloc byte[ValueSize];
        var total = 0L;
        for (var i = 0; i < KeyBuffers.Count; i++)
        {
            transaction.TryGet(DB, KeyBuffers[i], destination, out var valueLength);
            total += valueLength;
        }

        return total;
    }

    [Benchmark]
    public long TryGetAllocating()
    {
        using var transaction = Env.BeginTransaction(beginFlags: TransactionBeginFlags.ReadOnly);

        var total = 0L;
        for (var i = 0; i < KeyBuffers.Count; i++)
        {
            transaction.TryGet(DB, KeyBuffers[i], out byte[] value);
            total += value?.Length ?? 0;
        }

        return total;
    }
}
