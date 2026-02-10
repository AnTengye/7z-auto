using System;
using System.IO;

namespace Auto7z.UI.Core
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public class Logger : IDisposable
    {
        private static Logger? _instance;
        private static readonly object _lock = new();

        private StreamWriter? _fileWriter;
        private string? _logFilePath;
        private bool _disposed;

        public LogLevel MinLevel { get; set; } = LogLevel.Info;
        public event Action<string, LogLevel>? OnLog;

        public string? LogFilePath => _logFilePath;

        public static Logger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new Logger();
                    }
                }
                return _instance;
            }
        }

        private Logger()
        {
            InitFileLog();
        }

        private void InitFileLog()
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                _logFilePath = Path.Combine(logDir, $"Auto7z_{timestamp}.log");

                _fileWriter = new StreamWriter(_logFilePath, append: false, encoding: System.Text.Encoding.UTF8)
                {
                    AutoFlush = true
                };

                _fileWriter.WriteLine($"=== Auto7z Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                _fileWriter.WriteLine();
            }
            catch
            {
                // 文件日志初始化失败不应阻止程序运行
                _fileWriter = null;
                _logFilePath = null;
            }
        }

        public void Debug(string message, string? source = null)
        {
            Log(LogLevel.Debug, message, source);
        }

        public void Info(string message, string? source = null)
        {
            Log(LogLevel.Info, message, source);
        }

        public void Warning(string message, string? source = null)
        {
            Log(LogLevel.Warning, message, source);
        }

        public void Error(string message, string? source = null)
        {
            Log(LogLevel.Error, message, source);
        }

        public void Error(Exception ex, string? source = null)
        {
            Log(LogLevel.Error, $"{ex.GetType().Name}: {ex.Message}", source);
        }

        private void Log(LogLevel level, string message, string? source)
        {
            if (level < MinLevel) return;

            string prefix = level switch
            {
                LogLevel.Debug => "[DEBUG]",
                LogLevel.Info => "[INFO]",
                LogLevel.Warning => "[WARN]",
                LogLevel.Error => "[ERROR]",
                _ => ""
            };

            string sourcePrefix = string.IsNullOrEmpty(source) ? "" : $"[{source}] ";
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string fileLine = $"{timestamp} {prefix} {sourcePrefix}{message}";

            string uiPrefix = level == LogLevel.Info ? "" : prefix;
            string uiMessage = string.IsNullOrEmpty(uiPrefix)
                ? $"{sourcePrefix}{message}"
                : $"{uiPrefix} {sourcePrefix}{message}";

            lock (_lock)
            {
                try
                {
                    _fileWriter?.WriteLine(fileLine);
                }
                catch { }
            }

            OnLog?.Invoke(uiMessage, level);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                try
                {
                    _fileWriter?.WriteLine();
                    _fileWriter?.WriteLine($"=== Auto7z Log Ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    _fileWriter?.Flush();
                    _fileWriter?.Dispose();
                }
                catch { }
                _fileWriter = null;
            }
        }
    }
}
