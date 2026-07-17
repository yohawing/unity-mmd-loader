#nullable enable

using System;

namespace Mmd.UnityIntegration
{
    internal static class MmdJpegHeaderValidator
    {
        internal static void Validate(byte[] bytes, MmdTextureDecodeBudget budget)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            budget.ValidateInputLength(bytes.LongLength);
            if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8)
            {
                throw new ArgumentException("JPEG data must start with an SOI marker.", nameof(bytes));
            }

            int cursor = 2;
            bool foundFrame = false;
            while (cursor < bytes.Length)
            {
                if (bytes[cursor++] != 0xff)
                {
                    throw new ArgumentException("JPEG marker prefix is missing.", nameof(bytes));
                }

                while (cursor < bytes.Length && bytes[cursor] == 0xff) cursor++;
                if (cursor >= bytes.Length) throw new ArgumentException("JPEG marker is truncated.", nameof(bytes));
                byte marker = bytes[cursor++];
                if (marker == 0xd9) break;
                if (marker == 0xda)
                {
                    if (!foundFrame) throw new ArgumentException("JPEG scan precedes its frame header.", nameof(bytes));
                    return;
                }

                if (marker == 0xd8 || marker == 0x01 || (marker >= 0xd0 && marker <= 0xd7))
                {
                    continue;
                }

                if (cursor + 2 > bytes.Length) throw new ArgumentException("JPEG segment length is truncated.", nameof(bytes));
                int segmentLength = (bytes[cursor] << 8) | bytes[cursor + 1];
                if (segmentLength < 2 || segmentLength > bytes.Length - cursor)
                {
                    throw new ArgumentException("JPEG segment exceeds the file length.", nameof(bytes));
                }

                if (IsStartOfFrame(marker))
                {
                    if (foundFrame) throw new ArgumentException("JPEG contains more than one frame header.", nameof(bytes));
                    if (segmentLength < 8) throw new ArgumentException("JPEG frame header is truncated.", nameof(bytes));
                    int height = (bytes[cursor + 3] << 8) | bytes[cursor + 4];
                    int width = (bytes[cursor + 5] << 8) | bytes[cursor + 6];
                    int componentCount = bytes[cursor + 7];
                    if (componentCount <= 0 || segmentLength < 8 + componentCount * 3)
                    {
                        throw new ArgumentException("JPEG frame component table is truncated.", nameof(bytes));
                    }

                    budget.ValidateImageAndGetPixelCount(width, height);
                    foundFrame = true;
                }

                cursor += segmentLength;
            }

            if (!foundFrame)
            {
                throw new ArgumentException("JPEG frame header is missing.", nameof(bytes));
            }
        }

        private static bool IsStartOfFrame(byte marker)
        {
            return marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7
                or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf;
        }
    }
}
