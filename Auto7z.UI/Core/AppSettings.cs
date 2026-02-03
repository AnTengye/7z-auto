using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Auto7z.UI.Core
{
    public class AppSettings
    {
        private static readonly string ConfigDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string DisguisedExtensionsFile = Path.Combine(ConfigDir, "disguised_extensions.txt");

        public bool EnableDisguisedArchiveDetection { get; set; } = false;

        public HashSet<string> DisguisedExtensions { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public void Load()
        {
            LoadDisguisedExtensions();
        }

        private void LoadDisguisedExtensions()
        {
            DisguisedExtensions.Clear();

            if (!File.Exists(DisguisedExtensionsFile))
            {
                CreateDefaultDisguisedExtensionsFile();
            }

            try
            {
                var lines = File.ReadAllLines(DisguisedExtensionsFile);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                    var ext = trimmed.StartsWith(".") ? trimmed : "." + trimmed;
                    DisguisedExtensions.Add(ext);
                }
            }
            catch
            {
                DisguisedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".avi", ".jpg", ".png", ".gif" };
            }
        }

        private void CreateDefaultDisguisedExtensionsFile()
        {
            var defaultContent = @"# Disguised Archive Extensions
# Extensions listed here will be checked for hidden archive content
# when 'Detect Disguised Archives' option is enabled.
# One extension per line. Lines starting with # are comments.

.mp4
.mkv
.avi
.mov
.wmv
.jpg
.jpeg
.png
.gif
.bmp
.pdf
";
            try
            {
                File.WriteAllText(DisguisedExtensionsFile, defaultContent);
            }
            catch { }
        }

        public bool IsDisguisedExtension(string extension)
        {
            if (!EnableDisguisedArchiveDetection) return false;
            return DisguisedExtensions.Contains(extension);
        }
    }
}
