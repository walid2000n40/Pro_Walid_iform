using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ProWalid.Models;
using ProWalid.ViewModels;

namespace ProWalid.Views
{
    public sealed partial class InvoicesPage : Page
    {
        public InvoicesViewModel ViewModel { get; }

        public InvoicesPage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = new InvoicesViewModel();
            DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.SetFrame(Frame);
            await ViewModel.LoadAsync(e.Parameter as Customer);
        }
    }
}
