using FluentAssertions;
using Manager;
using CacheServerModels;
using Xunit;

namespace CacheServer.Tests;

public class CacheEventNotificationTests
{
    #region ItemAdded Event Tests

    [Fact]
    public void Should_Raise_ItemAdded_Event_On_Create()
    {
        var cache = new CacheManager(maxItems: 10);
        CacheEvent? receivedEvent = null;

        cache.EventNotifier.CacheEventOccurred += (sender, e) => receivedEvent = e;

        cache.Create("key1", "value1");

        receivedEvent.Should().NotBeNull();
        receivedEvent!.EventType.Should().Be(CacheEventType.ItemAdded);
        receivedEvent.Key.Should().Be("key1");
        receivedEvent.Value.Should().Be("value1");
        receivedEvent.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Should_Not_Raise_ItemAdded_Event_On_Failed_Create()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1"); // First create succeeds

        var eventCount = 0;
        cache.EventNotifier.CacheEventOccurred += (sender, e) =>
        {
            if (e.EventType == CacheEventType.ItemAdded)
                eventCount++;
        };

        cache.Create("key1", "value2"); // Duplicate - should fail

        eventCount.Should().Be(0);
    }

    [Fact]
    public void Should_Include_Value_In_ItemAdded_Event()
    {
        var cache = new CacheManager(maxItems: 10);
        var complexValue = new { Name = "Test", Id = 123 };
        CacheEvent? receivedEvent = null;

        cache.EventNotifier.CacheEventOccurred += (sender, e) => receivedEvent = e;

        cache.Create("complex", complexValue);

        receivedEvent.Should().NotBeNull();
        receivedEvent!.Value.Should().BeEquivalentTo(complexValue);
    }

    #endregion

    #region ItemUpdated Event Tests

    [Fact]
    public void Should_Raise_ItemUpdated_Event_On_Update()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1");

        CacheEvent? receivedEvent = null;
        cache.EventNotifier.CacheEventOccurred += (sender, e) => receivedEvent = e;

        cache.Update("key1", "value2");

