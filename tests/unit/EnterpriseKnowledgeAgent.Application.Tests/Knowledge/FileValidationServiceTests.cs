using System.Text;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Application.Tests.Knowledge;

/// <summary>TASK-KNW-02: unit tests for the pre-storage upload validation pipeline.</summary>
public sealed class FileValidationServiceTests
{
    private static FileValidationService CreateService(long maxFileSizeBytes = 10 * 1024 * 1024) =>
        new(Options.Create(new KnowledgeFileOptions { MaxFileSizeBytes = maxFileSizeBytes }));

    private static MemoryStream ToStream(string content) => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task ValidateAsync_accepts_a_plain_utf8_txt_file()
    {
        var service = CreateService();

        var result = await service.ValidateAsync("notes.txt", ToStream("Hello world"));

        result.IsValid.Should().BeTrue();
        result.SanitizedFileName.Should().Be("notes.txt");
        result.DetectedEncodingWebName.Should().Be("utf-8");
    }

    [Fact]
    public async Task ValidateAsync_accepts_a_markdown_file()
    {
        var service = CreateService();

        var result = await service.ValidateAsync("readme.md", ToStream("# Title\n\nBody"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("archive.zip")]
    [InlineData("photo.png")]
    [InlineData("noextension")]
    public async Task ValidateAsync_rejects_unsupported_extensions(string fileName)
    {
        var service = CreateService();

        var result = await service.ValidateAsync(fileName, ToStream("content"));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be(FileRejectionReason.UnsupportedExtension);
    }

    [Fact]
    public async Task ValidateAsync_rejects_empty_content()
    {
        var service = CreateService();

        var result = await service.ValidateAsync("notes.txt", new MemoryStream());

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be(FileRejectionReason.Empty);
    }

    [Fact]
    public async Task ValidateAsync_rejects_whitespace_only_content()
    {
        var service = CreateService();

        var result = await service.ValidateAsync("notes.txt", ToStream("   \n\t  "));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be(FileRejectionReason.Empty);
    }

    [Fact]
    public async Task ValidateAsync_rejects_content_exceeding_max_size()
    {
        var service = CreateService(maxFileSizeBytes: 5);

        var result = await service.ValidateAsync("notes.txt", ToStream("this is longer than five bytes"));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be(FileRejectionReason.TooLarge);
    }

    [Fact]
    public async Task ValidateAsync_rejects_binary_content_with_txt_extension()
    {
        var service = CreateService();
        var binary = new byte[100];
        Array.Fill(binary, (byte)0x00);

        var result = await service.ValidateAsync("notes.txt", new MemoryStream(binary));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be(FileRejectionReason.BinaryContent);
    }

    [Fact]
    public async Task ValidateAsync_rejects_invalid_utf8_byte_sequences()
    {
        var service = CreateService();
        // 0xC3 requires a continuation byte (0x80-0xBF); 0x28 ('(') is not one, so this is not valid UTF-8.
        // (Avoids the UTF-8/UTF-16 BOM byte patterns, which are detected as valid encodings before strict UTF-8 validation runs.)
        var invalidUtf8 = new byte[] { 0xC3, 0x28, 0x41, 0x42 };

        var result = await service.ValidateAsync("notes.txt", new MemoryStream(invalidUtf8));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be(FileRejectionReason.UnsupportedEncoding);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("..\\evil.txt")]
    [InlineData("dir/evil.txt")]
    [InlineData("dir\\evil.txt")]
    public async Task ValidateAsync_rejects_unsafe_file_names(string fileName)
    {
        var service = CreateService();

        var result = await service.ValidateAsync(fileName, ToStream("content"));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be(FileRejectionReason.UnsafeFileName);
    }
}
