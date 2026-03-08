using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProWalid.Models
{
    public partial class Transaction : ObservableObject
    {
        [ObservableProperty]
        private long customerId;

        [ObservableProperty]
        private string transactionStatus = "معلق";

        [ObservableProperty]
        private string invoiceNumber = string.Empty;

        [ObservableProperty]
        private string invoiceTemplateKey = string.Empty;

        [ObservableProperty]
        private string companyName = string.Empty;

        [ObservableProperty]
        private string employeeName = string.Empty;

        [ObservableProperty]
        private DateTimeOffset transactionDate = DateTimeOffset.Now;

        [ObservableProperty]
        private ObservableCollection<TransactionItemDetail> items = new();

        public double GrandTotal => Items.Sum(item => item.Total);

        public int ItemsCount => Items.Count;

        public Transaction()
        {
            Items.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(ItemsCount));
            };
        }
    }
}
