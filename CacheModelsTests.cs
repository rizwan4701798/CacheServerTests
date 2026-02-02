using CacheServerModels;
using FluentAssertions;
using Xunit;

namespace CacheServer.Tests;

public class CacheModelsTests
{
    [Fact]
    public void CacheRequest_Should_Set_And_Get_Properties()
    {
        // Arrange
        var request = new CacheRequest
        {
            Operation = CacheOperation.Create,
            Key = "user:1",
            Value = "Rizwan"
        };

        // Assert
        request.Operation.Should().Be(CacheOperation.Create);
        request.Key.Should().Be("user:1");
        request.Value.Should().Be("Rizwan");
    }

    [Fact]
    public void CacheRequest_Should_Allow_Null_Value()
    {
        // Arrange
        var request = new CacheRequest
        {
            Operation = CacheOperation.Read,
            Key = "user:1",
            Value = null
        };

        // Assert
        request.Value.Should().BeNull();
    }

    [Fact]
    public void CacheResponse_Should_Set_And_Get_Properties()
    {
        // Arrange
        var response = new CacheResponse
        {
            Success = true,
            Value = 42,
            Error = null
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Value.Should().Be(42);
        response.Error.Should().BeNull();
    }

    [Fact]
    public void CacheResponse_Should_Hold_Error_Message_When_Failed()
    {
        // Arrange
        var response = new CacheResponse
        {
            Success = false,
            Error = "Invalid operation"
        };

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Be("Invalid operation");
        response.Value.Should().BeNull();
    }

    [Fact]
    public void CacheResponse_Should_Allow_Any_Object_Type_As_Value()
    {
        // Arrange
        var payload = new { Id = 1, Name = "Test" };

        var response = new CacheResponse
        {
            Success = true,
            Value = payload
        };

        // Assert
        response.Value.Should().Be(payload);
        response.Value.Should().BeOfType(payload.GetType());
    }
}
