using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Auto7z.UI.Core
{
    public class PasswordManager
    {
        private readonly AppSettings _settings;
        private readonly Logger _log = Logger.Instance;

        public PasswordManager(AppSettings settings)
        {
            _settings = settings;
        }

        public IReadOnlyList<string> Passwords => _settings.Passwords;

        public IEnumerable<string> GetAttemptSequence(string fileName)
        {
            yield return "";

            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
            if (!string.IsNullOrEmpty(nameNoExt))
            {
                yield return nameNoExt;
            }

            int count = 0;
            foreach (var pwd in _settings.Passwords)
            {
                if (pwd == "" || pwd == nameNoExt) continue;
                count++;
                yield return pwd;
            }

            _log.Debug($"GetAttemptSequence for '{Path.GetFileName(fileName)}': 1 empty + 1 filename + {count} from settings", "PasswordManager");
        }
    }
}
