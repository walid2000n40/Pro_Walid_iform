using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;

namespace ProWalid.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string username = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private bool rememberMe;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private Visibility hasError = Visibility.Collapsed;

        public event EventHandler? LoginSuccessful;

        [RelayCommand]
        private void Login()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "الرجاء إدخال اسم المستخدم";
                HasError = Visibility.Visible;
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "الرجاء إدخال كلمة المرور";
                HasError = Visibility.Visible;
                return;
            }

            if (Username == "admin" && Password == "admin")
            {
                HasError = Visibility.Collapsed;
                ErrorMessage = string.Empty;
                LoginSuccessful?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = "اسم المستخدم أو كلمة المرور غير صحيحة";
                HasError = Visibility.Visible;
            }
        }
    }
}
