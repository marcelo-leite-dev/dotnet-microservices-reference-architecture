using BuildingBlocks.Domain;

namespace BuildingBlocks.UnitTests;

public sealed class ResultTests
{
    private static readonly Error SomeError = Error.Conflict("Test.Conflict", "Something conflicted.");

    [Fact]
    public void ImplicitConversionFromValue_CreatesSuccess()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ImplicitConversionFromError_CreatesFailure()
    {
        Result<int> result = SomeError;

        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.Error);
    }

    [Fact]
    public void Value_OfFailedResult_Throws()
    {
        Result<int> result = SomeError;

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_WithNoneError_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }
}
