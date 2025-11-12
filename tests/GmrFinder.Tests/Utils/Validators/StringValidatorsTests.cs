using FluentAssertions;
using GmrFinder.Utils.Validators;
using Xunit;

namespace GmrFinder.Tests.Utils.Validators;

public class StringValidatorsTests
{
    private readonly IStringValidators _stringValidators = new StringValidators();

    [Theory]
    [InlineData("25GB6RLA6C8OV8GAR2")]
    [InlineData("12FRABCDEFGH123456")]
    public void IsValidMrn_WithValidValue_ReturnsTrue(string mrn)
    {
        _stringValidators.IsValidMrn(mrn).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("25GB6RLA6C8OV8GAR")]
    [InlineData("251BG6RLA6C8OV8GAR2")]
    [InlineData("25GB6RLA6C8OV8GAR2XX")]
    public void IsValidMrn_WithInvalidValue_ReturnsFalse(string mrn)
    {
        _stringValidators.IsValidMrn(mrn).Should().BeFalse();
    }
}
