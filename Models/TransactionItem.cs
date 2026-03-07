using CommunityToolkit.Mvvm.ComponentModel;

namespace ProWalid.Views
{
    public partial class TransactionItem : ObservableObject
    {
        [ObservableProperty]
        private string itemName = string.Empty;

        [ObservableProperty]
        private string companyName = string.Empty;

        [ObservableProperty]
        private string employeeName = string.Empty;

        [ObservableProperty]
        private double quantity;

        [ObservableProperty]
        private double price;

        public double Total => Quantity * Price;

        partial void OnQuantityChanged(double value)
        {
            OnPropertyChanged(nameof(Total));
        }

        partial void OnPriceChanged(double value)
        {
            OnPropertyChanged(nameof(Total));
        }
    }
}
