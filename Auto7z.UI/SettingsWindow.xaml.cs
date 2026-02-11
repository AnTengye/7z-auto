using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Auto7z.UI.Core;

namespace Auto7z.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadSettings();
        }

        private void LoadSettings()
        {
            AutoDetectCheckBox.IsChecked = _settings.AutoDetectUnknownExtensions;

            PasswordsTextBox.Text = string.Join(Environment.NewLine, _settings.Passwords);

            ExtensionsTextBox.Text = string.Join(Environment.NewLine, _settings.DisguisedExtensions);

            // Load LogLevel
            LogLevelComboBox.SelectedItem = LogLevelComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => (string)item.Content == _settings.LogLevel.ToString());
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.AutoDetectUnknownExtensions = AutoDetectCheckBox.IsChecked == true;

            var passwords = PasswordsTextBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s));
            _settings.SetPasswords(passwords);

            var extensions = ExtensionsTextBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s) && !s.StartsWith("#"));
            _settings.SetDisguisedExtensions(extensions);

            // Save LogLevel
            if (LogLevelComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (Enum.TryParse(selectedItem.Content.ToString(), out LogLevel newLogLevel))
                {
                    _settings.LogLevel = newLogLevel;
                }
            }

            _settings.Save();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
