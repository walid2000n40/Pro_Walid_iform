using CommunityToolkit.Mvvm.ComponentModel;

namespace ProWalid.Models
{
    public partial class TransactionItemWithDetails : ObservableObject
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
        private string companyName = string.Empty;

        [ObservableProperty]
        private string employeeName = string.Empty;

        [ObservableProperty]
        private string attachmentPath = string.Empty;

        public double Total => Quantity * UnitPrice;
    }
}
