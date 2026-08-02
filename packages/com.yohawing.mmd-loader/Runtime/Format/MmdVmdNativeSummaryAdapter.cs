#nullable enable

using System;
using System.Text;
using Mmd.Native;

namespace Mmd
{
    /// <summary>
    /// Converts the native shared-context VMD summary into the existing public DTO.
    /// </summary>
    internal static class MmdVmdNativeSummaryAdapter
    {
        private static readonly Encoding? Cp932Encoding = TryGetCp932Encoding();

        internal static MmdVmdParseSummary Read(byte[] vmdBytes)
        {
            if (vmdBytes == null || vmdBytes.Length == 0)
            {
                throw new ArgumentException("VMD bytes are required.", nameof(vmdBytes));
            }

            using var context = MmdRuntimeFfiVmdContext.Create(vmdBytes);
            return Read(context);
        }

        internal static MmdVmdParseSummary Read(MmdRuntimeFfiVmdContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return FromNativeSummary(context.ReadSummary());
        }

        private static MmdVmdParseSummary FromNativeSummary(
            MmdRuntimeFfiMethods.VmdContextSummary summary)
        {
            ValidateTrackSummary(summary.bones, "bone");
            ValidateTrackSummary(summary.morphs, "morph");
            ValidateTrackSummary(summary.cameras, "camera");
            ValidateTrackSummary(summary.lights, "light");
            ValidateTrackSummary(summary.selfShadows, "self-shadow");
            ValidateTrackSummary(summary.properties, "property");

            return new MmdVmdParseSummary(
                DecodeModelName(summary.targetModelNameBytes),
                ToManagedCount(summary.maxFrame, "max frame"),
                ToManagedCount(summary.bones.keyCount, "bone key count"),
                ToManagedCount(summary.morphs.keyCount, "morph key count"),
                ToManagedCount(summary.properties.keyCount, "property key count"),
                ToManagedCount(summary.propertyIkEntryCount, "property IK entry count"),
                ToManagedCount(summary.cameras.keyCount, "camera key count"),
                ToManagedCount(summary.lights.keyCount, "light key count"),
                ToManagedCount(summary.selfShadows.keyCount, "self-shadow key count"));
        }

        private static void ValidateTrackSummary(
            MmdRuntimeFfiMethods.VmdTrackSummary summary,
            string channel)
        {
            ToManagedCount(summary.trackCount, channel + " track count");
            ToManagedCount(summary.keyCount, channel + " key count");
        }

        private static int ToManagedCount(uint value, string field)
        {
            if (value > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Native VMD summary " + field + " is outside the managed DTO range: " +
                    value + ".");
            }

            return (int)value;
        }

        private static string DecodeModelName(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 20)
            {
                throw new InvalidOperationException(
                    "Native VMD summary model-name bytes must contain exactly 20 bytes.");
            }

            int end = 0;
            while (end < bytes.Length && bytes[end] != 0)
            {
                end++;
            }

            Encoding encoding = Cp932Encoding ?? throw new InvalidOperationException(
                "Native VMD summary requires code page 932 to decode the target model name.");
            return encoding.GetString(bytes, 0, end).TrimEnd(' ', '\0');
        }

        private static Encoding? TryGetCp932Encoding()
        {
            try
            {
                return Encoding.GetEncoding(932);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
