using System;
using System.IO;
using System.Text.Json;

namespace ProWalid.Data
{
    internal static class AppStoragePaths
    {
        public static string ResolveDatabasePath(string appFolder)
        {
            var fallbackPath = Path.Combine(appFolder, "ProWalid.db");
            return ResolveConfiguredPath(appFolder, "Database", "Path", fallbackPath);
        }

        public static string ResolveAttachmentsFolder(string appFolder)
        {
            var fallbackPath = Path.Combine(appFolder, "Attachments");
            return ResolveConfiguredPath(appFolder, "Attachments", "Path", fallbackPath);
        }

        private static string ResolveConfiguredPath(string appFolder, string sectionName, string propertyName, string fallbackPath)
        {
            var settingsPath = Path.Combine(appFolder, "appsettings.json");
            if (!File.Exists(settingsPath))
            {
                return fallbackPath;
            }

            try
            {
                using var stream = File.OpenRead(settingsPath);
                using var document = JsonDocument.Parse(stream);

                if (!document.RootElement.TryGetProperty(sectionName, out var sectionElement))
                {
                    return fallbackPath;
                }

                if (!sectionElement.TryGetProperty(propertyName, out var pathElement))
                {
                    return fallbackPath;
                }

                var configuredPath = pathElement.GetString();
                if (string.IsNullOrWhiteSpace(configuredPath))
                {
                    return fallbackPath;
                }

                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));
            }
            catch
            {
                return fallbackPath;
            }
        }
    }
}
