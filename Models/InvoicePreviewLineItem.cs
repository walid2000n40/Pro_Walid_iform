using CommunityToolkit.Mvvm.ComponentModel;

namespace ProWalid.Models
{
    public partial class InvoicePreviewLineItem : ObservableObject
    {
        [ObservableProperty]
        private int lineNumber;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private double quantity;

        [ObservableProperty]
        private double unitPrice;

        [ObservableProperty]
        private string govFees = string.Empty;

        public double Total => Quantity * UnitPrice;

        public string GovFeesDisplay => string.IsNullOrWhiteSpace(GovFees) ? "-" : GovFees;

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
