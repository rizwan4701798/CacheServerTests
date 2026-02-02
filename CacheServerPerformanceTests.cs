using FluentAssertions;
using Manager;
using Moq;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using CacheServerModels;

namespace CacheServer.Tests;

public class CacheServerPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public CacheServerPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region CacheServer Performance

    [Fact]
    public void ProcessRequest_Should_Handle_100000_Requests_Quickly()
    {
        // Arrange
        var cacheMock = new Mock<ICacheManager>();
        cacheMock.Setup(c => c.Read(It.IsAny<string>()))
                 .Returns("value");
        cacheMock.Setup(c => c.EventNotifier)
                 .Returns(new Mock<ICacheEventNotifier>().Object);

        var server = new CacheServer.Server.CacheServer(5050, cacheMock.Object);

        var request = new CacheRequest
        {
            Operation = CacheOperation.Read,
            Key = "key"
        };

        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100000; i++)
        {
            server.ProcessRequest(request);
        }

        stopwatch.Stop();

        // Assert
        _output.WriteLine($"100K requests processed in {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "100K requests should complete in under 1 second");
    }

    #endregion

    #region CacheManager Performance

    [Fact]
    public void Single_Thread_Write_Performance()
    {
        var cache = new CacheManager(maxItems: 100000);
        const int iterations = 100000;

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        stopwatch.Stop();

        double opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Single-thread writes: {opsPerSecond:N0} ops/sec ({stopwatch.ElapsedMilliseconds}ms)");

        opsPerSecond.Should().BeGreaterThan(30000, "Should handle >30K writes/sec");
    }

    [Fact]
    public void Single_Thread_Read_Performance()
    {
        var cache = new CacheManager(maxItems: 10000);

        // Populate cache
        for (int i = 0; i < 10000; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        const int iterations = 100000;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            cache.Read($"key{i % 10000}");
        }

        stopwatch.Stop();

        double opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Single-thread reads: {opsPerSecond:N0} ops/sec ({stopwatch.ElapsedMilliseconds}ms)");

        opsPerSecond.Should().BeGreaterThan(50000, "Should handle >50K reads/sec");
    }

    [Fact]
    public void Multi_Thread_Read_Performance()
    {
        var cache = new CacheManager(maxItems: 10000);

        // Populate cache
        for (int i = 0; i < 10000; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        const int threads = 10;
        const int iterationsPerThread = 10000;
        var totalOps = new ConcurrentBag<int>();

        var stopwatch = Stopwatch.StartNew();

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < iterationsPerThread; i++)
            {
                cache.Read($"key{i % 10000}");
            }
            totalOps.Add(iterationsPerThread);
        });

        stopwatch.Stop();

        int total = totalOps.Sum();
        double opsPerSecond = total / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Multi-thread reads ({threads} threads): {opsPerSecond:N0} ops/sec ({stopwatch.ElapsedMilliseconds}ms)");

        opsPerSecond.Should().BeGreaterThan(10000, "Should scale with multiple threads");
    }

    [Fact]
    public void Multi_Thread_Write_Performance()
    {
        var cache = new CacheManager(maxItems: 100000);

        const int threads = 10;
        const int iterationsPerThread = 10000;
        var totalOps = new ConcurrentBag<int>();

        var stopwatch = Stopwatch.StartNew();

        Parallel.For(0, threads, threadId =>
        {
            for (int i = 0; i < iterationsPerThread; i++)
            {
                cache.Create($"key{threadId}_{i}", $"value{i}");
            }
            totalOps.Add(iterationsPerThread);
        });

        stopwatch.Stop();

        int total = totalOps.Sum();
        double opsPerSecond = total / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Multi-thread writes ({threads} threads): {opsPerSecond:N0} ops/sec ({stopwatch.ElapsedMilliseconds}ms)");

        opsPerSecond.Should().BeGreaterThan(20000, "Should handle concurrent writes");
    }

    [Fact]
    public void Mixed_Workload_Performance()
    {
        var cache = new CacheManager(maxItems: 10000);

        // Prepopulate
        for (int i = 0; i < 5000; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        const int threads = 8;
        const int opsPerThread = 10000;
        var totalOps = new ConcurrentBag<int>();

        var stopwatch = Stopwatch.StartNew();

        Parallel.For(0, threads, threadId =>
        {
            var random = new Random(threadId);
            for (int i = 0; i < opsPerThread; i++)
            {
                int key = random.Next(10000);
                int op = random.Next(100);

                if (op < 70) // 70% reads
                    cache.Read($"key{key}");
                else if (op < 85) // 15% writes
                    cache.Create($"key{key}", $"value{i}");
                else if (op < 95) // 10% updates
                    cache.Update($"key{key}", $"updated{i}");
                else // 5% deletes
                    cache.Delete($"key{key}");
            }
            totalOps.Add(opsPerThread);
        });

        stopwatch.Stop();

        double opsPerSecond = totalOps.Sum() / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Mixed workload ({threads} threads, 70/15/10/5 read/write/update/delete): {opsPerSecond:N0} ops/sec ({stopwatch.ElapsedMilliseconds}ms)");

        opsPerSecond.Should().BeGreaterThan(30000);
    }

    [Fact]
    public void LFU_Eviction_Performance_Under_Pressure()
    {
        var cache = new CacheManager(maxItems: 1000);

        // Fill cache
        for (int i = 0; i < 1000; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        const int iterations = 50000;
        var stopwatch = Stopwatch.StartNew();

        // Every create will trigger an eviction
        for (int i = 0; i < iterations; i++)
        {
            cache.Create($"newkey{i}", $"newvalue{i}");
        }

        stopwatch.Stop();

        double opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Writes with eviction: {opsPerSecond:N0} ops/sec ({stopwatch.ElapsedMilliseconds}ms)");

        opsPerSecond.Should().BeGreaterThan(10000, "Eviction should not severely impact performance");
    }

    [Fact]
    public void Memory_Pressure_Test()
    {
        var cache = new CacheManager(maxItems: 10000);
        var largeValue = new string('x', 10000); // 10KB string

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < 10000; i++)
        {
            cache.Create($"largekey{i}", largeValue);
        }

        stopwatch.Stop();

        _output.WriteLine($"Large value writes (10KB each): {stopwatch.ElapsedMilliseconds}ms for 10K items");

        // Verify data integrity
        cache.Read("largekey0").Should().Be(largeValue);
        cache.Read("largekey9999").Should().Be(largeValue);

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Should complete in reasonable time");
    }

    [Fact]
    public void Latency_Distribution_Test()
    {
        var cache = new CacheManager(maxItems: 10000);

        // Populate
        for (int i = 0; i < 10000; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        var latencies = new List<double>();
        var sw = new Stopwatch();

        for (int i = 0; i < 10000; i++)
        {
            sw.Restart();
            cache.Read($"key{i}");
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMicroseconds);
        }

        latencies.Sort();

        double p50 = latencies[(int)(latencies.Count * 0.50)];
        double p95 = latencies[(int)(latencies.Count * 0.95)];
        double p99 = latencies[(int)(latencies.Count * 0.99)];
        double max = latencies[latencies.Count - 1];

        _output.WriteLine($"Read latency distribution:");
        _output.WriteLine($"  P50: {p50:F2}µs");
        _output.WriteLine($"  P95: {p95:F2}µs");
        _output.WriteLine($"  P99: {p99:F2}µs");
        _output.WriteLine($"  Max: {max:F2}µs");

        p99.Should().BeLessThan(5000, "P99 latency should be under 5ms");
    }

    [Fact]
    public void Update_Performance()
    {
        var cache = new CacheManager(maxItems: 10000);

        // Populate
        for (int i = 0; i < 10000; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        const int iterations = 50000;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            cache.Update($"key{i % 10000}", $"updated{i}");
        }

        stopwatch.Stop();

        double opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Update operations: {opsPerSecond:N0} ops/sec ({stopwatch.ElapsedMilliseconds}ms)");

        opsPerSecond.Should().BeGreaterThan(30000, "Updates should be fast");
    }

    [Fact]
    public void Delete_Performance()
    {
        var cache = new CacheManager(maxItems: 50000);

        // Populate
        for (int i = 0; i < 50000; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < 50000; i++)
        {
            cache.Delete($"key{i}");
        }

        stopwatch.Stop();

        double opsPerSecond = 50000 / stopwatch.Elapsed.TotalSeconds;
        _output.WriteLine($"Delete operations: {opsPerSecond:N0} ops/sec ({stopwatch.ElapsedMilliseconds}ms)");

        opsPerSecond.Should().BeGreaterThan(30000, "Deletes should be fast");
    }

    #endregion
}
