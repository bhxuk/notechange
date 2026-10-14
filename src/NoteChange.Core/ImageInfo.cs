namespace NoteChange.Core;

/// <summary>Reads the type and pixel size of PNG and JPEG pictures without decoding them.</summary>
public static class ImageInfo
{
    public const int MaxBytes = 8 * 1024 * 1024;

    public static bool TryRead(byte[] data, out string format, out int width, out int height)
    {
        format = "";
        width = height = 0;
        if (data == null || data.Length < 24) return false;

        // PNG: signature, then the IHDR chunk holds width and height.
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            width = BigEndian(data, 16);
            height = BigEndian(data, 20);
            format = "png";
            return width > 0 && height > 0;
        }

        // JPEG: walk the markers until a start-of-frame.
        if (data[0] == 0xFF && data[1] == 0xD8)
        {
            int i = 2;
            while (i + 9 < data.Length)
            {
                if (data[i] != 0xFF) { i++; continue; }
                byte marker = data[i + 1];
                if (marker == 0xFF) { i++; continue; }
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
                int length = (data[i + 2] << 8) | data[i + 3];
                bool startOfFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (startOfFrame)
                {
                    height = (data[i + 5] << 8) | data[i + 6];
                    width = (data[i + 7] << 8) | data[i + 8];
                    format = "jpeg";
                    return width > 0 && height > 0;
                }
                if (length < 2) return false;
                i += 2 + length;
            }
        }
        return false;
    }

    /// <summary>Turns a data: URI into a picture, or returns null if it isn't a usable PNG or JPEG.</summary>
    public static NoteImage? FromDataUri(string? uri, string? alt)
    {
        if (string.IsNullOrEmpty(uri)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(uri, @"^data:image/(png|jpe?g);base64,(.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!m.Success) return null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(m.Groups[2].Value.Trim()); }
        catch (FormatException) { return null; }
        if (bytes.Length > MaxBytes) return null;
        if (!TryRead(bytes, out var format, out var w, out var h)) return null;
        return new NoteImage
        {
            Data = bytes,
            Format = format,
            Width = w,
            Height = h,
            Alt = string.IsNullOrWhiteSpace(alt) ? "Picture" : alt.Trim()
        };
    }

    private static int BigEndian(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];
}
