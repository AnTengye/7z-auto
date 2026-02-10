using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Auto7z.UI.Core
{
    public class PasswordManager
    {
        private List<string> _passwords = new List<string>();
        private readonly string _configPath = "passwords.txt";
        private readonly Logger _log = Logger.Instance;

        public PasswordManager()
        {
            LoadPasswords();
        }

        public IReadOnlyList<string> Passwords => _passwords;

        private void LoadPasswords()
        {
            _passwords.Clear();
            // Default priorities
            _passwords.Add(""); // No password
            
            string fullPath = Path.GetFullPath(_configPath);
            if (File.Exists(_configPath))
            {
                var lines = File.ReadAllLines(_configPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim());
                _passwords.AddRange(lines);
                _log.Debug($"Loaded {_passwords.Count - 1} password(s) from {fullPath}", "PasswordManager");
            }
            else
            {
                // Create default if not exists
                File.WriteAllLines(_configPath, new[] { "123456", "password", "1234" });
                _passwords.Add("123456");
                _log.Info($"Created default passwords.txt at {fullPath}", "PasswordManager");
            }
        }

        public IEnumerable<string> GetAttemptSequence(string fileName)
        {
            // Priority 1: No password
            yield return "";

            // Priority 2: Filename as password (often used in archives)
            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
            if (!string.IsNullOrEmpty(nameNoExt))
            {
                yield return nameNoExt;
            }

            // Priority 3: Loaded list
            int count = 0;
            foreach (var pwd in _passwords)
            {
                if (pwd == "" || pwd == nameNoExt) continue; // Skip duplicates
                count++;
                yield return pwd;
            }

            _log.Debug($"GetAttemptSequence for '{Path.GetFileName(fileName)}': 1 empty + 1 filename + {count} from list", "PasswordManager");
        }
    }
}
