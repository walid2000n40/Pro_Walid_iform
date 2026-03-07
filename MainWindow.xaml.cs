using Microsoft.UI.Xaml;
using ProWalid.Views;

namespace ProWalid
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Pro Walid - نظام إدارة المعاملات";
            
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1080));
            
            RootFrame.Navigate(typeof(LoginPage));
        }
    }
}
