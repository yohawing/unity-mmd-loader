#nullable enable

using NUnit.Framework;
using Mmd.Editor;

namespace Mmd.Tests
{
    public sealed class MmdAssetPathUtilityTests
    {
        [Test]
        public void OutputPathValidationAcceptsProjectRelativeAssetPath()
        {
            bool valid = MmdAssetPathUtility.TryValidateProjectRelativeOutputPath(
                "Assets/Foo/bar.anim",
                ".anim",
                out string normalized,
                out MmdOutputPathError error);

            Assert.That(valid, Is.True);
            Assert.That(normalized, Is.EqualTo("Assets/Foo/bar.anim"));
            Assert.That(error, Is.EqualTo(MmdOutputPathError.None));
        }

        [TestCase("C:/outside/bar.anim")]
        [TestCase("C:\\outside\\bar.anim")]
        public void OutputPathValidationRejectsRootedPath(string outputPath)
        {
            AssertInvalid(outputPath, ".anim", MmdOutputPathError.Rooted);
        }

        [Test]
        public void OutputPathValidationRejectsPackagesPath()
        {
            AssertInvalid("Packages/com.example/bar.anim", ".anim", MmdOutputPathError.NotUnderAssets);
        }

        [Test]
        public void OutputPathValidationRejectsWrongExtension()
        {
            AssertInvalid("Assets/Foo/bar.prefab", ".anim", MmdOutputPathError.WrongExtension);
        }

        [TestCase("Assets/a/../b.anim")]
        [TestCase("Assets/a/./b.anim")]
        public void OutputPathValidationRejectsDotSegment(string outputPath)
        {
            AssertInvalid(outputPath, ".anim", MmdOutputPathError.EmptyOrDotSegment);
        }

        [Test]
        public void OutputPathValidationRejectsEmptySegment()
        {
            AssertInvalid("Assets//x.anim", ".anim", MmdOutputPathError.EmptyOrDotSegment);
        }

        [Test]
        public void OutputPathValidationNormalizesBackslashes()
        {
            bool valid = MmdAssetPathUtility.TryValidateProjectRelativeOutputPath(
                "Assets\\Foo\\bar.prefab",
                ".prefab",
                out string normalized,
                out MmdOutputPathError error);

            Assert.That(valid, Is.True);
            Assert.That(normalized, Is.EqualTo("Assets/Foo/bar.prefab"));
            Assert.That(error, Is.EqualTo(MmdOutputPathError.None));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void OutputPathValidationRejectsEmptyPath(string outputPath)
        {
            AssertInvalid(outputPath, ".anim", MmdOutputPathError.Empty);
        }

        private static void AssertInvalid(
            string outputPath,
            string requiredExtension,
            MmdOutputPathError expectedError)
        {
            bool valid = MmdAssetPathUtility.TryValidateProjectRelativeOutputPath(
                outputPath,
                requiredExtension,
                out string normalized,
                out MmdOutputPathError error);

            Assert.That(valid, Is.False);
            Assert.That(normalized, Is.Empty);
            Assert.That(error, Is.EqualTo(expectedError));
        }
    }
}
