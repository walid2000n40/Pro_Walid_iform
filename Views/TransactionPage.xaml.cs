using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using ProWalid.Models;
using ProWalid.ViewModels;
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
    }
}
