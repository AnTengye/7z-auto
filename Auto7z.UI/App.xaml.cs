using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Auto7z.UI.Core;

namespace Auto7z.UI
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length > 0)
            {
                await RunHeadlessAsync(e.Args);
                Shutdown();
            }
            else
            {
                new MainWindow().Show();
            }
        }

        private async Task RunHeadlessAsync(string[] args)
        {
            AttachConsole(-1);
            
            Console.WriteLine("Auto7z CLI Mode Started.");
            var settings = new AppSettings();
            settings.Load();
            var engine = new ExtractorEngine(settings);
            engine.Log += msg => Console.WriteLine($"[Log] {msg}");

            foreach (var arg in args)
            {
                if (File.Exists(arg))
                {
                    string dir = Path.GetDirectoryName(arg) ?? "";
                    string name = Path.GetFileNameWithoutExtension(arg);
                    string output = Path.Combine(dir, name + "_Unpacked");
                    
                    Console.WriteLine($"Processing: {arg} -> {output}");
                    try
                    {
                        await engine.ProcessArchiveAsync(arg, output);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing {arg}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"File not found: {arg}");
                }
            }
            Console.WriteLine("Done.");
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
    }
}
