using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using ProWalid.Data;
using ProWalid.Services;
using System;
using System.IO;
using System.Text.Json;

namespace ProWalid
{
    public partial class App : Application
    {
        private Window m_window;
        private DispatcherTimer _backgroundSyncTimer;

        public static Window MainWindow { get; private set; }
        public static SyncService SharedSyncService { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            MainWindow = m_window;
            m_window.Activate();

            InitializeSharedSyncService();
            StartBackgroundSyncTimer();
        }

        private void InitializeSharedSyncService()
        {
            try
            {
                var appFolder = AppDomain.CurrentDomain.BaseDirectory;
                var dbPath = AppStoragePaths.ResolveDatabasePath(appFolder);
                var serverUrl = "https://informtyping.com/v2_test";
                var apiKey = "85d7bd6243258f6d4d057ffa3885263566f69422a457b2b11a04edd6fbeb456b";

                try
                {
                    var settingsPath = Path.Combine(appFolder, "appsettings.json");
                    if (File.Exists(settingsPath))
                    {
                        var json = JsonDocument.Parse(File.ReadAllText(settingsPath));
                        if (json.RootElement.TryGetProperty("Sync", out var syncSection))
                        {
                            serverUrl = syncSection.GetProperty("ServerUrl").GetString() ?? serverUrl;
                            apiKey = syncSection.GetProperty("ApiKey").GetString() ?? apiKey;
                        }
                    }
                }
                catch { }

                SharedSyncService = new SyncService(dbPath, serverUrl, apiKey);
            }
            catch { }
        }

        private void StartBackgroundSyncTimer()
        {
            _backgroundSyncTimer = new DispatcherTimer();
            _backgroundSyncTimer.Interval = TimeSpan.FromMinutes(5);
            _backgroundSyncTimer.Tick += async (s, e) =>
            {
                if (SharedSyncService == null || SharedSyncService.IsSyncing) return;
                try
                {
                    await SharedSyncService.FullSyncAsync();
                }
                catch { }
            };
            _backgroundSyncTimer.Start();
        }
    }
}
