using FluentAssertions;
using Velo.Domain;
using Xunit;

namespace Velo.UnitTests;

public class FileCategoryClassifierTests
{
    [Theory]
    [InlineData("archive.zip", CategoryType.Compressed)]
    [InlineData("backup.tar.gz", CategoryType.Compressed)]
    [InlineData("windows_setup.iso", CategoryType.Compressed)]
    [InlineData("movie.mp4", CategoryType.Video)]
    [InlineData("stream.mkv", CategoryType.Video)]
    [InlineData("podcast.mp3", CategoryType.Music)]
    [InlineData("song.flac", CategoryType.Music)]
    [InlineData("report.pdf", CategoryType.Documents)]
    [InlineData("notes.docx", CategoryType.Documents)]
    [InlineData("setup.exe", CategoryType.Programs)]
    [InlineData("installer.msi", CategoryType.Programs)]
    [InlineData("random_payload.xyz", CategoryType.Others)]
    public void Classify_ByFileName_ShouldReturnExpectedCategory(string fileName, CategoryType expected)
    {
        var category = FileCategoryClassifier.Classify(fileName);
        category.Should().Be(expected);
    }

    [Theory]
    [InlineData("video/mp4", CategoryType.Video)]
    [InlineData("audio/mpeg", CategoryType.Music)]
    [InlineData("application/zip", CategoryType.Compressed)]
    [InlineData("application/pdf", CategoryType.Documents)]
    public void Classify_ByContentType_ShouldReturnExpectedCategory(string contentType, CategoryType expected)
    {
        var category = FileCategoryClassifier.Classify("unknown_file", contentType);
        category.Should().Be(expected);
    }

    [Fact]
    public void ResolveCategoryFolder_ShouldAppendCategorySubfolder()
    {
        string baseDir = "C:\\Users\\User\\Downloads";
        string videoDir = FileCategoryClassifier.ResolveCategoryFolder(baseDir, CategoryType.Video);
        string othersDir = FileCategoryClassifier.ResolveCategoryFolder(baseDir, CategoryType.Others);

        videoDir.Should().Be("C:\\Users\\User\\Downloads\\Video");
        othersDir.Should().Be(baseDir, "Others category should default to root download directory");
    }
}
