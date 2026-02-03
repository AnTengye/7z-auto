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
            _engine = new ExtractorEngine(_settings);
            _engine.Log += OnLog;
            Log("Auto7z initialized. Waiting for files...");
            Log($"Current Directory: {AppDomain.CurrentDomain.BaseDirectory}");
            Log("Ensure 7z.dll is present in this folder!");
        }

        private void OnLog(string message)
        {
            Dispatcher.Invoke(() => Log(message));
        }

        private void Log(string message)
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogBox.ScrollToEnd();
            StatusText.Text = message;
        }

        private void DisguisedCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _settings.EnableDisguisedArchiveDetection = DisguisedCheckBox.IsChecked == true;
            Log(_settings.EnableDisguisedArchiveDetection 
                ? "Disguised archive detection: ON" 
                : "Disguised archive detection: OFF");
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
                Log("⚠️ Engine is busy. Please wait.");
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    _isBusy = true;
                    try
                    {
                        foreach (var file in files)
                        {
                            if (Directory.Exists(file)) continue;

                            string dir = Path.GetDirectoryName(file) ?? "";
                            string name = Path.GetFileNameWithoutExtension(file);
                            string output = Path.Combine(dir, name + "_Unpacked");

                            await _engine.ProcessArchiveAsync(file, output);
                        }
                        Log("✅ All tasks completed.");
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Critical Error: {ex.Message}");
                    }
                    finally
                    {
                        _isBusy = false;
                    }
                }
            }
        }
    }
}
