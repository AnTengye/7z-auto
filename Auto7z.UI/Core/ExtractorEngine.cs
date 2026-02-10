using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SevenZip;

namespace Auto7z.UI.Core
{
    public class ExtractorEngine
    {
        private readonly PasswordManager _pwdManager;
        private readonly AppSettings _settings;
        private readonly FileSignatureDetector _signatureDetector;
        private readonly Logger _log = Logger.Instance;
        
        private readonly HashSet<string> _executableExtensions = new(StringComparer.OrdinalIgnoreCase) 
        { 
            ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".com" 
        };
        private readonly HashSet<string> _archiveExtensions = new(StringComparer.OrdinalIgnoreCase) 
        { 
            ".7z", ".rar", ".zip", ".tar", ".gz", ".iso", ".bz2", ".xz", ".001" 
        };

        private readonly HashSet<string> _nonArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".mp3", ".mp4", ".avi", ".mkv", ".wav", ".pdf", ".doc", ".docx", ".xls", ".xlsx"
        };

        public event Action<string>? Log;

        public ExtractorEngine(AppSettings settings)
        {
            _settings = settings;
            _pwdManager = new PasswordManager();
            _signatureDetector = new FileSignatureDetector();
            InitializeSevenZip();
        }

        private void InitializeSevenZip()
        {
            var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.dll");
            if (File.Exists(dllPath))
            {
                SevenZipBase.SetLibraryPath(dllPath);
            }
            else
            {
                // Fallback or let it fail later with a clear message
                Log?.Invoke($"WARNING: 7z.dll not found at {dllPath}. Please ensure it is in the output directory.");
            }
        }

