using FluentAssertions;
using LexPCImages.Shared.Common;
using LexPCImages.Shared.Common.Errors;

namespace LexPCImages.UnitTests.Shared;

public sealed class ResultTests
{
    [Fact]
    public void Success_creates_result_with_value_and_no_error()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_creates_result_with_error_and_no_value()
    {
        var error = Error.NotFound("pc.not_found", "PC not found");
        var result = Result<int>.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default);
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Implicit_conversion_from_value_creates_success()
    {
        Result<int> result = 7;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(7);
    }

    [Fact]
    public void Implicit_conversion_from_error_creates_failure()
    {
        var error = Error.Validation("bad", "bad input");
        Result<int> result = error;

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Non_generic_Success_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Non_generic_Failure_carries_error()
    {
        var result = Result.Failure(ErrorType.Conflict, "conflict", "duplicate");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("conflict");
        result.Error.Message.Should().Be("duplicate");
    }

    [Fact]
    public void Error_factory_methods_set_the_correct_type()
    {
        Error.Validation("v", "m").Type.Should().Be(ErrorType.Validation);
        Error.NotFound("n", "m").Type.Should().Be(ErrorType.NotFound);
        Error.Conflict("c", "m").Type.Should().Be(ErrorType.Conflict);
        Error.Unauthorized("u", "m").Type.Should().Be(ErrorType.Unauthorized);
        Error.Forbidden("f", "m").Type.Should().Be(ErrorType.Forbidden);
        Error.DependencyFailure("d", "m").Type.Should().Be(ErrorType.DependencyFailure);
        Error.Internal("i", "m").Type.Should().Be(ErrorType.Internal);
    }
}
