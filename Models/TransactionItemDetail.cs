using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ProWalid.Models
{
    public partial class TransactionItemDetail : ObservableObject
    {
        [ObservableProperty]
        private long id;

        [ObservableProperty]
        private string serviceName = string.Empty;

        [ObservableProperty]
        private double quantity;

        [ObservableProperty]
        private double unitPrice;

        [ObservableProperty]
        private double profit;

        [ObservableProperty]
        private string govFees = string.Empty;

        [ObservableProperty]
        private string attachmentPath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Attachment> attachments = new();

        public double Total => Quantity * UnitPrice;

        public int AttachmentsCount => Attachments.Count;

        public TransactionItemDetail()
        {
            Attachments.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(AttachmentsCount));
            };
        }

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
