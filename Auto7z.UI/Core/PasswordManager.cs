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
            
            if (File.Exists(_configPath))
            {
                var lines = File.ReadAllLines(_configPath)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim());
                _passwords.AddRange(lines);
            }
            else
            {
                // Create default if not exists
                File.WriteAllLines(_configPath, new[] { "123456", "password", "1234" });
                _passwords.Add("123456");
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
            foreach (var pwd in _passwords)
            {
                if (pwd == "" || pwd == nameNoExt) continue; // Skip duplicates
                yield return pwd;
            }
        }
    }
}
