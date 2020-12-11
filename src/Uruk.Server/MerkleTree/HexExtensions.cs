using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Uruk.Server
{
    public static class HexExtensions
    {
        public static string ByteToHex(this ReadOnlySpan<byte> bytes)
        {
            char[] c = new char[bytes.Length * 2];
            int b;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = bytes[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c);
        }

        public static string ByteToHex(this Span<byte> bytes)
        {
            return MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(bytes), bytes.Length).ByteToHex();
        }

        public static string ByteToHex(this byte[] bytes)
        {
            return new ReadOnlySpan<byte>(bytes).ByteToHex();
        }

        public static byte[] HexToByteArray(this string hexString, int minimalLength = 0)
        {
            byte[] bytes = new byte[Math.Max(hexString.Length / 2, minimalLength)];

            for (int i = 0; i < hexString.Length; i += 2)
            {
                string s = hexString.Substring(i, 2);
                bytes[i / 2] = byte.Parse(s, NumberStyles.HexNumber, null);
            }

            return bytes;
        }
    }
}