using FluentAssertions;
using Manager;
using Xunit;

namespace CacheServer.Tests;

public class CacheManagerRobustnessTests
{
    #region Edge Cases

    [Fact]
    public void Should_Handle_Very_Long_Keys()
    {
        var cache = new CacheManager(maxItems: 10);
        var longKey = new string('k', 10000);

        cache.Create(longKey, "value").Should().BeTrue();
        cache.Read(longKey).Should().Be("value");
    }

    [Fact]
    public void Should_Handle_Special_Characters_In_Keys()
    {
        var cache = new CacheManager(maxItems: 20);
        var specialKeys = new[]
        {
            "key with spaces",
            "key\twith\ttabs",
            "key\nwith\nnewlines",
            "key:with:colons",
            "key/with/slashes",
            "key\\with\\backslashes",
            "key\"with\"quotes",
            "key'with'apostrophes",
            "unicode:日本語",
            "emoji:🎉🚀"
        };

        foreach (var key in specialKeys)
        {
            cache.Create(key, "value").Should().BeTrue($"Key '{key}' should be created");
            cache.Read(key).Should().Be("value", $"Key '{key}' should be readable");
        }
    }

    [Fact]
    public void Should_Handle_Very_Large_Values()
    {
        var cache = new CacheManager(maxItems: 10);
        var largeValue = new string('v', 1_000_000); // 1MB string

        cache.Create("large", largeValue).Should().BeTrue();
        cache.Read("large").Should().Be(largeValue);
    }

    [Fact]
    public void Should_Handle_Minimum_Capacity()
    {
        var cache = new CacheManager(maxItems: 1);

        cache.Create("key1", "value1").Should().BeTrue();
        cache.Create("key2", "value2").Should().BeTrue(); // Should evict key1

        cache.Read("key1").Should().BeNull();
        cache.Read("key2").Should().Be("value2");
    }

