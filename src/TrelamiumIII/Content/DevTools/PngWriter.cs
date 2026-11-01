using System.IO;
using System.IO.Compression;
using System.Text;

namespace TrelamiumIII.Content.DevTools;

/// <summary> A minimal RGB PNG encoder for the harnesses' images, with no image library to depend on. </summary>
internal static class PngWriter
{
    public static void Write(string path, int width, int height, Func<int, int, Color> pixel)
    {
        if (width <= 0 || height <= 0)
            return;

        int stride = width * 3 + 1;
        byte[] raw = new byte[stride * height];

        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                Color color = pixel(px, py);
                int i = py * stride + 1 + px * 3;
                raw[i] = color.R;
                raw[i + 1] = color.G;
                raw[i + 2] = color.B;
            }
        }

        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(raw);

        byte[] header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8;
        header[9] = 2;

        using FileStream file = File.Create(path);
        file.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(file, "IHDR", header);
        WriteChunk(file, "IDAT", compressed.ToArray());
        WriteChunk(file, "IEND", []);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);

        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        uint crc = Crc32(Crc32(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        byte[] crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)crc);
        stream.Write(crcBytes);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(uint crc, byte[] data)
    {
        foreach (byte b in data)
        {
            crc ^= b;

            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc;
    }
}
