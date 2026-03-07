using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ProWalid.ViewModels;

namespace ProWalid.Views
{
    public sealed partial class TransactionPage : Page
    {
        public TransactionViewModel ViewModel { get; }

        public TransactionPage()
        {
            this.InitializeComponent();
            ViewModel = new TransactionViewModel();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.SetFrame(this.Frame);
        }
    }
}
