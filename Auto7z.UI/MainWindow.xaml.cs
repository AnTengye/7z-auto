using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Auto7z.UI.Core;

namespace Auto7z.UI
{
    public partial class MainWindow : Window
    {
        private readonly ExtractorEngine _engine;
        private readonly AppSettings _settings;
        private bool _isBusy;

        public MainWindow()
        {
            InitializeComponent();
            _settings = new AppSettings();
            _settings.Load();

            Logger.Instance.MinLevel = _settings.LogLevel;
            Logger.Instance.OnLog += OnLogReceived;

            _engine = new ExtractorEngine(_settings);
            _engine.Log += msg => Logger.Instance.Info(msg, "Engine");
            _engine.Progress += OnProgress;

            Logger.Instance.Info("Auto7z initialized. Waiting for files...", "UI");
            Logger.Instance.Debug($"Current Directory: {AppDomain.CurrentDomain.BaseDirectory}", "UI");

            var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.dll");
            if (!File.Exists(dllPath))
                Logger.Instance.Warning("7z.dll not found! Place it next to the executable.", "UI");
        }

        private void OnLogReceived(string message, LogLevel level)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                LogBox.ScrollToEnd();
                StatusText.Text = message;
            });
        }

        private void OnProgress(byte percent)
        {
            Dispatcher.Invoke(() =>
            {
                if (ExtractionProgress.Visibility != Visibility.Visible)
                    ExtractionProgress.Visibility = Visibility.Visible;
                
                ExtractionProgress.Value = percent;
            });
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_settings);
            settingsWindow.Owner = this;
            if (settingsWindow.ShowDialog() == true)
            {
                Logger.Instance.MinLevel = _settings.LogLevel;
                Logger.Instance.Info("Settings saved.", "UI");
                Logger.Instance.Debug($"New LogLevel: {_settings.LogLevel}, DisguisedArchiveDetection: {_settings.EnableDisguisedArchiveDetection}", "UI");
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (_isBusy)
            {
                Logger.Instance.Warning("Engine is busy. Please wait.", "UI");
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    Logger.Instance.Debug($"Drop received: {files.Length} item(s)", "UI");
                    foreach (var p in files)
                    {
                        bool isDir = Directory.Exists(p);
                        Logger.Instance.Debug($"  → {p} ({(isDir ? "directory" : "file")})", "UI");
                    }

                    _isBusy = true;
                    ExtractionProgress.Visibility = Visibility.Visible;
                    ExtractionProgress.Value = 0;
                    try
                    {
                        var seenCanonicalInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (var path in files)
                        {
                            if (Directory.Exists(path))
                            {
                                Logger.Instance.Info($"Scanning directory: {path}", "UI");
                                var innerFiles = Directory.GetFiles(path);
                                if (innerFiles.Length == 0)
                                {
                                    Logger.Instance.Warning($"Directory is empty: {path}", "UI");
                                    continue;
                                }
                                Logger.Instance.Debug($"Directory contains {innerFiles.Length} file(s):", "UI");
                                foreach (var f in innerFiles)
                                {
                                    var fi = new FileInfo(f);
                                    Logger.Instance.Debug($"  → {fi.Name} ({fi.Length} bytes, ext={fi.Extension})", "UI");
                                }

                                var plannedFiles = BuildPlannedBatchInputs(innerFiles);
                                Logger.Instance.Info($"Planned {plannedFiles.Count} unique input(s) from directory.", "UI");
                                foreach (var planned in plannedFiles)
                                {
                                    Logger.Instance.Debug($"  → planned: {Path.GetFileName(planned)}", "UI");
                                }

                                string output = path + "_Unpacked";
                                foreach (var innerFile in plannedFiles)
                                {
                                    Logger.Instance.Debug($"Dispatching to engine: {Path.GetFileName(innerFile)}", "UI");
                                    await _engine.ProcessArchiveAsync(innerFile, output);
                                }
                            }
                            else
                            {
                                var canonical = ResolveBatchCanonicalPath(path);
                                if (!seenCanonicalInputs.Add(canonical))
                                {
                                    Logger.Instance.Info($"Skipping duplicate split-volume input: {Path.GetFileName(path)}", "UI");
                                    continue;
                                }

                                string dir = Path.GetDirectoryName(path) ?? "";
                                string name = Path.GetFileNameWithoutExtension(path);
                                string output = Path.Combine(dir, name + "_Unpacked");
                                Logger.Instance.Debug($"Dispatching to engine: {Path.GetFileName(path)}", "UI");
                                await _engine.ProcessArchiveAsync(path, output);
                            }
                        }
                        Logger.Instance.Info("All tasks completed.", "UI");
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error($"Critical Error: {ex}", "UI");
                    }
                    finally
                    {
                        ExtractionProgress.Visibility = Visibility.Collapsed;
                        _isBusy = false;
                    }
                }
            }
        }

        private static List<string> BuildPlannedBatchInputs(IEnumerable<string> filePaths)
        {
            var planned = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in filePaths)
            {
                if (Directory.Exists(path)) continue;

                var canonical = ResolveBatchCanonicalPath(path);
                if (seen.Add(canonical))
                {
                    planned.Add(path);
                }
            }

            return planned;
        }

        private static string ResolveBatchCanonicalPath(string filePath)
        {
            if (!File.Exists(filePath)) return filePath;

            string directory = Path.GetDirectoryName(filePath) ?? "";
            string name = Path.GetFileName(filePath);
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);

            if (ext.Length == 4 && int.TryParse(ext.TrimStart('.'), out int numericPart) && numericPart > 1)
            {
                string firstPartPath = Path.Combine(directory, $"{fileNameNoExt}.001");
                if (File.Exists(firstPartPath)) return firstPartPath;
            }

            if (name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
            {
                var match = System.Text.RegularExpressions.Regex.Match(name, @"^(.*)\.part(\d+)\.rar$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[2].Value, out int partNum) && partNum > 1)
                {
                    string prefix = match.Groups[1].Value;
                    int width = match.Groups[2].Value.Length;
                    string firstPartName = $"{prefix}.part{1.ToString(new string('0', width))}.rar";
                    string firstPartPath = Path.Combine(directory, firstPartName);
                    if (File.Exists(firstPartPath)) return firstPartPath;
                }
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(ext, @"^\.r\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                string firstPartPath = Path.Combine(directory, $"{fileNameNoExt}.rar");
                if (File.Exists(firstPartPath)) return firstPartPath;
            }

            return filePath;
        }
    }
}
