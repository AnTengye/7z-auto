using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Auto7z.UI.Core;
using Xunit;

namespace Auto7z.Tests
{
    public class ArchiveDetectionPolicyTests : IDisposable
    {
        private readonly List<string> _cleanupPaths = new();

        public void Dispose()
        {
            foreach (var path in _cleanupPaths)
            {
                if (File.Exists(path))
                {
                    try { File.Delete(path); } catch { }
                }
            }
        }

        [Fact]
        public void UnknownExtensionIsNotArchiveWhenAutoDetectDisabled()
        {
            var settings = new AppSettings { AutoDetectUnknownExtensions = false };
            var engine = new ExtractorEngine(settings);

            var filePath = CreateTempFile(".abc");
            bool isPotential = InvokeIsPotentialArchive(engine, filePath);

            Assert.False(isPotential);
        }

        [Fact]
        public void UnknownExtensionIsArchiveWhenAutoDetectEnabled()
        {
            var settings = new AppSettings { AutoDetectUnknownExtensions = true };
            var engine = new ExtractorEngine(settings);

            var filePath = CreateTempFile(".abc");
            bool isPotential = InvokeIsPotentialArchive(engine, filePath);

            Assert.True(isPotential);
        }

        [Fact]
        public void ConfiguredExtensionIsAlwaysTriedEvenWhenSignatureUnknown()
        {
            var settings = new AppSettings
            {
                AutoDetectUnknownExtensions = false,
                EnableDisguisedArchiveDetection = false
            };
            settings.DisguisedExtensions.Add(".tif");
            var engine = new ExtractorEngine(settings);

            var filePath = CreateTempFile(".tif");
            bool isPotential = InvokeIsPotentialArchive(engine, filePath);

            Assert.True(isPotential);
        }

        private string CreateTempFile(string extension)
        {
            var filePath = Path.Combine(Path.GetTempPath(), "Auto7z_UnknownExt_" + Guid.NewGuid().ToString("N") + extension);
            File.WriteAllBytes(filePath, new byte[] { 0x11, 0x22, 0x33, 0x44 });
            _cleanupPaths.Add(filePath);
            return filePath;
        }

        private static bool InvokeIsPotentialArchive(ExtractorEngine engine, string filePath)
        {
            var method = typeof(ExtractorEngine).GetMethod("IsPotentialArchive", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            Assert.NotNull(method);
            var result = method!.Invoke(engine, new object[] { filePath });
            Assert.NotNull(result);
            return (bool)result!;
        }
    }
}
