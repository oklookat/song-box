using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace song_box
{
    internal class Config
    {
        private static readonly string configPath = Path.Combine(Utils.exeDir, "song-box.json");

        public class AppConfig
        {
            public static AppConfig Read()
            {
                if (!Utils.FileExists(configPath))
                {
                    // Create default config
                    var config = new AppConfig();

                    // Save it to file
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string defaultJson = JsonSerializer.Serialize(config, options);
                    File.WriteAllText(configPath, defaultJson);
                }

                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<AppConfig>(json);
            }

            [JsonPropertyName("appUpdater")]
            public AppUpdater AppUpdater { get; set; } = new AppUpdater();

            [JsonPropertyName("singBox")]
            public SingBox SingBox { get; set; } = new SingBox();

            [JsonPropertyName("singBoxConfig")]
            public SingBoxConfig SingBoxConfig { get; set; } = new SingBoxConfig();
        }

        public class AppUpdater
        {
            [JsonPropertyName("autoUpdate")]
            public bool AutoUpdate { get; set; } = true;
            [JsonPropertyName("autoUpdateEveryMinutes")]
            public int AutoUpdateEveryMinutes { get; set; } = 60;
            [JsonPropertyName("userAgent")]
            public string UserAgent { get; set; } = "";

            [JsonPropertyName("updateInfoUrl")]
            public string UpdateInfoUrl { get; set; } = "";
        }

        public class SingBox
        {
            [JsonPropertyName("downloadUrl")]
            public string DownloadUrl { get; set; } = "";
        }

        public class SingBoxConfig
        {
            [JsonPropertyName("autoUpdate")]
            public bool AutoUpdate { get; set; } = true;

            [JsonPropertyName("autoUpdateEveryMinutes")]
            public int AutoUpdateEveryMinutes { get; set; } = 60;

            [JsonPropertyName("userAgent")]
            public string UserAgent { get; set; } = "";

            [JsonPropertyName("downloadUrl")]
            public string DownloadUrl { get; set; } = "";
        }
    }
}
