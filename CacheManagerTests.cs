using FluentAssertions;
using Manager;
using Xunit;

namespace CacheServer.Tests;

public class CacheManagerTests
{
    #region CRUD Operations

    [Fact]
    public void Create_Should_Add_Item()
    {
        var cache = new CacheManager(maxItems: 10);

        var result = cache.Create("key1", "value1");

        result.Should().BeTrue();
        cache.Read("key1").Should().Be("value1");
    }

    [Fact]
    public void Create_Should_Return_False_For_Duplicate_Key()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1");

        var result = cache.Create("key1", "value2");

        result.Should().BeFalse();
        cache.Read("key1").Should().Be("value1"); // Original value preserved
    }

    [Fact]
    public void Create_Should_Return_False_For_Empty_Key()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Create("", "value").Should().BeFalse();
        cache.Create("   ", "value").Should().BeFalse();
    }

    [Fact]
    public void Create_Should_Return_False_For_Null_Key()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Create(null!, "value").Should().BeFalse();
    }

    [Fact]
    public void Read_Should_Return_Null_For_Missing_Key()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Read("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Read_Should_Return_Null_For_Empty_Key()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Read("").Should().BeNull();
        cache.Read("   ").Should().BeNull();
    }

    [Fact]
    public void Update_Should_Modify_Existing_Value()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "original");

        var result = cache.Update("key1", "updated");

        result.Should().BeTrue();
        cache.Read("key1").Should().Be("updated");
    }

    [Fact]
    public void Update_Should_Return_False_For_Missing_Key()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Update("nonexistent", "value").Should().BeFalse();
    }

    [Fact]
    public void Update_Should_Return_False_For_Empty_Key()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Update("", "value").Should().BeFalse();
        cache.Update("   ", "value").Should().BeFalse();
    }

    [Fact]
    public void Delete_Should_Remove_Item()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1");

        var result = cache.Delete("key1");

        result.Should().BeTrue();
        cache.Read("key1").Should().BeNull();
    }

    [Fact]
    public void Delete_Should_Return_False_For_Missing_Key()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Delete("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void Delete_Should_Return_False_For_Empty_Key()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Delete("").Should().BeFalse();
        cache.Delete("   ").Should().BeFalse();
    }

    #endregion

    #region Expiration Tests

    [Fact]
    public void Create_With_Expiration_Should_Expire_After_Duration()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1", expirationSeconds: 1);

        cache.Read("key1").Should().Be("value1"); // Immediately accessible

        Thread.Sleep(1100); // Wait for expiration

        cache.Read("key1").Should().BeNull(); // Expired
    }

    [Fact]
    public void Update_Should_Fail_On_Expired_Item()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1", expirationSeconds: 1);

        Thread.Sleep(1100);

        cache.Update("key1", "newvalue").Should().BeFalse();
    }

    [Fact]
    public void Update_Should_Allow_Changing_Expiration()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1", expirationSeconds: 1);

        cache.Update("key1", "value2", expirationSeconds: 10);

        Thread.Sleep(1100);

        cache.Read("key1").Should().Be("value2"); // Still valid with new expiration
    }

    [Fact]
    public void Create_Without_Expiration_Should_Never_Expire()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1"); // No expiration

        Thread.Sleep(100);

        cache.Read("key1").Should().Be("value1");
    }

    #endregion

    #region LFU Eviction Tests

    [Fact]
    public void Should_Evict_Least_Frequently_Used_When_At_Capacity()
    {
        var cache = new CacheManager(maxItems: 3);

        cache.Create("key1", "value1");
        cache.Create("key2", "value2");
        cache.Create("key3", "value3");

        // Access key1 and key2 multiple times (increase frequency)
        cache.Read("key1");
        cache.Read("key1");
        cache.Read("key2");

        // Add new item - should evict key3 (least frequently used)
        cache.Create("key4", "value4");

        cache.Read("key1").Should().Be("value1");
        cache.Read("key2").Should().Be("value2");
        cache.Read("key3").Should().BeNull(); // Evicted
        cache.Read("key4").Should().Be("value4");
    }

    [Fact]
    public void Should_Evict_Oldest_Among_Same_Frequency()
    {
        var cache = new CacheManager(maxItems: 3);

        cache.Create("key1", "value1");
        Thread.Sleep(10);
        cache.Create("key2", "value2");
        Thread.Sleep(10);
        cache.Create("key3", "value3");

        // All have frequency 1, key1 is oldest
        cache.Create("key4", "value4");

        cache.Read("key1").Should().BeNull(); // Evicted (oldest with frequency 1)
        cache.Read("key2").Should().Be("value2");
        cache.Read("key3").Should().Be("value3");
        cache.Read("key4").Should().Be("value4");
    }

    [Fact]
    public void Frequency_Should_Increment_On_Read()
    {
        var cache = new CacheManager(maxItems: 2);

        cache.Create("key1", "value1");
        cache.Create("key2", "value2");

        // Read key1 multiple times
        cache.Read("key1");
        cache.Read("key1");
        cache.Read("key1");

        // Add new item - should evict key2 (lower frequency)
        cache.Create("key3", "value3");

        cache.Read("key1").Should().Be("value1"); // Kept (high frequency)
        cache.Read("key2").Should().BeNull(); // Evicted
    }

    #endregion

    #region Complex Object Tests

    [Fact]
    public void Should_Store_And_Retrieve_Complex_Objects()
    {
        var cache = new CacheManager(maxItems: 10);
        var complexObject = new TestObject { Name = "Test", Value = 123 };

        cache.Create("complex", complexObject);

        var retrieved = cache.Read("complex") as TestObject;
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test");
        retrieved.Value.Should().Be(123);
    }

    [Fact]
    public void Should_Store_Null_Values()
    {
        var cache = new CacheManager(maxItems: 10);

        cache.Create("nullkey", null).Should().BeTrue();
        cache.Read("nullkey").Should().BeNull();
    }

    [Fact]
    public void Should_Store_Different_Value_Types()
    {
        var cache = new CacheManager(maxItems: 20);

        cache.Create("string", "hello");
        cache.Create("int", 42);
        cache.Create("double", 3.14);
        cache.Create("bool", true);
        cache.Create("array", new[] { 1, 2, 3 });
        cache.Create("datetime", DateTime.Now);
        cache.Create("guid", Guid.NewGuid());

        cache.Read("string").Should().Be("hello");
        cache.Read("int").Should().Be(42);
        cache.Read("double").Should().Be(3.14);
        cache.Read("bool").Should().Be(true);
        (cache.Read("array") as int[]).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    #endregion

    #region Constructor Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_Should_Throw_For_Invalid_MaxItems(int maxItems)
    {
        Action act = () => new CacheManager(maxItems);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_Should_Accept_Valid_MaxItems()
    {
        var cache = new CacheManager(maxItems: 1);
        cache.Should().NotBeNull();
    }

    #endregion

    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
