using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WinPowerTools.Services
{
    public class JunkCategoryModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public long TotalBytes { get; set; }
        public string FormattedSize => FormatBytes(TotalBytes);
        public int ItemCount { get; set; }
        public List<string> Paths { get; set; } = new();
        public bool IsSelected { get; set; } = true;

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    public static class JunkCleanerService
    {
        public static async Task<List<JunkCategoryModel>> ScanJunkAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                var categories = new List<JunkCategoryModel>();

                // 1. iPhone / iTunes Yerel Yedekleri
                progress?.Report("iPhone yerel yedekleri taranıyor...");
                var iosBackup = new JunkCategoryModel
                {
                    Id = "ios_backup",
                    Name = "iPhone / iTunes Yedekleri",
                    Description = "Bilgisayara bağlanan telefonların gigabaytlarca yer tutan eski yedekleri"
                };

                string p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Apple Computer\MobileSync\Backup");
                string p2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Apple\MobileSync\Backup");
                ScanFolder(p1, iosBackup);
                ScanFolder(p2, iosBackup);
                if (iosBackup.TotalBytes > 0) categories.Add(iosBackup);

                // 2. Geliştirici Paket Önbellekleri (pip, nuget, gradle)
                progress?.Report("pip ve nuget önbellekleri taranıyor...");
                var devCaches = new JunkCategoryModel
                {
                    Id = "dev_cache",
                    Name = "Python & .NET Paket Önbellekleri",
                    Description = "pip cache ve nuget lokal paket depoları"
                };

                string pipCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"pip\cache");
                string nugetCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".nuget\packages");
                string gradleCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".gradle\caches");

                ScanFolder(pipCache, devCaches);
                ScanFolder(nugetCache, devCaches);
                ScanFolder(gradleCache, devCaches);
                if (devCaches.TotalBytes > 0) categories.Add(devCaches);

                // 3. DirectX & GPU Gölgelendirici (Shader) Önbellekleri
                progress?.Report("GPU ve Shader önbellekleri taranıyor...");
                var shaderCache = new JunkCategoryModel
                {
                    Id = "shader_cache",
                    Name = "DirectX & GPU Shader Önbellekleri",
                    Description = "Oyunlar ve grafik motorlarının biriken geçici derleme dosyaları"
                };

                string d3dCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache");
                string nvCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NVIDIA\DXCache");
                ScanFolder(d3dCache, shaderCache);
                ScanFolder(nvCache, shaderCache);
                if (shaderCache.TotalBytes > 0) categories.Add(shaderCache);

                // 4. Windows Kullanıcı Temp Klasörü
                progress?.Report("Windows geçici dosyaları taranıyor...");
                var tempJunk = new JunkCategoryModel
                {
                    Id = "temp_files",
                    Name = "Windows Kullanıcı Geçici Dosyaları (Temp)",
                    Description = "Kapanmış uygulamalardan kalan artık dosyalar"
                };
                string tempPath = Path.GetTempPath();
                ScanFolder(tempPath, tempJunk, maxFiles: 5000);
                if (tempJunk.TotalBytes > 0) categories.Add(tempJunk);

                return categories;
            });
        }

        private static void ScanFolder(string path, JunkCategoryModel cat, int maxFiles = int.MaxValue)
        {
            if (!Directory.Exists(path)) return;
            try
            {
                var dir = new DirectoryInfo(path);
                int count = 0;
                foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        cat.TotalBytes += file.Length;
                        cat.Paths.Add(file.FullName);
                        count++;
                        cat.ItemCount = count;
                        if (count >= maxFiles) break;
                    }
                    catch { }
                }
            }
            catch { }
        }

        public static async Task<long> CleanCategoriesAsync(IEnumerable<JunkCategoryModel> selectedCategories)
        {
            return await Task.Run(() =>
            {
                long cleanedBytes = 0;
                foreach (var cat in selectedCategories)
                {
                    foreach (var path in cat.Paths)
                    {
                        try
                        {
                            if (File.Exists(path))
                            {
                                var fi = new FileInfo(path);
                                long len = fi.Length;
                                fi.Delete();
                                cleanedBytes += len;
                            }
                        }
                        catch { }
                    }
                }
                return cleanedBytes;
            });
        }
    }
}
