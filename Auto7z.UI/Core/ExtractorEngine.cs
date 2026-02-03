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
            Log?.Invoke($"Starting process: {Path.GetFileName(sourceFile)}");
            
            // Create a unique temp folder for this session
            string sessionTemp = Path.Combine(Path.GetTempPath(), "Auto7z_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sessionTemp);

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
            if (depth > 5)
            {
                Log?.Invoke("Max recursion depth reached. Stopping here.");
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

            // Case: Single Folder inside (e.g. wrapper folder) -> Go deeper without counting as recursion step
            if (files.Length == 0 && dirs.Length == 1)
            {
                Log?.Invoke("Found wrapper folder, drilling down...");
                var subDir = dirs[0];
                files = Directory.GetFiles(subDir, "*", SearchOption.TopDirectoryOnly);
                dirs = Directory.GetDirectories(subDir, "*", SearchOption.TopDirectoryOnly);
                extractedDir = subDir;
            }

            // 3. Heuristics
            bool hasExecutable = files.Any(f => _executableExtensions.Contains(Path.GetExtension(f)));
            
            if (hasExecutable)
            {
                Log?.Invoke("Executable found! Target reached.");
                MoveToFinal(extractedDir, finalOutput);
                return;
            }

            var potentialArchives = files
                .Select(f => (Path: f, IsPotential: IsPotentialArchive(f, out var fmt), Format: fmt))
                .Where(x => x.IsPotential && !IsSplitVolumePartButNotFirst(x.Path))
                .Select(x => (x.Path, x.Format))
                .ToList();
            
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

                    MoveFileToFinal(file, finalOutput);
                }

                foreach (var dir in dirs)
                {
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
            File.Copy(sourceFile, dest);
        }

        private void MoveDirectoryToFinal(string sourceDir, string finalOutput)
        {
            if (!Directory.Exists(finalOutput)) Directory.CreateDirectory(finalOutput);
            
            string targetSub = Path.Combine(finalOutput, new DirectoryInfo(sourceDir).Name);
            if (!Directory.Exists(targetSub)) Directory.CreateDirectory(targetSub);
            CopyDirectory(sourceDir, targetSub);
        }



        private async Task<string?> TryExtractWithPasswordsAsync(string archivePath, string tempRoot, InArchiveFormat? forcedFormat = null)
        {
            string extractPath = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractPath);

            var passwords = _pwdManager.GetAttemptSequence(archivePath);

            return await Task.Run(() => 
            {
                foreach (var pwd in passwords)
                {
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
                            if (extractor.Check())
                            {
                                Log?.Invoke(string.IsNullOrEmpty(pwd) ? "Attempting: (No Password)" : $"Attempting password: {pwd}");
                                extractor.ExtractArchive(extractPath);
                                return extractPath;
                            }
                        }
                    }
                    catch (SevenZipException)
                    {
                    }
                    catch (Exception ex)
                    {
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
            
            if (string.IsNullOrEmpty(ext)) return true;
            if (_archiveExtensions.Contains(ext)) return true;
            if (_executableExtensions.Contains(ext)) return false;
            
            if (_settings.IsDisguisedExtension(ext))
            {
                if (_signatureDetector.IsDisguisedArchive(filePath, out var detectedFormat))
                {
                    forcedFormat = detectedFormat;
                    Log?.Invoke($"Disguised archive detected: {Path.GetFileName(filePath)} is actually {FileSignatureDetector.GetFormatName(detectedFormat!.Value)}");
                    return true;
                }
            }
            
            if (_nonArchiveExtensions.Contains(ext)) return false;
            
            return true;
        }

        private bool IsSplitVolumePartButNotFirst(string filePath)
        {
            string name = Path.GetFileName(filePath);
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            // .002, .003, ... (But .001 is first, so we return false for .001)
            if (ext.Length == 4 && int.TryParse(ext.TrimStart('.'), out int num))
            {
                return num > 1; 
            }

            // .part2.rar, .part02.rar ...
            if (name.Contains(".part", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
            {
                var lower = name.ToLowerInvariant();
                if (lower.EndsWith(".part1.rar") || lower.EndsWith(".part01.rar") || lower.EndsWith(".part001.rar")) return false;
                return true; 
            }

            // .r00, .r01 ... (Legacy RAR)
            if (System.Text.RegularExpressions.Regex.IsMatch(ext, @"^\.r\d+$"))
            {
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
                File.Copy(sourcePath, dest);
            }
            else if (Directory.Exists(sourcePath))
            {
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
                if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
            }
            catch 
            { 
                Log?.Invoke($"Warning: Could not fully clean up {tempPath}");
            }
        }
    }
}
