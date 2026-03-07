using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ProWalid.Models;
using ProWalid.ViewModels;

namespace ProWalid.Views
{
    public sealed partial class AddTransactionPage : Page
    {
        public AddTransactionPageViewModel ViewModel { get; }

        public AddTransactionPage()
        {
            this.InitializeComponent();
            ViewModel = new AddTransactionPageViewModel();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.SetFrame(this.Frame);
            
            if (e.Parameter is Transaction transaction)
            {
                ViewModel.LoadTransaction(transaction);
            }
            else if (e.Parameter is Customer customer)
            {
                ViewModel.LoadCustomer(customer);
            }
        }
    }
}