    [Fact]
    public void Should_Handle_Empty_String_Value()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Create("emptyvalue", "").Should().BeTrue();
        cache.Read("emptyvalue").Should().Be("");
    }

    [Fact]
    public void Should_Handle_Whitespace_Only_Value()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Create("whitespace", "   ").Should().BeTrue();
        cache.Read("whitespace").Should().Be("   ");
    }

    #endregion

    #region Recovery Scenarios

    [Fact]
    public void Should_Recover_After_Rapid_Create_Delete_Cycles()
    {
        var cache = new CacheManager(maxItems: 100);

        // Rapid create-delete cycles
        for (int i = 0; i < 1000; i++)
        {
            cache.Create($"key{i}", $"value{i}");
            cache.Delete($"key{i}");
        }

        // Cache should still be functional
        cache.Create("final", "value").Should().BeTrue();
        cache.Read("final").Should().Be("value");
    }

    [Fact]
    public void Should_Handle_Repeated_Operations_On_Same_Key()
    {
        var cache = new CacheManager(maxItems: 10);

        for (int i = 0; i < 1000; i++)
        {
            cache.Create("samekey", $"value{i}");
            cache.Update("samekey", $"updated{i}");
            cache.Read("samekey");
            cache.Delete("samekey");
        }

        // Should still work
        cache.Create("samekey", "final").Should().BeTrue();
        cache.Read("samekey").Should().Be("final");
    }

    [Fact]
    public void Should_Handle_All_Items_Expiring()
    {
        var cache = new CacheManager(maxItems: 10);

        // Add items with very short expiration
        for (int i = 0; i < 10; i++)
        {
            cache.Create($"expire{i}", $"value{i}", expirationSeconds: 1);
        }

        Thread.Sleep(1500);

        // All should be expired
        for (int i = 0; i < 10; i++)
        {
            cache.Read($"expire{i}").Should().BeNull();
        }

        // Cache should still be functional
        cache.Create("newkey", "newvalue").Should().BeTrue();
        cache.Read("newkey").Should().Be("newvalue");
    }

    [Fact]
    public void Should_Handle_Interleaved_Expiring_And_NonExpiring_Items()
    {
        var cache = new CacheManager(maxItems: 20);

        // Mix of expiring and non-expiring items
        for (int i = 0; i < 10; i++)
        {
            cache.Create($"expire{i}", $"expiring{i}", expirationSeconds: 1);
            cache.Create($"persist{i}", $"persistent{i}");
        }

        Thread.Sleep(1500);

        // Expiring items should be gone
        for (int i = 0; i < 10; i++)
        {
            cache.Read($"expire{i}").Should().BeNull();
        }

        // Persistent items should remain
        for (int i = 0; i < 10; i++)
        {
            cache.Read($"persist{i}").Should().Be($"persistent{i}");
        }
    }

    #endregion

    #region Data Integrity

    [Fact]
    public void Values_Should_Not_Be_Corrupted_After_Many_Operations()
    {
        var cache = new CacheManager(maxItems: 100);
        var expectedValues = new Dictionary<string, string>();

        // Create initial data
        for (int i = 0; i < 50; i++)
        {
            var key = $"key{i}";
            var value = $"value{i}";
            cache.Create(key, value);
            expectedValues[key] = value;
        }

        // Perform many random operations
        var random = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            var keyNum = random.Next(100);
            var key = $"key{keyNum}";
            var op = random.Next(3);

            switch (op)
            {
                case 0: // Create
                    var newValue = $"new{i}";
                    if (cache.Create(key, newValue))
                        expectedValues[key] = newValue;
                    break;
                case 1: // Update
                    var updateValue = $"update{i}";
                    if (cache.Update(key, updateValue))
                        expectedValues[key] = updateValue;
                    break;
                case 2: // Delete
                    if (cache.Delete(key))
                        expectedValues.Remove(key);
                    break;
            }
        }

        // Verify data integrity
        foreach (var kvp in expectedValues)
        {
            var actual = cache.Read(kvp.Key);
            if (actual != null) // Might have been evicted
            {
                actual.Should().Be(kvp.Value, $"Value for {kvp.Key} should match");
            }
        }
    }

    [Fact]
    public void Different_Value_Types_Should_Be_Stored_Correctly()
    {
        var cache = new CacheManager(maxItems: 20);

        // Various types
        cache.Create("string", "hello");
        cache.Create("int", 42);
        cache.Create("double", 3.14);
        cache.Create("bool", true);
        cache.Create("array", new[] { 1, 2, 3 });
        cache.Create("list", new List<string> { "a", "b", "c" });
        cache.Create("null", null);
        cache.Create("datetime", new DateTime(2024, 1, 1));
        cache.Create("guid", new Guid("12345678-1234-1234-1234-123456789012"));

        cache.Read("string").Should().Be("hello");
        cache.Read("int").Should().Be(42);
        cache.Read("double").Should().Be(3.14);
        cache.Read("bool").Should().Be(true);
        (cache.Read("array") as int[]).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        (cache.Read("list") as List<string>).Should().BeEquivalentTo(new List<string> { "a", "b", "c" });
        cache.Read("null").Should().BeNull();
        cache.Read("datetime").Should().Be(new DateTime(2024, 1, 1));
        cache.Read("guid").Should().Be(new Guid("12345678-1234-1234-1234-123456789012"));
    }

    [Fact]
    public void Should_Handle_Object_Reference_Correctly()
    {
        var cache = new CacheManager(maxItems: 10);
        var original = new TestMutableObject { Value = 1 };

        cache.Create("mutable", original);

        // Modify original object
        original.Value = 2;

        // Cached value should also reflect change (same reference)
        var retrieved = cache.Read("mutable") as TestMutableObject;
        retrieved!.Value.Should().Be(2);
    }

    #endregion

    #region Boundary Conditions

    [Fact]
    public void Should_Handle_Zero_Second_Expiration()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Create("instant", "value", expirationSeconds: 0);

        // Should immediately be considered expired (or nearly so)
        Thread.Sleep(100);
        cache.Read("instant").Should().BeNull();
    }

    [Fact]
    public void Should_Handle_Very_Long_Expiration()
    {
        var cache = new CacheManager(maxItems: 10);

        // Use a reasonable large value instead of int.MaxValue to avoid overflow
        cache.Create("longlife", "value", expirationSeconds: 86400 * 365); // 1 year
        cache.Read("longlife").Should().Be("value");
    }

    [Fact]
    public void Frequency_Should_Not_Overflow()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("hotkey", "value");

        // Read many times - frequency should not overflow
        for (int i = 0; i < 100000; i++)
        {
            cache.Read("hotkey");
        }

        cache.Read("hotkey").Should().Be("value");
    }

    [Fact]
    public void Should_Handle_Capacity_Boundary()
    {
        var cache = new CacheManager(maxItems: 5);

        // Fill exactly to capacity
        for (int i = 0; i < 5; i++)
        {
            cache.Create($"key{i}", $"value{i}").Should().BeTrue();
        }

        // All 5 should be readable
        for (int i = 0; i < 5; i++)
        {
            cache.Read($"key{i}").Should().Be($"value{i}");
        }

        // Adding 6th should evict one
        cache.Create("key5", "value5").Should().BeTrue();

        // Only 5 items should remain
        int count = 0;
        for (int i = 0; i <= 5; i++)
        {
            if (cache.Read($"key{i}") != null)
                count++;
        }

        count.Should().Be(5);
    }

    #endregion

    #region Error Handling

    [Fact]
    public void Read_Should_Handle_Null_Key_Gracefully()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Read(null!).Should().BeNull();
    }

    [Fact]
    public void Update_Should_Handle_Null_Key_Gracefully()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Update(null!, "value").Should().BeFalse();
    }

    [Fact]
    public void Delete_Should_Handle_Null_Key_Gracefully()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Delete(null!).Should().BeFalse();
    }

    [Fact]
    public void Should_Handle_Concurrent_Create_Of_Same_Key()
    {
        var cache = new CacheManager(maxItems: 10);
        var results = new bool[10];

        Parallel.For(0, 10, i =>
        {
            results[i] = cache.Create("samekey", $"value{i}");
        });

        // Only one should succeed
        results.Count(r => r).Should().Be(1);

        // Key should have a value
        cache.Read("samekey").Should().NotBeNull();
    }

    #endregion

    #region Sequence Tests

    [Fact]
    public void Create_Read_Update_Delete_Sequence_Should_Work()
    {
        var cache = new CacheManager(maxItems: 10);

        // Create
        cache.Create("key", "initial").Should().BeTrue();
        cache.Read("key").Should().Be("initial");

        // Update
        cache.Update("key", "updated").Should().BeTrue();
        cache.Read("key").Should().Be("updated");

        // Delete
        cache.Delete("key").Should().BeTrue();
        cache.Read("key").Should().BeNull();

        // Re-create
        cache.Create("key", "recreated").Should().BeTrue();
        cache.Read("key").Should().Be("recreated");
    }

    [Fact]
    public void Should_Track_Frequency_Correctly_Through_Operations()
    {
        var cache = new CacheManager(maxItems: 3);

        cache.Create("a", "1");
        cache.Create("b", "2");
        cache.Create("c", "3");

        // Read 'a' twice, 'b' once
        cache.Read("a"); // freq: 2
        cache.Read("a"); // freq: 3
        cache.Read("b"); // freq: 2

        // 'c' has lowest frequency (1), should be evicted
        cache.Create("d", "4");

        cache.Read("a").Should().NotBeNull(); // freq 3
        cache.Read("b").Should().NotBeNull(); // freq 2
        cache.Read("c").Should().BeNull();    // evicted
        cache.Read("d").Should().NotBeNull(); // freq 1
    }

    #endregion

    private class TestMutableObject
    {
        public int Value { get; set; }
    }
}
