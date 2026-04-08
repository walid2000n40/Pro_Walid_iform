using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using ProWalid.Views;

namespace ProWalid
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Pro Walid - نظام إدارة المعاملات";

            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
            }
            
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1920, 1080));
            
            RootFrame.Navigate(typeof(LoginPage));
        }
    }
}
