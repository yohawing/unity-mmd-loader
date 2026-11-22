#nullable enable

using System;
using System.Text;
using Mmd.Native;

namespace Mmd
{
    /// <summary>
    /// Single lookup for the CP932 (Shift-JIS) encoding used by VMD/PMX names and MME text.
    /// The lookup is lazy and never throws from a static initializer: a runtime without the code
    /// page (for example a player build without CJK encoding support) reports
    /// <see cref="MmdRuntimeUnsupportedException"/> at the point of use instead of a
    /// TypeInitializationException on the calling type.
    /// </summary>
    internal static class MmdCp932Encoding
    {
        private const int CodePage = 932;

        private static readonly Lazy<LookupResult> Lookup = new Lazy<LookupResult>(Load);

        /// <summary>Returns CP932, or false with the platform failure reason when it is unavailable.</summary>
        internal static bool TryGet(out Encoding encoding, out string failureReason)
        {
            LookupResult result = Lookup.Value;
            encoding = result.Encoding!;
            failureReason = result.FailureReason;
            return result.Encoding != null;
        }

        /// <summary>
        /// Returns CP932 or throws <see cref="MmdRuntimeUnsupportedException"/> naming what needed it.
        /// </summary>
        internal static Encoding Get(string purpose)
        {
            if (TryGet(out Encoding encoding, out string failureReason))
            {
                return encoding;
            }

            throw new MmdRuntimeUnsupportedException(
                "CP932/Shift-JIS decoding is unavailable for " + purpose + " (" + failureReason + ").");
        }

        private static LookupResult Load()
        {
            try
            {
                return new LookupResult(Encoding.GetEncoding(CodePage), string.Empty);
            }
            catch (ArgumentException exception)
            {
                return new LookupResult(null, exception.GetType().Name + ": " + exception.Message);
            }
            catch (NotSupportedException exception)
            {
                return new LookupResult(null, exception.GetType().Name + ": " + exception.Message);
            }
        }

        private sealed class LookupResult
        {
            internal LookupResult(Encoding? encoding, string failureReason)
            {
                Encoding = encoding;
                FailureReason = failureReason;
            }

            internal Encoding? Encoding { get; }

            internal string FailureReason { get; }
        }
    }
}
