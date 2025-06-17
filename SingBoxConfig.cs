using System;
using System.IO;
using System.Net;
using System.Timers;

namespace song_box
{
    internal class SingBoxConfig
    {
        public static readonly string configPath = Path.Combine(Utils.exeDir, "sing-box.json");
        public static readonly string downloadedConfigPath = Path.Combine(Utils.exeDir, "sing-box-temp.json");

        private Timer _autoUpdateTimer;
        private readonly Config.SingBoxConfig cfg;

        private readonly Utils.ILogger log;

        public SingBoxConfig(Utils.ILogger log, Config.SingBoxConfig cfg)
        {
            this.log = log;
            this.cfg = cfg;
            UpdateConfig();
            if (this.cfg.AutoUpdate)
            {
                StartAutoUpdate(this.cfg.AutoUpdateEveryMinutes);
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
                Interval = TimeSpan.FromMinutes(everyMins).TotalMilliseconds
            };
            _autoUpdateTimer.Elapsed += AutoUpdateTimerElapsed;
            _autoUpdateTimer.AutoReset = true;
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
            UpdateConfig();
        }

        public void UpdateConfig()
        {
            if (!IsConfigValid())
            {
                LogError("Invalid config, skipping updating");
                return;
            }
            try
            {
                LogInfo("Updating...");
                DownloadCompareReplaceConfig();
            }
            catch (System.Exception ex)
            {
                log.Error(ex.Message);
            }
        }


        private void DownloadCompareReplaceConfig()
        {
            DownloadConfig();
            if (!Utils.FileExists(configPath))
            {
                // No active config, just set downloaded config.
                LogInfo("No active config, set new");
                ReplaceDownloadedConfig();
                return;
            }
            var sameConfigs = Utils.CompareFileHashes(configPath, downloadedConfigPath);
            if (sameConfigs)
            {
                LogInfo("Config no changed, skip");
                File.Delete(downloadedConfigPath);
                return;
            }
            LogInfo("New version of config, replace");
            ReplaceDownloadedConfig();
        }

        private void DownloadConfig()
        {
            LogInfo($"User-Agent: {cfg.UserAgent}");
            LogInfo($"Upstream: {cfg.DownloadUrl}");
            using (var client = new WebClient())
            {
                client.Headers.Set("User-Agent", cfg.UserAgent);
                client.DownloadFile(cfg.DownloadUrl, downloadedConfigPath);
            }
        }

        private void ReplaceDownloadedConfig()
        {
            if (!Utils.FileExists(downloadedConfigPath))
            {
                return;
            }
            File.Delete(configPath);
            File.Move(downloadedConfigPath, configPath);
        }

        private void LogInfo(string msg)
        {
            log.Info($"[SingBoxConfig] {msg}");
        }

        private void LogError(string msg)
        {
            log.Error($"[SingBoxConfig] {msg}");
        }

        private bool IsConfigValid()
        {
            if (cfg.AutoUpdateEveryMinutes < 5 || cfg.DownloadUrl.Length < 1)
            {
                return false;
            }
            return true;
        }
    }
}
