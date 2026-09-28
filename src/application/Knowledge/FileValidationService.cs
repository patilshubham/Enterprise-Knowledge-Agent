using System.Text;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>
/// TASK-KNW-02: pre-storage validation for candidate `.txt`/`.md` uploads -
/// extension, size, non-empty, encoding, binary-content detection, and safe
/// filename (FR-KNW-001, FR-KNW-003, FR-SEC-003). Streaming-friendly: the
/// caller supplies a seekable stream (buffered multipart section) so this
/// validator can rewind after sampling bytes rather than loading the whole
/// file twice.
/// </summary>
public sealed class FileValidationService(IOptions<KnowledgeFileOptions> options)
{
    private static readonly string[] AllowedExtensions = [".txt", ".md"];

    /// <summary>Bytes sampled from the start of the file for binary-content detection - enough to catch a mis-renamed binary without reading huge files fully.</summary>
    private const int BinarySampleSize = 8000;

    public async Task<FileValidationResult> ValidateAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var sanitizedName = SanitizeFileName(fileName);
        if (sanitizedName is null)
        {
            return FileValidationResult.Invalid(FileRejectionReason.UnsafeFileName);
        }

        var extension = Path.GetExtension(sanitizedName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return FileValidationResult.Invalid(FileRejectionReason.UnsupportedExtension);
        }

        if (content.Length == 0)
        {
            return FileValidationResult.Invalid(FileRejectionReason.Empty);
        }

        if (content.Length > options.Value.MaxFileSizeBytes)
        {
            return FileValidationResult.Invalid(FileRejectionReason.TooLarge);
        }

        content.Position = 0;
        var buffer = new byte[content.Length];
        await content.ReadExactlyAsync(buffer, cancellationToken);
        content.Position = 0;

        var sampleLength = Math.Min(buffer.Length, BinarySampleSize);
        if (LooksBinary(buffer.AsSpan(0, sampleLength)))
        {
            return FileValidationResult.Invalid(FileRejectionReason.BinaryContent);
        }

        if (!TryDetectEncoding(buffer, out var encoding))
        {
            return FileValidationResult.Invalid(FileRejectionReason.UnsupportedEncoding);
        }

        var text = encoding.GetString(buffer).TrimStart('\uFEFF');
        if (string.IsNullOrWhiteSpace(text))
        {
            return FileValidationResult.Invalid(FileRejectionReason.Empty);
        }

        return FileValidationResult.Valid(sanitizedName, encoding.WebName);
    }

    /// <summary>Strips any directory component and rejects traversal/control-character/empty names (FR-SEC-003).</summary>
    internal static string? SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        // Reject before stripping directories: a name embedding a traversal
        // segment or separator is itself suspicious, even though
        // Path.GetFileName below would already strip it.
        if (fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal)
            || fileName.Any(char.IsControl))
        {
            return null;
        }

        var name = Path.GetFileName(fileName).Trim();
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        return name;
    }

    /// <summary>Heuristic binary-content detection: a NUL byte, or a high proportion of non-printable control bytes, indicates a mis-renamed binary despite a `.txt`/`.md` extension.</summary>
    private static bool LooksBinary(ReadOnlySpan<byte> sample)
    {
        if (sample.IsEmpty)
        {
            return false;
        }

        var suspiciousCount = 0;
        foreach (var b in sample)
        {
            if (b == 0)
            {
                return true; // NUL byte never appears in valid text content.
            }

            var isAllowedControl = b is 9 or 10 or 13; // tab, LF, CR
            if (b < 0x20 && !isAllowedControl)
            {
                suspiciousCount++;
            }
        }

        // Conservative threshold (>1%) to avoid rejecting valid UTF text
        // that happens to contain a rare control character.
        return suspiciousCount * 100 > sample.Length;
    }

    /// <summary>Detects UTF-8 (with/without BOM) or UTF-16 (LE/BE, via BOM) content; anything else (or invalid UTF-8 byte sequences) is unsupported.</summary>
    private static bool TryDetectEncoding(byte[] buffer, out Encoding encoding)
    {
        if (buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return true;
        }

        if (buffer.Length >= 2 && buffer[0] == 0xFF && buffer[1] == 0xFE)
        {
            encoding = Encoding.Unicode; // UTF-16 LE
            return true;
        }

        if (buffer.Length >= 2 && buffer[0] == 0xFE && buffer[1] == 0xFF)
        {
            encoding = Encoding.BigEndianUnicode; // UTF-16 BE
            return true;
        }

        // No BOM: require strictly valid UTF-8 (covers plain ASCII too).
        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            strictUtf8.GetString(buffer);
            encoding = strictUtf8;
            return true;
        }
        catch (DecoderFallbackException)
        {
            encoding = Encoding.UTF8;
            return false;
        }
    }
}
