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
        var request = new DataRequest(CacheOperation.Create, "user:1", "Rizwan", null);

        // Assert
        request.Operation.Should().Be(CacheOperation.Create);
        request.Key.Should().Be("user:1");
        request.Value.Should().Be("Rizwan");
    }

    [Fact]
    public void CacheRequest_Should_Allow_Null_Value()
    {
        // Arrange
        var request = new DataRequest(CacheOperation.Read, "user:1", null, null);

        // Assert
        request.Value.Should().BeNull();
    }

    [Fact]
    public void DataResponse_Should_Set_And_Get_Properties()
    {
        // Arrange
        var response = new DataResponse(42)
        {
            Success = true,
            Error = null
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Value.Should().Be(42);
        response.Error.Should().BeNull();
    }

    [Fact]
    public void ErrorResponse_Should_Hold_Error_Message_When_Failed()
    {
        // Arrange
        var response = new ErrorResponse("Invalid operation");

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Be("Invalid operation");
    }

    [Fact]
    public void DataResponse_Should_Allow_Any_Object_Type_As_Value()
    {
        // Arrange
        var payload = new { Id = 1, Name = "Test" };

        var response = new DataResponse(payload);

        // Assert
        response.Value.Should().Be(payload);
        response.Value.Should().BeOfType(payload.GetType());
    }
}
