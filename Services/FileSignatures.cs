namespace MyFirstApi.Services;

// Detects image types from the first bytes of a file ("magic numbers"), so a
// renamed executable can't be uploaded as signature.png.
public static class FileSignatures
{
    public record ImageType(string ContentType, string Extension);

    public static readonly ImageType Png = new("image/png", "png");
    public static readonly ImageType Jpeg = new("image/jpeg", "jpg");
    public static readonly ImageType Webp = new("image/webp", "webp");

    public static async Task<ImageType?> DetectImageAsync(Stream stream)
    {
        var header = new byte[12];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        stream.Position = 0;
        if (read < 12) return null;

        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return Png;
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return Jpeg;
        if (header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
            header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P') return Webp;
        return null;
    }
}
