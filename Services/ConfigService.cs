using System;
using System.IO;
using HotelRatingViewer.Models;

namespace HotelRatingViewer.Services
{
    public class ConfigService
    {
        private const string ConfigFileName = "database.inf";

        public static DatabaseConfig? LoadConfig()
        {
            try
            {
                // Check if config file exists in the same directory as the executable
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                
                if (!File.Exists(configPath))
                {
                    return null;
                }

                var config = new DatabaseConfig();
                var lines = File.ReadAllLines(configPath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue; // Skip empty lines and comments

                    var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].ToLower();
                    var value = parts[1];

                    switch (key)
                    {
                        case "server":
                        case "host":
                            config.Server = value;
                            break;
                        case "port":
                            config.Port = value;
                            break;
                        case "service":
                        case "servicename":
                        case "service_name":
                            config.ServiceName = value;
                            break;
                        case "username":
                        case "user":
                            config.Username = value;
                            break;
                        case "password":
                        case "pass":
                            config.Password = value;
                            break;
                    }
                }

                // Validate required fields
                if (string.IsNullOrEmpty(config.Server) || 
                    string.IsNullOrEmpty(config.ServiceName) || 
                    string.IsNullOrEmpty(config.Username))
                {
                    return null; // Invalid config
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
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            
            if (File.Exists(configPath))
                return;

            var sampleContent = @"# Hotel Rating System Database Configuration
# Lines starting with # are comments
# Format: key=value

# Database server (hostname or IP address)
server=localhost

# Port number (default Oracle port is 1521)
port=1521

# Oracle service name
service=ORCL

# Database username
username=your_username

# Database password
password=your_password
";

            try
            {
                File.WriteAllText(configPath, sampleContent);
            }
            catch
            {
                // Silently fail if we can't create the sample
            }
        }
    }
}
