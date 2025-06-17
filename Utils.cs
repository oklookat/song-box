using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace song_box
{
    internal class Utils
    {
        public interface ILogger
        {
            void Debug(string message);
            void Info(string message);
            void Warn(string message);
            void Error(string message);
        }

        public static readonly string exeDir = AppDomain.CurrentDomain.BaseDirectory;

        public static bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public static void FindUnzipExe(FileStream zipStream, string findExeName, string unzipPath)
        {
            using (ZipArchive archive = new ZipArchive(zipStream))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.Name.EndsWith(findExeName))
                    {
                        continue;
                    }
                    using (Stream exeFromStream = entry.Open())
                    {
                        using (FileStream newSingBoxExe = File.Create(unzipPath))
                        {
                            exeFromStream.CopyTo(newSingBoxExe);
                        }
                    }
                }
            }
        }

        public static bool CompareFileHashes(string path1, string path2)
        {
            using (FileStream fs1 = File.OpenRead(path1))
            using (FileStream fs2 = File.OpenRead(path2))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash1 = sha.ComputeHash(fs1);
                byte[] hash2 = sha.ComputeHash(fs2);

                if (hash1.Length != hash2.Length)
                    return false;

                for (int i = 0; i < hash1.Length; i++)
                    if (hash1[i] != hash2[i])
                        return false;

                return true;
            }
        }

        public static bool CheckFileHash(string filePath, string expectedHash)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл не найден", filePath);

            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hashBytes = sha.ComputeHash(stream);
                string fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                return string.Equals(fileHash, expectedHash.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
