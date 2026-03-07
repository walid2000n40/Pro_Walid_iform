using Microsoft.UI.Xaml;
using System;

namespace ProWalid
{
    public partial class App : Application
    {
        private Window m_window;

        public static Window MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            MainWindow = m_window;
            m_window.Activate();
        }
    }
}
