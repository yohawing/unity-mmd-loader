#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mmd.Mme;
using NUnit.Framework;

namespace Mmd.Tests
{
    [TestFixture]
    public sealed class MmeFxScannerTests
    {
        private string? tempDirectory;

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(tempDirectory) && Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Test]
        public void ScanFromModelPath_AddsEmdMaterialIndexDescriptor()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "mmd-mme-fx-scanner-" + Guid.NewGuid().ToString("N"));
            string fxDirectory = Path.Combine(tempDirectory, "fx");
            string japaneseFxDirectory = Path.Combine(tempDirectory, "エフェクト");
            Directory.CreateDirectory(fxDirectory);
            Directory.CreateDirectory(japaneseFxDirectory);

            string pmxPath = Path.Combine(tempDirectory, "model.pmx");
            string baseFxPath = Path.Combine(tempDirectory, "base.fx");
            string fxPath = Path.Combine(fxDirectory, "body.fx");
            string otherFxPath = Path.Combine(fxDirectory, "other.fx");
            string japaneseFxPath = Path.Combine(japaneseFxDirectory, "肌.fx");
            File.WriteAllBytes(pmxPath, Array.Empty<byte>());
            File.WriteAllText(
                baseFxPath,
                @"#define USE_NORMALMAP
#include ""AlternativeFull.fxsub""
");
            File.WriteAllText(
                fxPath,
                @"#define USE_NORMALMAP
#include ""AlternativeFull.fxsub""
");
            File.WriteAllText(
                otherFxPath,
                @"#define USE_NORMALMAP
#include ""AlternativeFull.fxsub""
");
            File.WriteAllText(
                japaneseFxPath,
                @"#define USE_NORMALMAP
#include ""AlternativeFull.fxsub""
");
            byte[] emdBytes = Encoding.GetEncoding(932).GetBytes(
                @"[Effect]
Obj = base.fx
Obj[2] = fx\body.fx
Obj[3] = none
Obj[4] = エフェクト\肌.fx
");
            File.WriteAllBytes(Path.Combine(tempDirectory, "model.emd"), emdBytes);
            File.WriteAllText(
                Path.Combine(tempDirectory, "other-model.emd"),
                @"[Effect]
Obj[2] = fx\other.fx
");

            IReadOnlyList<MmeFxEffectDescriptor> legacyDescriptors = MmeFxScanner.ScanFromModelPath(pmxPath);
            IReadOnlyList<MmeFxEffectDescriptor> descriptors =
                MmeFxScanner.ScanFromModelPath(pmxPath, materialCount: 5);
            MmeFxEffectDescriptor[] mappedDescriptors =
                descriptors.Where(descriptor => descriptor.materialIndex == 2).ToArray();
            MmeFxEffectDescriptor[] defaultDescriptors =
                descriptors
                    .Where(descriptor =>
                        descriptor.materialIndex == 0 ||
                        descriptor.materialIndex == 1)
                    .ToArray();
            MmeFxEffectDescriptor[] noneDescriptors =
                descriptors.Where(descriptor => descriptor.materialIndex == 3).ToArray();
            MmeFxEffectDescriptor[] japanesePathDescriptors =
                descriptors.Where(descriptor => descriptor.materialIndex == 4).ToArray();
            MmeFxEffectDescriptor[] basenameFallbackDescriptors =
                descriptors.Where(descriptor => descriptor.materialIndex < 0).ToArray();

            Assert.That(legacyDescriptors, Is.Not.Empty);
            Assert.That(legacyDescriptors.Select(descriptor => descriptor.materialIndex), Is.All.EqualTo(-1));
            Assert.That(mappedDescriptors, Has.Length.EqualTo(1));
            MmeFxEffectDescriptor mappedDescriptor = mappedDescriptors[0];
            Assert.That(mappedDescriptor!.sourcePath, Is.EqualTo(fxPath));
            Assert.That(mappedDescriptor.effectType, Is.EqualTo("AlternativeFull"));
            Assert.That(defaultDescriptors, Has.Length.EqualTo(2));
            Assert.That(defaultDescriptors.Select(descriptor => descriptor.sourcePath), Is.All.EqualTo(baseFxPath));
            Assert.That(noneDescriptors, Is.Empty);
            Assert.That(japanesePathDescriptors, Has.Length.EqualTo(1));
            Assert.That(japanesePathDescriptors[0].sourcePath, Is.EqualTo(japaneseFxPath));
            Assert.That(basenameFallbackDescriptors, Is.Empty);
        }
    }
}
