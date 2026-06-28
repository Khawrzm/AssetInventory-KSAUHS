using System.IO;
using System.Text.Json;

namespace AssetInventory.Core
{
    public class Config { public string DatabasePath { get; set; } = "AssetInventory.db"; }

    public static class ConfigService
    {
        private const string ConfigFile = "config.json";
        public static Config Load()
        {
            if (!File.Exists(ConfigFile))
            {
                var defaultConfig = new Config();
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
                return defaultConfig;
            }
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigFile)) ?? new Config();
        }
    }
}
