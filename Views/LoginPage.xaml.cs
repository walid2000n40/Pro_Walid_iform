using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProWalid.ViewModels;

namespace ProWalid.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginPage()
        {
            this.InitializeComponent();
            var viewModel = new LoginViewModel();
            this.DataContext = viewModel;
            viewModel.LoginSuccessful += OnLoginSuccessful;
        }

        private void OnLoginSuccessful(object? sender, System.EventArgs e)
        {
            Frame.Navigate(typeof(TransactionPage));
        }
    }
}
