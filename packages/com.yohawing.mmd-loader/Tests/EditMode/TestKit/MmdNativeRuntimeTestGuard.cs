#nullable enable

using System;
using NUnit.Framework;

namespace Mmd.Tests
{
    /// <summary>
    /// Skips a test when the packaged mmd-runtime native library cannot be loaded, instead of
    /// deciding by Editor OS. The Windows checkout ships the DLL and CI installs the Linux .so,
    /// so on those runners these tests execute; an Editor without a native build (for example a
    /// plain macOS or Linux checkout) skips them. Gates that admit only allowlisted skips still
    /// fail when a runner that should have the library skips.
    /// </summary>
    internal static class MmdNativeRuntimeTestGuard
    {
        /// <param name="validateAbi">
        /// Calls the runtime's ABI check (MmdRuntimeFfiMethods.ValidateAbiVersion). It is passed
        /// in because that type is internal to Mmd.Runtime and this test kit has no access to it.
        /// </param>
        internal static void IgnoreIfUnavailable(Action validateAbi)
        {
            try
            {
                validateAbi();
            }
            catch (DllNotFoundException ex)
            {
                Assert.Ignore("Native runtime is unavailable: " + ex.Message);
            }
            catch (EntryPointNotFoundException ex)
            {
                Assert.Ignore("Native runtime ABI entry point is unavailable: " + ex.Message);
            }
            catch (BadImageFormatException ex)
            {
                Assert.Ignore("Native runtime binary is incompatible: " + ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Assert.Ignore("Native runtime ABI is unsupported: " + ex.Message);
            }
        }
    }
}
