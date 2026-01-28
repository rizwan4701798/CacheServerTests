using CacheServerModels;
using FluentAssertions;
using Manager;
using Moq;
using Xunit;
using CacheServer.Server;

namespace CacheServer.Tests;

public class CacheServerTests
{
    private readonly Mock<ICacheManager> _cacheManagerMock;
    private readonly CacheServer.Server.CacheServer _server;

    public CacheServerTests()
    {
        _cacheManagerMock = new Mock<ICacheManager>(MockBehavior.Strict);
        _server = new CacheServer.Server.CacheServer(port: 5050, cacheManager: _cacheManagerMock.Object);
    }

    [Fact]
    public void ProcessRequest_CREATE_Should_Call_Create_On_CacheManager()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Create("key1", "value1", null))
            .Returns(true);

        var request = new CacheRequest
        {
            Operation = "CREATE",
            Key = "key1",
            Value = "value1"
        };

        // Act
        var response = _server.ProcessRequest(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Error.Should().BeNull();

        _cacheManagerMock.Verify(
            c => c.Create("key1", "value1", null),
            Times.Once);
    }

    [Fact]
    public void ProcessRequest_READ_Should_Return_Value_From_CacheManager()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Read("key1"))
            .Returns("value1");

        var request = new CacheRequest
        {
            Operation = "READ",
            Key = "key1"
        };

        // Act
        var response = _server.ProcessRequest(request);

        // Assert
        response.Success.Should().BeTrue();
        response.Value.Should().Be("value1");

        _cacheManagerMock.Verify(c => c.Read("key1"), Times.Once);
    }

    [Fact]
    public void ProcessRequest_UPDATE_Should_Call_Update_On_CacheManager()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Update("key1", "value2", null))
            .Returns(true);

        var request = new CacheRequest
        {
            Operation = "UPDATE",
            Key = "key1",
            Value = "value2"
        };

        // Act
        var response = _server.ProcessRequest(request);

        // Assert
        response.Success.Should().BeTrue();

        _cacheManagerMock.Verify(
            c => c.Update("key1", "value2", null),
            Times.Once);
    }

    [Fact]
    public void ProcessRequest_DELETE_Should_Call_Delete_On_CacheManager()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Delete("key1"))
            .Returns(true);

        var request = new CacheRequest
        {
            Operation = "DELETE",
            Key = "key1"
        };

        // Act
        var response = _server.ProcessRequest(request);

        // Assert
        response.Success.Should().BeTrue();

        _cacheManagerMock.Verify(c => c.Delete("key1"), Times.Once);
    }

    [Fact]
    public void ProcessRequest_Invalid_Operation_Should_Return_Error()
    {
        // Arrange
        var request = new CacheRequest
        {
            Operation = "INVALID",
            Key = "key1"
        };

        // Act
        var response = _server.ProcessRequest(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Be("Invalid operation");
    }

    [Fact]
    public void ProcessRequest_Should_Handle_Exception_Gracefully()
    {
        // Arrange
        _cacheManagerMock
            .Setup(c => c.Create(It.IsAny<string>(), It.IsAny<object>(), default))
            .Throws(new InvalidOperationException("Boom"));

        var request = new CacheRequest
        {
            Operation = "CREATE",
            Key = "key1",
            Value = "value1"
        };

        // Act
        var response = _server.ProcessRequest(request);

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Contain("Boom");
    }
}
