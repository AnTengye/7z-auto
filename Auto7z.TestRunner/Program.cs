using System;
using System.IO;
using System.Threading.Tasks;
using Auto7z.UI.Core;

namespace Auto7z.TestRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("TestRunner Starting...");
            
            // Hardcoded test path
            string archivePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\test\test.7z"));
            string outputDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\test\test_Unpacked"));

            // Ensure output dir is clean
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);

            if (!File.Exists(archivePath))
            {
                Console.WriteLine($"Error: Archive not found at {archivePath}");
                // Fallback attempt to find it simply
                 if (File.Exists("test/test.7z")) archivePath = Path.GetFullPath("test/test.7z");
                 else if (File.Exists("../test/test.7z")) archivePath = Path.GetFullPath("../test/test.7z");
            }

            Console.WriteLine($"Processing: {archivePath}");
            Console.WriteLine($"Output to: {outputDir}");

            var engine = new ExtractorEngine();
            engine.Log += msg => Console.WriteLine($"[Engine] {msg}");

            try
            {
                await engine.ProcessArchiveAsync(archivePath, outputDir);
                Console.WriteLine("Success. Processing finished.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
            }
        }
    }
}
