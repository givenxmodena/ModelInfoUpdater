using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ModelInfoUpdater.Logging
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Simple file-based logger shared by the add-in and updater.
    /// Writes to %LOCALAPPDATA%\ModelInfoUpdater\Logs\<name>_yyyyMMdd_HHmmss.log
    /// and also forwards messages to Debug output for development.
    /// </summary>
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private static bool _initialized;
        private static string? _logFilePath;
        private const int MaxLogFiles = 50;
        private const int MaxLogAgeDays = 30;

        /// <summary>
        /// Initializes the logger with a logical application name used in the file prefix.
        /// Safe to call multiple times; only the first call wins.
        /// </summary>
        public static void Initialize(string appLogName)
        {
            if (_initialized)
                return;

            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logDir = Path.Combine(localAppData, "ModelInfoUpdater", "Logs");
                Directory.CreateDirectory(logDir);

                CleanupOldLogs(logDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _logFilePath = Path.Combine(logDir, $"{appLogName}_{timestamp}.log");
                _initialized = true;

                LogInternal(LogLevel.Info, "Startup", $"Logger initialized. Log file: {_logFilePath}", null);
            }
            catch
            {
                // Never throw from logging; if initialization fails, logging becomes no-op.
            }
        }

        private static void CleanupOldLogs(string logDir)
        {
            try
            {
                var files = Directory.GetFiles(logDir, "*.log");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                // Delete by age
                DateTime cutoff = DateTime.Now.AddDays(-MaxLogAgeDays);
                foreach (string file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.CreationTimeUtc < cutoff.ToUniversalTime())
                        {
                            info.Delete();
                        }
                    }
                    catch
                    {
                        // Ignore cleanup failures
                    }
                }

                // Enforce max file count (keep newest)
                files = Directory.GetFiles(logDir, "*.log");
                if (files.Length > MaxLogFiles)
                {
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    int toDelete = files.Length - MaxLogFiles;
                    for (int i = 0; i < toDelete; i++)
                    {
                        try { File.Delete(files[i]); } catch { }
                    }
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        public static void Log(LogLevel level, string category, string message, Exception? exception = null)
        {
            LogInternal(level, category, message, exception);
        }

        public static void LogException(string category, string context, Exception ex)
        {
            string message = $"{context} failed: {ex.GetType().Name}: {ex.Message}";
            LogInternal(LogLevel.Error, category, message, ex);
        }

        public static void LogEnvironment(string category, string appVersion, string extra = "")
        {
            try
            {
                var process = Process.GetCurrentProcess();
                string runtime = Environment.Version.ToString();
                string os = Environment.OSVersion.VersionString;
                string user = Environment.UserName;
                string machine = Environment.MachineName;
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;

                string message =
                    $"AppVersion={appVersion}, Process={process.ProcessName}({process.Id}), " +
                    $"Runtime={runtime}, OS={os}, User={user}, Machine={machine}, " +
                    $"AssemblyLocation={assemblyLocation}";

                if (!string.IsNullOrWhiteSpace(extra))
                {
                    message += ", " + extra;
                }

                LogInternal(LogLevel.Info, category, message, null);
            }
            catch
            {
                // Ignore environment logging failures
            }
        }

        private static void LogInternal(LogLevel level, string category, string message, Exception? exception)
        {
            try
            {
                if (!_initialized)
                {
                    Initialize("ModelInfoUpdater");
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string line = $"{timestamp} [{level}] [{category}] {message}";

                if (exception != null)
                {
                    line += Environment.NewLine + exception.ToString();
                }

                lock (_lock)
                {
                    if (!string.IsNullOrEmpty(_logFilePath))
                    {
                        File.AppendAllText(_logFilePath, line + Environment.NewLine);
                    }
                }

                Debug.WriteLine($"[ModelInfoUpdater] {line}");
            }
            catch
            {
                // Swallow all logging failures
            }
        }
    }
}

