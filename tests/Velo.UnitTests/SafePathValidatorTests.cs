using FluentAssertions;
using Velo.Domain;
using Xunit;

namespace Velo.UnitTests;

public class SafePathValidatorTests
{
    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system32\\calc.exe", "calc.exe")]
    [InlineData("normal_file.zip", "normal_file.zip")]
    [InlineData("file?with*illegal:chars.mp4", "file_with_illegal_chars.mp4")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("NUL", "_NUL")]
    [InlineData("COM1.dat", "_COM1.dat")]
    public void SanitizeFileName_ShouldSanitizeDangerousInput(string rawInput, string expected)
    {
        string actual = SafePathValidator.SanitizeFileName(rawInput);
        actual.Should().Be(expected);
    }

    [Fact]
    public void SanitizeFileName_GivenEmptyInput_ShouldReturnFallback()
    {
        string actual = SafePathValidator.SanitizeFileName("", "default.bin");
        actual.Should().Be("default.bin");
    }
}
