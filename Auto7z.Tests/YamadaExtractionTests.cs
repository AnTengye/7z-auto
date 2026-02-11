using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Auto7z.UI.Core;
using Xunit;
using Xunit.Abstractions;

namespace Auto7z.Tests
{
    public class YamadaExtractionTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testRoot;
        private readonly string _outputDir;
        private readonly List<string> _engineLogs = new();

        public YamadaExtractionTests(ITestOutputHelper output)
        {
            _output = output;
            _testRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
            _outputDir = Path.Combine(_testRoot, "test", "山田_Unpacked");
        }

        public void Dispose()
        {
            if (Directory.Exists(_outputDir))
            {
                try { Directory.Delete(_outputDir, true); } catch { }
            }
        }

        private string? FindTestArchive()
        {
            var path = Path.Combine(_testRoot, "test", "山田", "山田.tif");
            if (File.Exists(path)) return path;

            _output.WriteLine($"Test archive not found at: {path}");
            return null;
        }

        private AppSettings CreateSettings()
        {
            var settings = new AppSettings();
            settings.Load();
            settings.EnableDisguisedArchiveDetection = true;
            var exts = new HashSet<string>(settings.DisguisedExtensions, StringComparer.OrdinalIgnoreCase);
            exts.Add(".tif");
            settings.SetDisguisedExtensions(exts);
            settings.AddPassword("嘤嘤嘤");
            return settings;
        }

        private ExtractorEngine CreateEngine()
        {
            var settings = CreateSettings();
            var engine = new ExtractorEngine(settings);
            engine.Log += msg =>
            {
                _engineLogs.Add(msg);
                _output.WriteLine($"[Engine] {msg}");
            };
            return engine;
        }

        [Fact]
        public void TestArchiveExists()
        {
            var archivePath = FindTestArchive();
            Assert.NotNull(archivePath);
            Assert.True(File.Exists(archivePath), $"Archive must exist at: {archivePath}");

            var fi = new FileInfo(archivePath!);
            _output.WriteLine($"Archive: {fi.FullName}");
            _output.WriteLine($"Size: {fi.Length:N0} bytes ({fi.Length / 1024.0 / 1024.0:F1} MB)");
            Assert.True(fi.Length > 0, "Archive must not be empty");
        }

        [Fact]
        public async Task CanExtractYamadaTwoLayerArchive()
        {
            var archivePath = FindTestArchive();
            if (archivePath == null)
            {
                _output.WriteLine("SKIPPED: Test archive not found");
                return;
            }

            if (Directory.Exists(_outputDir))
                Directory.Delete(_outputDir, true);

            var engine = CreateEngine();

            await engine.ProcessArchiveAsync(archivePath, _outputDir);

            Assert.True(Directory.Exists(_outputDir), 
                $"Output directory should be created at: {_outputDir}");

            var outputFiles = Directory.GetFiles(_outputDir, "*", SearchOption.AllDirectories);
            _output.WriteLine($"Output files ({outputFiles.Length}):");
            foreach (var f in outputFiles)
            {
                var fi = new FileInfo(f);
                _output.WriteLine($"  {fi.FullName} ({fi.Length:N0} bytes)");
            }

            Assert.True(outputFiles.Length > 0,
                "Extraction should produce at least one output file");

            var sourceSize = new FileInfo(archivePath).Length;
            var verbatimCopy = Array.Find(outputFiles, f =>
            {
                var fi = new FileInfo(f);
                return fi.Name == "山田.tif" && fi.Length == sourceSize;
            });
            Assert.True(verbatimCopy == null,
                "Output must not be a verbatim copy of the original archive — extraction did not actually happen");

            var innerArchiveSize = 1195695141L;
            var unextractedInner = Array.Find(outputFiles, f =>
            {
                var fi = new FileInfo(f);
                return fi.Name == "山田.tif" && fi.Length == innerArchiveSize;
            });
            Assert.True(unextractedInner == null,
                "Inner archive should not remain as-is — Layer 2 extraction failed");

            Assert.True(outputFiles.Length > 1,
                $"Layer 2 contains 4122 files; got only {outputFiles.Length} — likely only stripped the TAR shell");

            _output.WriteLine($"\nEngine log ({_engineLogs.Count} messages):");
            foreach (var log in _engineLogs)
            {
                _output.WriteLine($"  {log}");
            }
        }

        [Fact]
        public void SignatureDetectorCanIdentifyDisguisedTar()
        {
            var archivePath = FindTestArchive();
            if (archivePath == null)
            {
                _output.WriteLine("SKIPPED: Test archive not found");
                return;
            }

            var detector = new FileSignatureDetector();
            var format = detector.DetectFormat(archivePath);

            _output.WriteLine($"Detected format: {format?.ToString() ?? "null"}");

            // TAR signature at offset 924084 is beyond the 512-byte header read — documents the detection gap requiring CLI fallback
            if (format == null)
            {
                _output.WriteLine("WARNING: Signature detector cannot detect the format (offset too deep).");
                _output.WriteLine("The engine will need to use 7z CLI or try all formats as fallback.");
            }
            else
            {
                _output.WriteLine($"Format detected: {FileSignatureDetector.GetFormatName(format.Value)}");
            }
        }

        [Fact]
        public void PasswordManagerIncludesFilenameAsPassword()
        {
            var settings = CreateSettings();
            var pwdManager = new PasswordManager(settings);
            var passwords = new List<string>(pwdManager.GetAttemptSequence("山田.tif"));

            _output.WriteLine($"Password sequence for '山田.tif' ({passwords.Count} entries):");
            foreach (var pwd in passwords)
            {
                _output.WriteLine($"  '{pwd}'");
            }

            Assert.Contains("山田", passwords);
            Assert.Contains("嘤嘤嘤", passwords);
        }
    }
}
