using FluentAssertions;
using Velo.Domain;
using Xunit;

namespace Velo.UnitTests;

public class SecurityInputValidationTests
{
    [Theory]
    [InlineData("../../windows/system32/cmd.exe", "cmd.exe")]
    [InlineData("..\\..\\boot.ini", "boot.ini")]
    [InlineData("file<>:\"|?*.zip", "file_______.zip")]
    [InlineData("CON", "_CON")]
    [InlineData("NUL.txt", "_NUL.txt")]
    [InlineData("AUX.tar.gz", "_AUX.tar.gz")]
    [InlineData("COM1", "_COM1")]
    [InlineData("LPT2.bin", "_LPT2.bin")]
    [InlineData("trailing_dots....", "trailing_dots")]
    [InlineData("trailing_spaces   ", "trailing_spaces")]
    public void SanitizeFileName_StripsTraversalAndIllegalChars(string input, string expected)
    {
        var result = SafePathValidator.SanitizeFileName(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void CombineSafePath_ConfinesOutputToDestinationDirectory()
    {
        string baseDir = "C:\\Downloads\\Velo";
        string maliciousFile = "../../windows/system32/evil.exe";

        string resolved = SafePathValidator.CombineSafePath(baseDir, maliciousFile);

        resolved.Should().StartWith(baseDir);
        resolved.Should().Be("C:\\Downloads\\Velo\\evil.exe");
    }

    [Theory]
    [InlineData("http://example.com/test.zip", true)]
    [InlineData("https://example.com/test.zip", true)]
    [InlineData("file:///C:/Windows/System32/cmd.exe", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<html>malicious</html>", false)]
    [InlineData("gopher://old.internet", false)]
    public void ValidateUrlScheme_OnlyAllowsHttpAndHttps(string url, bool expectedValid)
    {
        bool isValid = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        isValid.Should().Be(expectedValid);
    }
}
