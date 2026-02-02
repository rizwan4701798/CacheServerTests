using CacheServerModels;
using FluentAssertions;
using Manager;
using Moq;
using Xunit;
using CacheServer.Services;

namespace CacheServer.Tests;

public class RequestProcessorTests
{
    private readonly Mock<ICacheManager> _cacheManagerMock;
    private readonly RequestProcessor _processor;

    public RequestProcessorTests()
    {
        _cacheManagerMock = new Mock<ICacheManager>(MockBehavior.Strict);
        // EventNotifier setup might not be needed for pure request processing but good to keep if mocked
        var eventNotifierMock = new Mock<ICacheEventNotifier>();
        _cacheManagerMock.Setup(c => c.EventNotifier).Returns(eventNotifierMock.Object);

        _processor = new RequestProcessor(_cacheManagerMock.Object);
    }

    [Fact]
    public void Process_CREATE_Should_Call_Create_On_CacheManager()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Create("key1", "value1", null))
            .Returns(true);

        var request = new DataRequest(CacheOperation.Create, "key1", "value1", null);

        // Act
        var response = _processor.Process(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();

        _cacheManagerMock.Verify(
            c => c.Create("key1", "value1", null),
            Times.Once);
    }

    [Fact]
    public void Process_READ_Should_Return_Value_From_CacheManager()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Read("key1"))
            .Returns("value1");

        var request = new KeyRequest(CacheOperation.Read, "key1");

        // Act
        var response = _processor.Process(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Should().BeOfType<DataResponse>()
            .Which.Value.Should().Be("value1");

        _cacheManagerMock.Verify(c => c.Read("key1"), Times.Once);
    }

    [Fact]
    public void Process_UPDATE_Should_Call_Update_On_CacheManager()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Update("key1", "value2", null))
            .Returns(true);

        var request = new DataRequest(CacheOperation.Update, "key1", "value2", null);

        // Act
        var response = _processor.Process(request);

        // Assert
        response.Success.Should().BeTrue();

        _cacheManagerMock.Verify(
            c => c.Update("key1", "value2", null),
            Times.Once);
    }

    [Fact]
    public void Process_DELETE_Should_Call_Delete_On_CacheManager()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Delete("key1"))
            .Returns(true);

        var request = new KeyRequest(CacheOperation.Delete, "key1");

        // Act
        var response = _processor.Process(request);

        // Assert
        response.Success.Should().BeTrue();

        _cacheManagerMock.Verify(c => c.Delete("key1"), Times.Once);
    }

    [Fact]
    public void Process_Invalid_Operation_Should_Return_Error()
    {
        // Arrange
        var request = new KeyRequest((CacheOperation)999, "key1");

        // Act
        var response = _processor.Process(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Be("Invalid operation");
    }

    [Fact]
    public void Process_Should_Handle_Exception_Gracefully()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Create(It.IsAny<string>(), It.IsAny<object>(), default))
            .Throws(new InvalidOperationException("Boom"));

        var request = new DataRequest(CacheOperation.Create, "key1", "value1", null);

        // Act
        var response = _processor.Process(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Boom");
    }
}