        receivedEvent.Should().NotBeNull();
        receivedEvent!.EventType.Should().Be(CacheEventType.ItemUpdated);
        receivedEvent.Key.Should().Be("key1");
        receivedEvent.Value.Should().Be("value2");
    }

    [Fact]
    public void Should_Not_Raise_ItemUpdated_Event_On_Failed_Update()
    {
        var cache = new CacheManager(maxItems: 10);

        var eventCount = 0;
        cache.EventNotifier.CacheEventOccurred += (sender, e) =>
        {
            if (e.EventType == CacheEventType.ItemUpdated)
                eventCount++;
        };

        cache.Update("nonexistent", "value"); // Key doesn't exist

        eventCount.Should().Be(0);
    }

    #endregion

    #region ItemRemoved Event Tests

    [Fact]
    public void Should_Raise_ItemRemoved_Event_On_Delete()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1");

        CacheEvent? receivedEvent = null;
        cache.EventNotifier.CacheEventOccurred += (sender, e) => receivedEvent = e;

        cache.Delete("key1");

        receivedEvent.Should().NotBeNull();
        receivedEvent!.EventType.Should().Be(CacheEventType.ItemRemoved);
        receivedEvent.Key.Should().Be("key1");
    }

    [Fact]
    public void Should_Not_Raise_ItemRemoved_Event_On_Failed_Delete()
    {
        var cache = new CacheManager(maxItems: 10);

        var eventCount = 0;
        cache.EventNotifier.CacheEventOccurred += (sender, e) =>
        {
            if (e.EventType == CacheEventType.ItemRemoved)
                eventCount++;
        };

        cache.Delete("nonexistent");

        eventCount.Should().Be(0);
    }

    #endregion

    #region ItemEvicted Event Tests

    [Fact]
    public void Should_Raise_ItemEvicted_Event_On_LFU_Eviction()
    {
        var cache = new CacheManager(maxItems: 2);
        var events = new List<CacheEvent>();

        cache.EventNotifier.CacheEventOccurred += (sender, e) => events.Add(e);

        cache.Create("key1", "value1");
        cache.Create("key2", "value2");
        cache.Read("key1"); // Increase frequency of key1
        cache.Create("key3", "value3"); // Should evict key2

        var evictedEvent = events.FirstOrDefault(e => e.EventType == CacheEventType.ItemEvicted);
        evictedEvent.Should().NotBeNull();
        evictedEvent!.Key.Should().Be("key2");
        evictedEvent.Reason.Should().Contain("LFU");
    }

    [Fact]
    public void Should_Include_Reason_In_ItemEvicted_Event()
    {
        var cache = new CacheManager(maxItems: 1);
        CacheEvent? evictedEvent = null;

        cache.Create("key1", "value1");

        cache.EventNotifier.CacheEventOccurred += (sender, e) =>
        {
            if (e.EventType == CacheEventType.ItemEvicted)
                evictedEvent = e;
        };

        cache.Create("key2", "value2"); // Should evict key1

        evictedEvent.Should().NotBeNull();
        evictedEvent!.Reason.Should().NotBeNullOrEmpty();
        evictedEvent.Reason.Should().Contain("frequency");
    }

    #endregion

    #region ItemExpired Event Tests

    [Fact]
    public void Should_Raise_ItemExpired_Event_On_Read_Of_Expired_Item()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1", expirationSeconds: 1);

        CacheEvent? receivedEvent = null;
        cache.EventNotifier.CacheEventOccurred += (sender, e) =>
        {
            if (e.EventType == CacheEventType.ItemExpired)
                receivedEvent = e;
        };

        Thread.Sleep(1100);
        cache.Read("key1"); // Triggers expiration check

        receivedEvent.Should().NotBeNull();
        receivedEvent!.EventType.Should().Be(CacheEventType.ItemExpired);
        receivedEvent.Key.Should().Be("key1");
    }

    [Fact]
    public void Should_Raise_ItemExpired_Event_On_Update_Of_Expired_Item()
    {
        var cache = new CacheManager(maxItems: 10);
        cache.Create("key1", "value1", expirationSeconds: 1);

        CacheEvent? receivedEvent = null;
        cache.EventNotifier.CacheEventOccurred += (sender, e) =>
        {
            if (e.EventType == CacheEventType.ItemExpired)
                receivedEvent = e;
        };

        Thread.Sleep(1100);
        cache.Update("key1", "newvalue"); // Triggers expiration check

        receivedEvent.Should().NotBeNull();
        receivedEvent!.Key.Should().Be("key1");
    }

    #endregion

    #region Multiple Events Tests

    [Fact]
    public void Should_Raise_Multiple_Events_In_Sequence()
    {
        var cache = new CacheManager(maxItems: 10);
        var events = new List<CacheEvent>();

        cache.EventNotifier.CacheEventOccurred += (sender, e) => events.Add(e);

        cache.Create("key1", "value1");
        cache.Update("key1", "value2");
        cache.Delete("key1");

        events.Should().HaveCount(3);
        events[0].EventType.Should().Be(CacheEventType.ItemAdded);
        events[1].EventType.Should().Be(CacheEventType.ItemUpdated);
        events[2].EventType.Should().Be(CacheEventType.ItemRemoved);
    }

    [Fact]
    public void Should_Handle_Multiple_Event_Handlers()
    {
        var cache = new CacheManager(maxItems: 10);
        var handler1Count = 0;
        var handler2Count = 0;

        cache.EventNotifier.CacheEventOccurred += (sender, e) => handler1Count++;
        cache.EventNotifier.CacheEventOccurred += (sender, e) => handler2Count++;

        cache.Create("key1", "value1");

        handler1Count.Should().Be(1);
        handler2Count.Should().Be(1);
    }

    [Fact]
    public void Events_Should_Be_Raised_In_Order()
    {
        var cache = new CacheManager(maxItems: 3);
        var events = new List<(CacheEventType Type, string Key)>();

        cache.EventNotifier.CacheEventOccurred += (sender, e) =>
            events.Add((e.EventType, e.Key));

        cache.Create("a", "1");
        cache.Create("b", "2");
        cache.Read("a"); // Increase frequency
        cache.Create("c", "3");
        cache.Create("d", "4"); // Should evict 'b'

        // Verify sequence
        events[0].Should().Be((CacheEventType.ItemAdded, "a"));
        events[1].Should().Be((CacheEventType.ItemAdded, "b"));
        events[2].Should().Be((CacheEventType.ItemAdded, "c"));

        // Find eviction event
        events.Should().Contain(e => e.Type == CacheEventType.ItemEvicted && e.Key == "b");
        events.Should().Contain(e => e.Type == CacheEventType.ItemAdded && e.Key == "d");
    }

    #endregion

    #region Event Timing Tests

    [Fact]
    public void Event_Timestamp_Should_Be_Close_To_Operation_Time()
    {
        var cache = new CacheManager(maxItems: 10);
        CacheEvent? receivedEvent = null;

        cache.EventNotifier.CacheEventOccurred += (sender, e) => receivedEvent = e;

        var beforeCreate = DateTime.UtcNow;
        cache.Create("key1", "value1");
        var afterCreate = DateTime.UtcNow;

        receivedEvent.Should().NotBeNull();
        receivedEvent!.Timestamp.Should().BeOnOrAfter(beforeCreate);
        receivedEvent.Timestamp.Should().BeOnOrBefore(afterCreate.AddMilliseconds(100));
    }

    #endregion

    #region Thread Safety of Events

    [Fact]
    public void Events_Should_Be_Thread_Safe()
    {
        var cache = new CacheManager(maxItems: 1000);
        var eventCount = 0;
        var lockObj = new object();

        cache.EventNotifier.CacheEventOccurred += (sender, e) =>
        {
            lock (lockObj)
            {
                eventCount++;
            }
        };

        Parallel.For(0, 100, i =>
        {
            cache.Create($"key{i}", $"value{i}");
        });

        eventCount.Should().Be(100);
    }

    #endregion
}
