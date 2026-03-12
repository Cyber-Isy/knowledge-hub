namespace KnowledgeHub.API.Validators;

public static class FileSignatureValidator
{
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04]; // PK.. (DOCX is ZIP-based)

    public static bool IsValidFileSignature(Stream fileStream, string extension)
    {
        if (fileStream.Length == 0)
            return false;

        var originalPosition = fileStream.Position;
        fileStream.Position = 0;

        try
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => HasSignature(fileStream, PdfSignature),
                ".docx" => HasSignature(fileStream, ZipSignature),
                ".txt" or ".md" => IsValidUtf8Text(fileStream),
                _ => false
            };
        }
        finally
        {
            fileStream.Position = originalPosition;
        }
    }

    private static bool HasSignature(Stream stream, byte[] signature)
    {
        if (stream.Length < signature.Length)
            return false;

        var buffer = new byte[signature.Length];
        var bytesRead = stream.Read(buffer, 0, signature.Length);

        if (bytesRead < signature.Length)
            return false;

        return buffer.AsSpan().SequenceEqual(signature);
    }

    private static bool IsValidUtf8Text(Stream stream)
    {
        // Read up to 8KB to check for valid UTF-8 text content
        var bufferSize = (int)Math.Min(stream.Length, 8192);
        var buffer = new byte[bufferSize];
        var bytesRead = stream.Read(buffer, 0, bufferSize);

        if (bytesRead == 0)
            return false;

        // Check for null bytes (binary file indicator)
        for (var i = 0; i < bytesRead; i++)
        {
            if (buffer[i] == 0)
                return false;
        }

        // Attempt UTF-8 decoding
        try
        {
            System.Text.Encoding.UTF8.GetString(buffer.AsSpan(0, bytesRead));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
