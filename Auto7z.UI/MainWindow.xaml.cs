using System;
using System.IO;
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
                                string output = path + "_Unpacked";
                                foreach (var innerFile in innerFiles)
                                {
                                    Logger.Instance.Debug($"Dispatching to engine: {Path.GetFileName(innerFile)}", "UI");
                                    await _engine.ProcessArchiveAsync(innerFile, output);
                                }
                            }
                            else
                            {
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
    }
}
