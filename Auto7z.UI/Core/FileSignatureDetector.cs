using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SevenZip;

namespace Auto7z.UI.Core
{
    /// <summary>
    /// Detects real file format by reading magic bytes (file signature).
    /// Used to identify disguised archives (e.g., .mp4 that is actually .rar).
    /// </summary>
    public class FileSignatureDetector
    {
        // Magic byte signatures for common archive formats
        // Format: (signature bytes, offset, format)
        private static readonly List<(byte[] Signature, int Offset, InArchiveFormat Format)> _signatures = new()
        {
            // 7z: 37 7A BC AF 27 1C
            (new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }, 0, InArchiveFormat.SevenZip),
            
            // RAR5: 52 61 72 21 1A 07 01 00
            (new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 }, 0, InArchiveFormat.Rar),
            
            // RAR4: 52 61 72 21 1A 07 00
            (new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }, 0, InArchiveFormat.Rar),
            
            // ZIP: 50 4B 03 04 (normal) or 50 4B 05 06 (empty) or 50 4B 07 08 (spanned)
            (new byte[] { 0x50, 0x4B, 0x03, 0x04 }, 0, InArchiveFormat.Zip),
            (new byte[] { 0x50, 0x4B, 0x05, 0x06 }, 0, InArchiveFormat.Zip),
            (new byte[] { 0x50, 0x4B, 0x07, 0x08 }, 0, InArchiveFormat.Zip),
            
            // GZip: 1F 8B
            (new byte[] { 0x1F, 0x8B }, 0, InArchiveFormat.GZip),
            
            // BZip2: 42 5A 68
            (new byte[] { 0x42, 0x5A, 0x68 }, 0, InArchiveFormat.BZip2),
            
            // XZ: FD 37 7A 58 5A 00
            (new byte[] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 }, 0, InArchiveFormat.XZ),
            
            // TAR: "ustar" at offset 257
            (new byte[] { 0x75, 0x73, 0x74, 0x61, 0x72 }, 257, InArchiveFormat.Tar),
            
            // ISO: "CD001" at offset 32769 (too far, we'll skip deep check for ISO)
            
            // CAB: 4D 53 43 46 ("MSCF")
            (new byte[] { 0x4D, 0x53, 0x43, 0x46 }, 0, InArchiveFormat.Cab),
            
            // ARJ: 60 EA
            (new byte[] { 0x60, 0xEA }, 0, InArchiveFormat.Arj),
            
            // LZH: -lh (various, check for common pattern)
            (new byte[] { 0x2D, 0x6C, 0x68 }, 2, InArchiveFormat.Lzh),
        };

        // Extension to expected format mapping (for mismatch detection)
        private static readonly Dictionary<string, InArchiveFormat[]> _extensionFormats = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".7z", new[] { InArchiveFormat.SevenZip } },
            { ".rar", new[] { InArchiveFormat.Rar } },
            { ".zip", new[] { InArchiveFormat.Zip } },
            { ".tar", new[] { InArchiveFormat.Tar } },
            { ".gz", new[] { InArchiveFormat.GZip } },
            { ".tgz", new[] { InArchiveFormat.GZip } },
            { ".bz2", new[] { InArchiveFormat.BZip2 } },
            { ".xz", new[] { InArchiveFormat.XZ } },
            { ".cab", new[] { InArchiveFormat.Cab } },
            { ".iso", new[] { InArchiveFormat.Iso } },
        };

        /// <summary>
        /// Detects the real archive format by reading file signature.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Detected format, or null if not an archive</returns>
        public InArchiveFormat? DetectFormat(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 2) return null; // Too small

                // Read header bytes (enough for most signatures + TAR check)
                int bytesToRead = Math.Min(512, (int)fileInfo.Length);
                byte[] header = new byte[bytesToRead];

                using (var fs = File.OpenRead(filePath))
                {
                    fs.Read(header, 0, bytesToRead);
                }

                // Check each signature
                foreach (var (signature, offset, format) in _signatures)
                {
                    if (offset + signature.Length > header.Length) continue;

                    bool match = true;
                    for (int i = 0; i < signature.Length; i++)
                    {
                        if (header[offset + i] != signature[i])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match) return format;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if a file is a disguised archive (extension doesn't match content).
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="detectedFormat">Output: the real format if disguised</param>
        /// <returns>True if file is a disguised archive</returns>
        public bool IsDisguisedArchive(string filePath, out InArchiveFormat? detectedFormat)
        {
            detectedFormat = DetectFormat(filePath);
            
            if (detectedFormat == null) return false;

            string ext = Path.GetExtension(filePath);
            
            // If extension matches expected format, it's not disguised
            if (_extensionFormats.TryGetValue(ext, out var expectedFormats))
            {
                if (expectedFormats.Contains(detectedFormat.Value))
                {
                    return false;
                }
            }

            // Extension is something else (like .mp4) but content is archive
            return true;
        }

        /// <summary>
        /// Gets a friendly name for the archive format.
        /// </summary>
        public static string GetFormatName(InArchiveFormat format)
        {
            return format switch
            {
                InArchiveFormat.SevenZip => "7z",
                InArchiveFormat.Rar => "RAR",
                InArchiveFormat.Zip => "ZIP",
                InArchiveFormat.GZip => "GZip",
                InArchiveFormat.BZip2 => "BZip2",
                InArchiveFormat.Tar => "TAR",
                InArchiveFormat.XZ => "XZ",
                InArchiveFormat.Cab => "CAB",
                InArchiveFormat.Iso => "ISO",
                _ => format.ToString()
            };
        }
    }
}
