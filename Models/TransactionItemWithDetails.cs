using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ProWalid.Models
{
    public partial class TransactionItemWithDetails : ObservableObject
    {
        [ObservableProperty]
        private long itemId;

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

        [ObservableProperty]
        private ObservableCollection<Attachment> attachments = new();

        public double Total => Quantity * UnitPrice;
    }
}
