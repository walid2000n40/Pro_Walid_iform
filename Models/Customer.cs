using CommunityToolkit.Mvvm.ComponentModel;

namespace ProWalid.Models
{
    public partial class Customer : ObservableObject
    {
        [ObservableProperty]
        private long id;

        [ObservableProperty]
        private long customerNumber;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string phone = string.Empty;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string address = string.Empty;

        [ObservableProperty]
        private string notes = string.Empty;

        [ObservableProperty]
        private bool isSelected;
    }
}
