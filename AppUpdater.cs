using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Timers;

namespace song_box
{
    internal class AppUpdater
    {
        public class UpdateInfo
        {
            [JsonPropertyName("version")]
            public int[] Version { get; set; }
            [JsonPropertyName("zipSha256")]
            public string ZipSha256 { get; set; }
            [JsonPropertyName("zipUrl")]
            public string ZipUrl { get; set; }
        }

        private static readonly string exeDir = Utils.exeDir;
        private static readonly string updaterNewPrefix = ".new";
        private static readonly string updaterExe = Path.Combine(Utils.exeDir, "song-box-updater.exe");
        private static readonly string updaterNewExe = Path.Combine(Utils.exeDir, "song-box-updater.exe" + updaterNewPrefix);

        private readonly Utils.ILogger log;
        private readonly Config.AppUpdater cfg;

        private readonly Action closeCallback;
        private Timer _autoUpdateTimer;


        public AppUpdater(Utils.ILogger log, Action closeCallback, Config.AppUpdater cfg)
        {
            this.log = log;
            this.cfg = cfg;
            this.closeCallback = closeCallback;
            try
            {
                UpdateUpdater();
                if (this.cfg.AutoUpdate)
                {
                    CheckUpdateAndUpdate();
                    StartAutoUpdate(this.cfg.AutoUpdateEveryMinutes);
                }
            }
            catch (Exception e)
            {
                LogError("AppUpdater constructor: " + e.Message);
                LogError(e.StackTrace);
            }
        }

        public void StartAutoUpdate(int everyMins)
        {
            if (!IsConfigValid())
            {
                LogError("Invalid config, skipping autoupdating");
                return;
            }
            if (_autoUpdateTimer != null)
            {
                StopAutoUpdate();
            }
            _autoUpdateTimer = new Timer
            {
                Interval = TimeSpan.FromMinutes(everyMins).TotalMilliseconds // 1 hour
            };
            _autoUpdateTimer.Elapsed += AutoUpdateTimerElapsed;
            _autoUpdateTimer.AutoReset = true; // Repeat every interval
            _autoUpdateTimer.Enabled = true;
            LogInfo($"Auto-update enabled. Every {everyMins} minutes");
        }

        public void StopAutoUpdate()
        {
            if (_autoUpdateTimer != null)
            {
                _autoUpdateTimer.Stop();
                _autoUpdateTimer = null;
            }
            LogInfo("Auto-updating disabled");
        }

        private void AutoUpdateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            LogInfo("Auto-updating...");
            CheckUpdateAndUpdate();
        }

        public void CheckUpdateAndUpdate()
        {
            if (!IsConfigValid())
            {
                LogError("Invalid config, skipping updating");
                return;
            }

            UpdateInfo info;

            using (var client = new WebClient())
            {
                LogInfo("Downloading update info");
                if (cfg.UserAgent.Length > 0)
                {
                    client.Headers.Set("User-Agent", cfg.UserAgent);
                }
                // Download update info.
                string json = client.DownloadString(cfg.UpdateInfoUrl);
                info = JsonSerializer.Deserialize<UpdateInfo>(json);
                if (info == null)
                {
                    return;
                }
            }

            // Get current version.
            var currentVersion = System.Reflection.Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version;
            int[] currentVerArr = new[] { currentVersion.Major, currentVersion.Minor, currentVersion.Build, currentVersion.Revision };

            // Check is update required.
            if (!IsNewerVersion(currentVerArr, info.Version))
            {
                string versionStr = string.Join(".", currentVerArr);
                string updateVersionStr = string.Join(".", info.Version);
                LogInfo($"Update not required. Current version: {versionStr}, UpdateInfo version: {updateVersionStr}");
                return;
            }

            // Download new binaries, etc.

            // Download updated zip.
            LogInfo("Downloading updated zip");
            var zipPath = Path.Combine(exeDir, "song-box-update.zip");
            File.Delete(zipPath);
            using (WebClient client = new WebClient())
            {
                if (cfg.UserAgent.Length > 0)
                {
                    client.Headers.Set("User-Agent", cfg.UserAgent);
                }
                LogInfo($"Downloading: {info.ZipUrl}");
                client.DownloadFile(info.ZipUrl, zipPath);
            }

            // Compare hashes.
            if (!Utils.CheckFileHash(zipPath, info.ZipSha256))
            {
                File.Delete(zipPath);
                LogError("Hashes mismatch!");
                return;
            }

            // Extract zip and delete.
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // Skip dirs.
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    // Add .new to name
                    var newFileName = entry.Name + updaterNewPrefix;
                    var destinationPath = Path.Combine(exeDir, newFileName);

                    // Copy and rename.
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
            File.Delete(zipPath);

            // Run updater.
            LogInfo("Update ready. Run updater...");

            var startInfo = new ProcessStartInfo
            {
                FileName = updaterExe,
                UseShellExecute = false
            };
            Process.Start(startInfo);

            // Close current process.
            closeCallback?.Invoke();
        }

        private bool IsNewerVersion(int[] current, int[] incoming)
        {
            for (int i = 0; i < Math.Min(current.Length, incoming.Length); i++)
            {
                if (incoming[i] > current[i]) return true;
                if (incoming[i] < current[i]) return false;
            }
            return incoming.Length > current.Length;
        }

        private void LogInfo(string msg)
        {
            log.Info($"[AppUpdater] {msg}");
        }

        private void LogError(string msg)
        {
            log.Error($"[AppUpdater] {msg}");
        }

        private bool IsConfigValid()
        {
            if (cfg.AutoUpdateEveryMinutes < 5 || cfg.UpdateInfoUrl.Length < 1)
            {
                return false;
            }
            return true;
        }

        private void UpdateUpdater()
        {
            if (!File.Exists(updaterNewExe))
            {
                LogInfo("Updated updater not found");
                return;
            }
            LogInfo("Updating updater...");
            File.Delete(updaterExe);
            File.Move(updaterNewExe, updaterExe);
            LogInfo("Updater... updated. Bruh");
        }
    }
}
