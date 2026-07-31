#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Mmd.Tests
{
    /// <summary>
    /// Test helper that launches the NVIDIA FLIP executable to compute a perceptual
    /// error (mean FLIP value) between two PNG images.
    ///
    /// Usage from a test:
    /// <code>
    ///     var availability = MmdFlipHelper.ProbeAvailability();
    ///     if (!availability.available)
    ///         Assert.Ignore("FLIP not available: " + availability.unsupportedReason);
    ///     float mean = MmdFlipHelper.ComputeMeanError(referencePath, testPath);
    ///     Assert.That(mean, Is.LessThanOrEqualTo(maxMean));
    /// </code>
    /// </summary>
    public static class MmdFlipHelper
    {
        /// <summary>
        /// Name of the FLIP executable (searched via PATH).
        /// </summary>
        public const string FlipExecutableName = "flip";

        /// <summary>
        /// Regex for parsing the "Mean: &lt;value&gt;" line from FLIP output.
        /// </summary>
        private static readonly Regex MeanPattern = new Regex(
            @"^\s*Mean:\s+([0-9]+(?:\.[0-9]+)?)",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// Probes whether the FLIP executable is available on PATH.
        /// </summary>
        public static MmdFlipAvailability ProbeAvailability()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = FlipExecutableName,
                        Arguments = "--help",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };
                process.Start();
                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(5000))
                {
                    process.Kill();
                    return Unavailable("FLIP --help timed out after 5 seconds.");
                }
                string _ = stdoutTask.GetAwaiter().GetResult();
                string err = stderrTask.GetAwaiter().GetResult();

                if (process.ExitCode != 0)
                {
                    return Unavailable("FLIP --help exited with code " + process.ExitCode + ": " + err.Trim());
                }

                return new MmdFlipAvailability { available = true };
            }
            catch (Exception ex) when (
                ex is System.ComponentModel.Win32Exception
                || ex is InvalidOperationException
                || ex is FileNotFoundException)
            {
                return Unavailable("FLIP executable not found on PATH (\"" + FlipExecutableName + "\"): " + ex.Message);
            }
        }

        /// <summary>
        /// Runs FLIP on the two given PNG images and returns the mean perceptual error.
        /// </summary>
        /// <param name="referencePath">Path to the reference (golden) PNG.</param>
        /// <param name="testPath">Path to the test PNG.</param>
        /// <param name="outputDir">
        /// Directory FLIP writes its error-map PNG into. When null, a temp directory is
        /// used so error maps never litter the current working directory (the Unity
        /// project root) during a test run.
        /// </param>
        /// <returns>The mean FLIP error (0 = identical, 1 = maximally different).</returns>
        /// <exception cref="InvalidOperationException">If FLIP fails or output cannot be parsed.</exception>
        public static float ComputeMeanError(string referencePath, string testPath, string? outputDir = null)
        {
            if (string.IsNullOrEmpty(referencePath))
                throw new ArgumentException("Reference path must not be null or empty", nameof(referencePath));
            if (string.IsNullOrEmpty(testPath))
                throw new ArgumentException("Test path must not be null or empty", nameof(testPath));
            if (!File.Exists(referencePath))
                throw new FileNotFoundException("Reference file not found", referencePath);
            if (!File.Exists(testPath))
                throw new FileNotFoundException("Test file not found", testPath);

            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = Path.Combine(Path.GetTempPath(), "mmd-flip-errormaps");
            }
            Directory.CreateDirectory(outputDir);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FlipExecutableName,
                    // Use ArgumentList so paths with spaces/special characters are passed
                    // verbatim without manual quoting. -d keeps FLIP error maps out of cwd.
                    ArgumentList = { "-v", "1", "-r", referencePath, "-t", testPath, "-d", outputDir },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            process.Start();
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill();
                throw new TimeoutException("FLIP comparison timed out after 30 seconds.");
            }
            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                string detail = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                throw new InvalidOperationException(
                    "FLIP exited with code " + process.ExitCode + ": " + detail.Trim());
            }

            var match = MeanPattern.Match(stdout);
            if (!match.Success || !float.TryParse(match.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float mean))
            {
                throw new InvalidOperationException(
                    "Could not parse mean FLIP error from output:\n" + stdout);
            }

            return mean;
        }

        private static MmdFlipAvailability Unavailable(string reason)
        {
            return new MmdFlipAvailability
            {
                available = false,
                unsupportedReason = reason
            };
        }
    }

    /// <summary>
    /// Result of probing FLIP executable availability.
    /// </summary>
    public struct MmdFlipAvailability
    {
        /// <summary>True if FLIP is available and ready to use.</summary>
        public bool available;
        /// <summary>Human-readable reason when <see cref="available"/> is false.</summary>
        public string unsupportedReason;
    }
}
