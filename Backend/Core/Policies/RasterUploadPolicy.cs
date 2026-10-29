using System.Buffers.Binary;
using Badminton.Core.Base;
namespace Badminton.Core.Policies;

public static class RasterUploadPolicy
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    public static string Validate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 || bytes.Length > MaximumBytes)
            throw Invalid();
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            if (bytes.Length < 45 || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8) || !bytes.Slice(bytes.Length - 8, 4).SequenceEqual("IEND"u8))
                throw Invalid();
            Dimensions(BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4)), BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4)));
            return "image/png";
        }
        if (bytes[0] != 255 || bytes[1] != 216 || bytes[^2] != 255 || bytes[^1] != 217)
            throw Invalid();
        var position = 2;
        while (position + 4 < bytes.Length)
        {
            if (bytes[position++] != 255)
                throw Invalid();
            while (position < bytes.Length && bytes[position] == 255)
                position++;
            if (position >= bytes.Length)
                throw Invalid();
            var marker = bytes[position++];
            if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7)
                continue;
            if (position + 2 > bytes.Length)
                throw Invalid();
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(position, 2));
            if (length < 2 || position + length > bytes.Length)
                throw Invalid();
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (length < 8)
                    throw Invalid();
                Dimensions(BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(position + 5, 2)), BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(position + 3, 2)));
                return "image/jpeg";
            }
            if (marker == 0xDA)
                throw Invalid();
            position += length;
        }
        throw Invalid();
    }
    private static void Dimensions(uint width, uint height)
    {
        if (width == 0 || height == 0 || width > 8192 || height > 8192 || (ulong)width * height > 32_000_000)
            throw Invalid();
    }
    private static BadRequestException Invalid() => new("Chỉ nhận PNG/JPEG tối đa 2 MB, 8192 px mỗi chiều và 32 megapixel; tệp phải có cấu trúc ảnh hợp lệ.");
}
