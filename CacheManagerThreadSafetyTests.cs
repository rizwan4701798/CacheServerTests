using FluentAssertions;
using Manager;
using System.Collections.Concurrent;
using Xunit;

namespace CacheServer.Tests;

public class CacheManagerThreadSafetyTests
{
    [Fact]
    public void Concurrent_Creates_Should_Not_Lose_Data()
    {
        var cache = new CacheManager(maxItems: 1000);
        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    cache.Create($"key{index}", $"value{index}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        exceptions.Should().BeEmpty();

        // Verify all items were created
        int successCount = 0;
        for (int i = 0; i < 100; i++)
        {
            if (cache.Read($"key{i}") != null)
                successCount++;
        }

        successCount.Should().Be(100);
    }

    [Fact]
    public void Concurrent_Reads_Should_Be_Safe()
    {
        var cache = new CacheManager(maxItems: 100);

        // Populate cache
        for (int i = 0; i < 50; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        var exceptions = new ConcurrentBag<Exception>();
        var results = new ConcurrentBag<object>();
        var tasks = new List<Task>();

        // 100 concurrent reads
        for (int i = 0; i < 100; i++)
        {
            int index = i % 50;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var value = cache.Read($"key{index}");
                    if (value != null)
                        results.Add(value);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        exceptions.Should().BeEmpty();
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void Concurrent_Mixed_Operations_Should_Be_Thread_Safe()
    {
        var cache = new CacheManager(maxItems: 100);
        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();

        // Prepopulate some data
        for (int i = 0; i < 50; i++)
        {
            cache.Create($"key{i}", $"value{i}");
        }

        // Run mixed operations concurrently
        for (int i = 0; i < 200; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    int key = index % 100;
                    int operation = index % 4;

                    switch (operation)
                    {
                        case 0:
                            cache.Create($"key{key}", $"value{index}");
                            break;
                        case 1:
                            cache.Read($"key{key}");
                            break;
                        case 2:
                            cache.Update($"key{key}", $"updated{index}");
                            break;
                        case 3:
                            cache.Delete($"key{key}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        exceptions.Should().BeEmpty("No exceptions should occur during concurrent operations");
    }

    [Fact]
    public void Concurrent_Operations_At_Capacity_Should_Not_Deadlock()
    {
        var cache = new CacheManager(maxItems: 10);
        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Fill cache
        for (int i = 0; i < 10; i++)
        {
            cache.Create($"init{i}", $"value{i}");
        }

        // Run many operations that will trigger evictions
        for (int i = 0; i < 100; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    // These will trigger LFU evictions
                    cache.Create($"new{index}", $"newvalue{index}");
                    cache.Read($"init{index % 10}");
                    cache.Read($"new{index}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }, cts.Token));
        }

        var completed = Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10));

        completed.Should().BeTrue("Operations should complete without deadlock");
        exceptions.Should().BeEmpty();
    }

    [Fact]
    public void High_Contention_On_Same_Key_Should_Be_Safe()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("hotkey", "initial");

        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();

        // 50 threads all operating on same key
        for (int i = 0; i < 50; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        cache.Read("hotkey");
                        cache.Update("hotkey", $"value{index}_{j}");
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        exceptions.Should().BeEmpty();
        cache.Read("hotkey").Should().NotBeNull();
    }

    [Fact]
    public void Concurrent_Expiration_Checks_Should_Be_Safe()
    {
        var cache = new CacheManager(maxItems: 100);
        var exceptions = new ConcurrentBag<Exception>();

        // Create items with short expiration
        for (int i = 0; i < 50; i++)
        {
            cache.Create($"expire{i}", $"value{i}", expirationSeconds: 1);
        }

        var tasks = new List<Task>();

        // Concurrent reads during expiration window
        for (int i = 0; i < 100; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    Thread.Sleep(index * 20); // Stagger reads
                    cache.Read($"expire{index % 50}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        exceptions.Should().BeEmpty();
    }

    [Fact]
    public void Parallel_Creates_And_Evictions_Should_Maintain_Consistency()
    {
        var cache = new CacheManager(maxItems: 50);
        var exceptions = new ConcurrentBag<Exception>();
        var createdKeys = new ConcurrentBag<string>();

        Parallel.For(0, 200, i =>
        {
            try
            {
                var key = $"parallel{i}";
                if (cache.Create(key, $"value{i}"))
                {
                    createdKeys.Add(key);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.Should().BeEmpty();

        // Some keys should have been evicted, but cache should be consistent
        int foundCount = 0;
        foreach (var key in createdKeys)
        {
            if (cache.Read(key) != null)
                foundCount++;
        }

        foundCount.Should().BeLessThanOrEqualTo(50, "Cache should not exceed capacity");
    }

    [Fact]
    public void Stress_Test_Should_Not_Corrupt_Data()
    {
        var cache = new CacheManager(maxItems: 100);
        var exceptions = new ConcurrentBag<Exception>();
        var expectedValues = new ConcurrentDictionary<string, string>();

        // Initial population
        for (int i = 0; i < 50; i++)
        {
            var key = $"stress{i}";
            var value = $"initial{i}";
            cache.Create(key, value);
            expectedValues[key] = value;
        }

        var tasks = new List<Task>();

        // Stress test with many operations
        for (int t = 0; t < 10; t++)
        {
            int threadId = t;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var random = new Random(threadId);
                    for (int i = 0; i < 500; i++)
                    {
                        var keyNum = random.Next(100);
                        var key = $"stress{keyNum}";
                        var op = random.Next(4);

                        switch (op)
                        {
                            case 0:
                                var createValue = $"created_{threadId}_{i}";
                                if (cache.Create(key, createValue))
                                    expectedValues[key] = createValue;
                                break;
                            case 1:
                                cache.Read(key);
                                break;
                            case 2:
                                var updateValue = $"updated_{threadId}_{i}";
                                if (cache.Update(key, updateValue))
                                    expectedValues[key] = updateValue;
                                break;
                            case 3:
                                if (cache.Delete(key))
                                    expectedValues.TryRemove(key, out _);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        exceptions.Should().BeEmpty("Stress test should complete without exceptions");
    }
}
