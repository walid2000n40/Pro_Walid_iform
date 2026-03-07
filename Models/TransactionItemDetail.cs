using CommunityToolkit.Mvvm.ComponentModel;

namespace ProWalid.Models
{
    public partial class TransactionItemDetail : ObservableObject
    {
        [ObservableProperty]
        private string serviceName = string.Empty;

        [ObservableProperty]
        private double quantity;

        [ObservableProperty]
        private double unitPrice;

        [ObservableProperty]
        private double profit;

        [ObservableProperty]
        private double discount;

        [ObservableProperty]
        private string attachmentPath = string.Empty;

        public double Total => Quantity * UnitPrice;

        partial void OnQuantityChanged(double value)
        {
            OnPropertyChanged(nameof(Total));
        }

        partial void OnUnitPriceChanged(double value)
        {
            OnPropertyChanged(nameof(Total));
        }
    }
}
