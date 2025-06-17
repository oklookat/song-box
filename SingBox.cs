using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace song_box
{
    internal class SingBox : IDisposable
    {
        private static readonly string exePath = Path.Combine(Utils.exeDir, "sing-box.exe");
        private static readonly string zipPath = Path.Combine(Utils.exeDir, "sing-box.zip");
        private readonly string configPath;

        private bool disposed = false;
        private readonly Config.SingBox cfg;
        private readonly Utils.ILogger log;

        private Process process;


        public SingBox(Utils.ILogger log, Config.SingBox cfg, string configPath)
        {
            this.log = log;
            this.cfg = cfg;
            this.configPath = configPath;
            try
            {
                if (IsInstalled())
                {
                    LogInfo("Already installed");
                }
                else
                {
                    Download(cfg.DownloadUrl);
                }
            }
            catch (System.Exception ex)
            {
                LogError(ex.Message);
            }
        }

        ~SingBox()
        {
            ExitDispose();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                ExitDispose();
                disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        private void ExitDispose()
        {
            // Exit from sing-box process
            if (process != null && !process.HasExited)
            {
                try
                {
                    process.Kill();
                    process.Dispose();
                }
                catch { }
            }
        }

        public void Start()
        {
            if (!IsInstalled())
            {
                LogError("Bad start: sing-box not installed");
                return;
            }

            LogInfo("Starting...");

            var fileName = "sing-box.exe";
            var arguments = $"-D {Utils.exeDir} -c {configPath} run";

            LogInfo($"Something like: {fileName} {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    log.Info(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    log.Error(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            LogInfo("Started!");
        }

        private bool IsInstalled()
        {
            return Utils.FileExists(exePath);
        }

        /** url must be an zip archive with 'sing-box.exe' file. */
        private void Download(string url)
        {
            if (url.Length < 1)
            {
                LogError("Bad download link, skipping downloading");
                return;
            }
            using (var client = new WebClient())
            {
                LogInfo("Downloading: " + url);
                client.DownloadFile(url, zipPath);
            }

            using (FileStream singBoxZip = File.Open(zipPath, FileMode.Open, FileAccess.Read))
            {
                LogInfo("Unzipping: " + singBoxZip);
                Utils.FindUnzipExe(singBoxZip, "sing-box.exe", exePath);
            }

            File.Delete(zipPath);
        }

        private void LogInfo(string msg)
        {
            log.Info($"[SingBox] {msg}");
        }

        private void LogError(string msg)
        {
            log.Error($"[SingBox] {msg}");
        }
    }

}
