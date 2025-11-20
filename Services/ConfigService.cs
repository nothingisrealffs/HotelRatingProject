using System;
using System.IO;
using HotelRatingViewer.Models;

namespace HotelRatingViewer.Services
{
    public static class ConfigService
    {
        private const string ConfigFileName = "db_config.inf";
        public static DatabaseConfig? LoadConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                if (!File.Exists(configPath)) return null;

                var config = new DatabaseConfig();
                var lines = File.ReadAllLines(configPath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;

                    var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length != 2) continue;

                    var key = parts[0].ToLowerInvariant();
                    var value = parts[1];

                    switch (key)
                    {
                        case "server": config.Server = value; break;
                        case "port": config.Port = value; break;
                        case "service": config.ServiceName = value; break;
                        case "user": config.Username = value; break;
                        case "password": config.Password = value; break;
                    }
                }

                return config;
            }
            catch
            {
                return null;
            }
        }
        public static void CreateSampleConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                if (!File.Exists(configPath))
                {
                    var content = """
                        # Hotel Rating System Database Configuration
                        # Rename or edit this file to auto-fill login credentials
                        
                        Server=localhost
                        Port=1521
                        Service=XE
                        User=placeholder
                        Password=secret
                        """;
                    
                    File.WriteAllText(configPath, content);
                }
            }
            catch
            {
            }
        }
    }
}
