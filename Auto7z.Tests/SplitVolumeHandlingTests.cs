using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Auto7z.UI.Core;
using Xunit;
using Xunit.Abstractions;

namespace Auto7z.Tests
{
    public class SplitVolumeHandlingTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testRoot;
        private readonly List<string> _engineLogs = new();
        private readonly List<string> _cleanupPaths = new();

        public SplitVolumeHandlingTests(ITestOutputHelper output)
        {
            _output = output;
            _testRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
        }

        public void Dispose()
        {
            foreach (var path in _cleanupPaths)
            {
                if (!Directory.Exists(path)) continue;
                try { Directory.Delete(path, true); } catch { }
            }
        }

        private ExtractorEngine CreateEngine()
        {
            var settings = new AppSettings();
            var engine = new ExtractorEngine(settings);
            engine.Log += msg =>
            {
                _engineLogs.Add(msg);
                _output.WriteLine($"[Engine] {msg}");
            };
            return engine;
        }

        [Fact]
        public async Task NonFirstNumericSplitInputAutoSwitchesToFirstPart()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Auto7z_SplitSwitch_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            _cleanupPaths.Add(tempDir);

            var firstPart = Path.Combine(tempDir, "demo.001");
            var nonFirstPart = Path.Combine(tempDir, "demo.003");
            await File.WriteAllBytesAsync(firstPart, new byte[] { 0x50, 0x4B, 0x03, 0x04 });
            await File.WriteAllBytesAsync(nonFirstPart, new byte[] { 0x50, 0x4B, 0x03, 0x04 });

            var outputDir = Path.Combine(tempDir, "out");
            _cleanupPaths.Add(outputDir);

            var engine = CreateEngine();
            await engine.ProcessArchiveAsync(nonFirstPart, outputDir);

            Assert.Contains(_engineLogs, msg => msg.Contains("automatically switched to first part", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(_engineLogs, msg => msg.Contains("demo.001", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task MissingFirstPartShowsFriendlyMessage()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Auto7z_SplitMissing_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            _cleanupPaths.Add(tempDir);

            var nonFirstPart = Path.Combine(tempDir, "demo.003");
            await File.WriteAllBytesAsync(nonFirstPart, new byte[] { 0x41, 0x42, 0x43, 0x44 });

            var outputDir = Path.Combine(tempDir, "out");
            _cleanupPaths.Add(outputDir);

            var engine = CreateEngine();
            await engine.ProcessArchiveAsync(nonFirstPart, outputDir);

            Assert.Contains(_engineLogs, msg => msg.Contains("first part demo.001 is missing", StringComparison.OrdinalIgnoreCase));
        }
    }
}
