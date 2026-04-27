using AuditLogService.Application.Logging;
using Shouldly;
using Xunit;

namespace AuditLogService.Application.Tests.Logging;

public sealed class MaskingPiiRedactorTests
{
    private readonly MaskingPiiRedactor _redactor = new();

    [Theory]
    [InlineData("alice@example.com", "a***@e***")]
    [InlineData("bob@a.io", "b***@a***")]
    [InlineData("user1", "u***")]
    [InlineData("x", "***")]
    public void RedactActor_MasksAsExpected(string input, string expected)
    {
        _redactor.RedactActor(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RedactActor_BlankReturnsStars(string? input)
    {
        _redactor.RedactActor(input).ShouldBe("***");
    }
}