        public async Task ProcessArchiveAsync(string sourceFile, string outputRoot)
        {
            var fileInfo = new FileInfo(sourceFile);
            Log?.Invoke($"Starting process: {Path.GetFileName(sourceFile)}");
            _log.Debug($"ProcessArchive: {sourceFile} ({fileInfo.Length} bytes) → output: {outputRoot}", "Engine");
            
            // Create a unique temp folder for this session
            string sessionTemp = Path.Combine(Path.GetTempPath(), "Auto7z_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sessionTemp);
            _log.Debug($"Temp directory: {sessionTemp}", "Engine");

            try
            {
                await RecursiveExtractAsync(sourceFile, sessionTemp, outputRoot, 0);
            }
            finally
            {
                Cleanup(sessionTemp);
                Log?.Invoke("Cleanup completed.");
            }
        }

        private async Task RecursiveExtractAsync(string currentFile, string tempRoot, string finalOutput, int depth)
        {
            await RecursiveExtractAsync(currentFile, tempRoot, finalOutput, depth, null);
        }

        private async Task RecursiveExtractAsync(string currentFile, string tempRoot, string finalOutput, int depth, InArchiveFormat? forcedFormat)
        {
            var currentFileInfo = new FileInfo(currentFile);
            string ext = Path.GetExtension(currentFile);
            _log.Debug($"RecursiveExtract depth={depth}: {Path.GetFileName(currentFile)}, ext={ext}, size={currentFileInfo.Length}, forcedFormat={forcedFormat?.ToString() ?? "null"}", "Engine");

            if (depth > 5)
            {
                Log?.Invoke("Max recursion depth reached. Stopping here.");
                _log.Warning($"Max recursion depth (5) reached for: {Path.GetFileName(currentFile)}", "Engine");
                MoveToFinal(currentFile, finalOutput);
                return;
            }

            Log?.Invoke($"{new string('-', depth * 2)} Analysing: {Path.GetFileName(currentFile)}");

            string? extractedDir = await TryExtractWithPasswordsAsync(currentFile, tempRoot, forcedFormat);

            if (extractedDir == null)
            {
                Log?.Invoke($"Failed to extract {Path.GetFileName(currentFile)} or no password matched.");
                // Move the archive itself to output so user can handle it
                MoveToFinal(currentFile, finalOutput);
                return;
            }

            // 2. Analyze content
            var files = Directory.GetFiles(extractedDir, "*", SearchOption.TopDirectoryOnly);
            var dirs = Directory.GetDirectories(extractedDir, "*", SearchOption.TopDirectoryOnly);
            _log.Debug($"Extracted content: {files.Length} file(s), {dirs.Length} dir(s) in {extractedDir}", "Engine");

            // Case: Single Folder inside (e.g. wrapper folder) -> Go deeper without counting as recursion step
            if (files.Length == 0 && dirs.Length == 1)
            {
                Log?.Invoke("Found wrapper folder, drilling down...");
                var subDir = dirs[0];
                _log.Debug($"Wrapper folder: {Path.GetFileName(subDir)}", "Engine");
                files = Directory.GetFiles(subDir, "*", SearchOption.TopDirectoryOnly);
                dirs = Directory.GetDirectories(subDir, "*", SearchOption.TopDirectoryOnly);
                extractedDir = subDir;
                _log.Debug($"After drill-down: {files.Length} file(s), {dirs.Length} dir(s)", "Engine");
            }

            // 3. Heuristics
            bool hasExecutable = files.Any(f => _executableExtensions.Contains(Path.GetExtension(f)));
            _log.Debug($"Heuristics: hasExecutable={hasExecutable}", "Engine");
            
            if (hasExecutable)
            {
                var exeFiles = files.Where(f => _executableExtensions.Contains(Path.GetExtension(f))).Select(Path.GetFileName);
                _log.Debug($"Executables found: {string.Join(", ", exeFiles)}", "Engine");
                Log?.Invoke("Executable found! Target reached.");
                MoveToFinal(extractedDir, finalOutput);
                return;
            }

            var potentialArchives = files
                .Select(f => (Path: f, IsPotential: IsPotentialArchive(f, out var fmt), Format: fmt))
                .Where(x => x.IsPotential && !IsSplitVolumePartButNotFirst(x.Path))
                .Select(x => (x.Path, x.Format))
                .ToList();

            _log.Debug($"Potential archives: {potentialArchives.Count} — [{string.Join(", ", potentialArchives.Select(a => Path.GetFileName(a.Path)))}]", "Engine");
            
            if (potentialArchives.Any())
            {
                Log?.Invoke($"Found {potentialArchives.Count} nested archive(s). Processing...");

                foreach (var (archive, format) in potentialArchives)
                {
                     Log?.Invoke($"Recursing into: {Path.GetFileName(archive)}");
                     await RecursiveExtractAsync(archive, tempRoot, finalOutput, depth + 1, format);
                }

                foreach (var file in files)
                {
                    if (IsPotentialArchive(file)) continue; 
                    if (IsSplitVolumePartButNotFirst(file)) continue;

                    _log.Debug($"Moving non-archive file to output: {Path.GetFileName(file)}", "Engine");
                    MoveFileToFinal(file, finalOutput);
                }

                foreach (var dir in dirs)
                {
                    _log.Debug($"Moving directory to output: {new DirectoryInfo(dir).Name}", "Engine");
                    MoveDirectoryToFinal(dir, finalOutput);
                }
            }
            else
            {
                Log?.Invoke("Extracted content is generic. Moving to output.");
                MoveToFinal(extractedDir, finalOutput);
            }
        }

        private void MoveFileToFinal(string sourceFile, string finalOutput)
        {
            if (!Directory.Exists(finalOutput)) Directory.CreateDirectory(finalOutput);
            string dest = Path.Combine(finalOutput, Path.GetFileName(sourceFile));
            dest = EnsureUniquePath(dest);
            var fi = new FileInfo(sourceFile);
            _log.Debug($"MoveFileToFinal: {fi.Name} ({fi.Length} bytes) → {dest}", "Engine");
            File.Copy(sourceFile, dest);
        }

        private void MoveDirectoryToFinal(string sourceDir, string finalOutput)
        {
            if (!Directory.Exists(finalOutput)) Directory.CreateDirectory(finalOutput);
            
            string targetSub = Path.Combine(finalOutput, new DirectoryInfo(sourceDir).Name);
            _log.Debug($"MoveDirectoryToFinal: {new DirectoryInfo(sourceDir).Name} → {targetSub}", "Engine");
            if (!Directory.Exists(targetSub)) Directory.CreateDirectory(targetSub);
            CopyDirectory(sourceDir, targetSub);
        }



        private async Task<string?> TryExtractWithPasswordsAsync(string archivePath, string tempRoot, InArchiveFormat? forcedFormat = null)
        {
            string extractPath = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractPath);

            var passwords = _pwdManager.GetAttemptSequence(archivePath).ToList();
            _log.Debug($"TryExtract: {passwords.Count} passwords to attempt, forcedFormat={forcedFormat?.ToString() ?? "null"}", "Engine");

            return await Task.Run(() => 
            {
                int index = 0;
                foreach (var pwd in passwords)
                {
                    index++;
                    string pwdDisplay = string.IsNullOrEmpty(pwd) ? "(empty)" 
                        : pwd.Length <= 2 ? "**" 
                        : pwd[..2] + new string('*', Math.Min(pwd.Length - 2, 4));
                    
                    try
                    {
                        SevenZipExtractor extractor;
                        if (forcedFormat.HasValue)
                        {
                            extractor = new SevenZipExtractor(archivePath, pwd, forcedFormat.Value);
                        }
                        else
                        {
                            extractor = new SevenZipExtractor(archivePath, pwd);
                        }

                        using (extractor)
                        {
                            bool checkResult = extractor.Check();
                            _log.Debug($"Password [{index}/{passwords.Count}]: {pwdDisplay} → Check()={checkResult}", "Engine");
                            
                            if (checkResult)
                            {
                                Log?.Invoke(string.IsNullOrEmpty(pwd) ? "Attempting: (No Password)" : $"Attempting password: {pwd}");
                                extractor.ExtractArchive(extractPath);
                                var extractedFiles = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);
                                _log.Debug($"Extraction succeeded with password {pwdDisplay}, {extractedFiles.Length} file(s) extracted", "Engine");
                                return extractPath;
                            }
                        }
                    }
                    catch (SevenZipException ex)
                    {
                        _log.Debug($"Password [{index}/{passwords.Count}]: {pwdDisplay} → SevenZipException: {ex.Message}", "Engine");
                    }
                    catch (Exception ex)
                    {
                        _log.Debug($"Password [{index}/{passwords.Count}]: {pwdDisplay} → {ex.GetType().Name}: {ex.Message}", "Engine");
                        Log?.Invoke($"Error: {ex.Message}");
                    }
                }
                return null;
            });
        }

        private bool IsPotentialArchive(string filePath)
        {
            return IsPotentialArchive(filePath, out _);
        }

        private bool IsPotentialArchive(string filePath, out InArchiveFormat? forcedFormat)
        {
            forcedFormat = null;
            string ext = Path.GetExtension(filePath);
            string fileName = Path.GetFileName(filePath);
            
            if (string.IsNullOrEmpty(ext))
            {
                _log.Debug($"IsPotentialArchive: {fileName} → no extension → treating as potential archive", "Engine");
                return true;
            }
            if (_archiveExtensions.Contains(ext))
            {
                _log.Debug($"IsPotentialArchive: {fileName} → {ext} is known archive extension → true", "Engine");
                return true;
            }
            if (_executableExtensions.Contains(ext))
            {
                _log.Debug($"IsPotentialArchive: {fileName} → {ext} is executable → false", "Engine");
                return false;
            }
            
            if (_settings.IsDisguisedExtension(ext))
            {
                _log.Debug($"IsPotentialArchive: {fileName} → {ext} is disguised extension candidate, running signature check", "Engine");
                if (_signatureDetector.IsDisguisedArchive(filePath, out var detectedFormat))
                {
                    forcedFormat = detectedFormat;
                    _log.Debug($"IsPotentialArchive: {fileName} → disguised archive detected as {FileSignatureDetector.GetFormatName(detectedFormat!.Value)}", "Engine");
                    Log?.Invoke($"Disguised archive detected: {fileName} is actually {FileSignatureDetector.GetFormatName(detectedFormat!.Value)}");
                    return true;
                }
                _log.Debug($"IsPotentialArchive: {fileName} → signature check negative, not a disguised archive", "Engine");
            }
            
            if (_nonArchiveExtensions.Contains(ext))
            {
                _log.Debug($"IsPotentialArchive: {fileName} → {ext} is known non-archive → false", "Engine");
                return false;
            }
            
            _log.Warning($"IsPotentialArchive: {fileName} → {ext} is unknown extension, treating as potential archive", "Engine");
            return true;
        }

        private bool IsSplitVolumePartButNotFirst(string filePath)
        {
            string name = Path.GetFileName(filePath);
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            // .002, .003, ... (But .001 is first, so we return false for .001)
            if (ext.Length == 4 && int.TryParse(ext.TrimStart('.'), out int num))
            {
                if (num > 1)
                {
                    _log.Debug($"SplitVolume: {name} → {ext} is split part {num}, skipping", "Engine");
                    return true;
                }
                return false;
            }

            // .part2.rar, .part02.rar ...
            if (name.Contains(".part", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
            {
                var lower = name.ToLowerInvariant();
                if (lower.EndsWith(".part1.rar") || lower.EndsWith(".part01.rar") || lower.EndsWith(".part001.rar")) return false;
                _log.Debug($"SplitVolume: {name} → non-first RAR split part, skipping", "Engine");
                return true; 
            }

            // .r00, .r01 ... (Legacy RAR)
            if (System.Text.RegularExpressions.Regex.IsMatch(ext, @"^\.r\d+$"))
            {
                _log.Debug($"SplitVolume: {name} → legacy RAR split {ext}, skipping", "Engine");
                return true;
            }

            return false;
        }

        private void MoveToFinal(string sourcePath, string finalOutput)
        {
            if (!Directory.Exists(finalOutput)) Directory.CreateDirectory(finalOutput);

            if (File.Exists(sourcePath))
            {
                string dest = Path.Combine(finalOutput, Path.GetFileName(sourcePath));
                dest = EnsureUniquePath(dest);
                _log.Debug($"MoveToFinal (file): {Path.GetFileName(sourcePath)} → {dest}", "Engine");
                File.Copy(sourcePath, dest);
            }
            else if (Directory.Exists(sourcePath))
            {
                _log.Debug($"MoveToFinal (dir): {new DirectoryInfo(sourcePath).Name} → {finalOutput}", "Engine");
                CopyDirectory(sourcePath, finalOutput);
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            foreach (var file in dir.GetFiles())
            {
                string targetPath = Path.Combine(destDir, file.Name);
                targetPath = EnsureUniquePath(targetPath);
                file.CopyTo(targetPath);
            }

            foreach (var sub in dir.GetDirectories())
            {
                string targetSub = Path.Combine(destDir, sub.Name);
                if (!Directory.Exists(targetSub)) Directory.CreateDirectory(targetSub);
                CopyDirectory(sub.FullName, targetSub);
            }
        }

        private string EnsureUniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;

            string dir = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int i = 1;

            while (true)
            {
                string newPath = Path.Combine(dir, $"{name}_{i}{ext}");
                if (!File.Exists(newPath) && !Directory.Exists(newPath)) return newPath;
                i++;
            }
        }

        private void Cleanup(string tempPath)
        {
            try
            {
                if (Directory.Exists(tempPath))
                {
                    var fileCount = Directory.GetFiles(tempPath, "*", SearchOption.AllDirectories).Length;
                    _log.Debug($"Cleanup: removing {tempPath} ({fileCount} file(s))", "Engine");
                    Directory.Delete(tempPath, true);
                }
            }
            catch (Exception ex)
            { 
                _log.Warning($"Cleanup failed for {tempPath}: {ex.Message}", "Engine");
                Log?.Invoke($"Warning: Could not fully clean up {tempPath}");
            }
        }
    }
}
