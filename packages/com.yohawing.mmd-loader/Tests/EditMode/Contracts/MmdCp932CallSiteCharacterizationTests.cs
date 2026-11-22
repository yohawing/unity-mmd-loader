#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Text;
using Mmd.Editor;
using Mmd.Parser;
using Mmd.UnityIntegration;
using NUnit.Framework;

namespace Mmd.Tests
{
    /// <summary>
    /// Pins the CP932 output of the model-only rest VMD header and the MME normal-map text fallback,
    /// the two call sites that used to look up code page 932 directly.
    /// </summary>
    [TestFixture]
    public sealed class MmdCp932CallSiteCharacterizationTests
    {
        private static readonly Encoding Cp932 = Encoding.GetEncoding(932);

        [Test]
        public void ModelOnlyRestMotionWritesCp932ModelNameIntoVmdHeader()
        {
            const string modelName = "初音ミク";
            var model = new MmdModelDefinition { name = modelName };
            MethodInfo method = typeof(MmdUnityPlaybackBinding).GetMethod(
                "CreateModelOnlyRestMotion",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.That(method, Is.Not.Null);

            var motion = (MmdMotionDefinition)method.Invoke(null, new object[] { model })!;

            byte[] expectedName = Cp932.GetBytes(modelName);
            Assert.That(motion.targetModelName, Is.EqualTo(modelName));
            Assert.That(motion.sourceBytes, Is.Not.Null);
            Assert.That(motion.sourceBytes!.Length, Is.EqualTo(66));
            Assert.That(Encoding.ASCII.GetString(motion.sourceBytes, 0, 25), Is.EqualTo("Vocaloid Motion Data 0002"));
            var actualName = new byte[expectedName.Length];
            Array.Copy(motion.sourceBytes, 30, actualName, 0, expectedName.Length);
            Assert.That(actualName, Is.EqualTo(expectedName));
            Assert.That(motion.sourceBytes[30 + expectedName.Length], Is.EqualTo(0));
        }

        [Test]
        public void MmeNormalMapTextFallsBackToCp932ForNonUtf8Bytes()
        {
            const string text = "#define NORMALMAP_MAIN_FILENAME \"法線.png\"";
            string path = Path.Combine(Path.GetTempPath(), "mmd-cp932-" + Guid.NewGuid().ToString("N") + ".fx");
            File.WriteAllBytes(path, Cp932.GetBytes(text));
            try
            {
                MethodInfo method = typeof(MmdMmeNormalMapTexturePostprocessor).GetMethod(
                    "ReadTextBestEffort",
                    BindingFlags.NonPublic | BindingFlags.Static)!;
                Assert.That(method, Is.Not.Null);

                var decoded = (string)method.Invoke(null, new object[] { path })!;

                Assert.That(decoded, Is.EqualTo(text));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
