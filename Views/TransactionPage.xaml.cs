using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using ProWalid.Data;
using ProWalid.Models;
using ProWalid.Services;
using ProWalid.ViewModels;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProWalid.Views
{
    public sealed partial class TransactionPage : Page
    {
        public TransactionViewModel ViewModel { get; }

        public TransactionPage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = TransactionViewModel.Instance ?? new TransactionViewModel();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.SetFrame(this.Frame);
        }

        private async void PendingStatusButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplyStatusAsync("معلق");
        }

        private async void DeliveredStatusButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplyStatusAsync("تم التسليم");
        }

        private async Task ApplyStatusAsync(string status)
        {
            if (ViewModel?.SetSelectedTransactionStatusCommand == null)
            {
                return;
            }

            await ViewModel.SetSelectedTransactionStatusCommand.ExecuteAsync(status);
        }

        private void CustomerListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Customer customer)
            {
                ViewModel.SelectedCustomer = customer;
            }
        }

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            SyncButton.IsEnabled = false;
            SyncButtonText.Text = "جاري المزامنة...";

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

                var syncService = new SyncService(dbPath, serverUrl, apiKey);
                var result = await syncService.FullSyncAsync();

                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = result.Success ? "تمت المزامنة" : "فشلت المزامنة",
                    Content = result.Message,
                    CloseButtonText = "حسناً"
                };
                await dialog.ShowAsync();

                if (result.Success)
                {
                    await ViewModel.LoadTransactionsAsync();
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "خطأ",
                    Content = $"فشلت المزامنة: {ex.Message}",
                    CloseButtonText = "حسناً"
                };
                await dialog.ShowAsync();
            }
            finally
            {
                SyncButton.IsEnabled = true;
                SyncButtonText.Text = "مزامنة";
            }
        }
    }
}
