using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;

namespace ProWalid.Models
{
    public partial class InvoiceSummaryRow : ObservableObject
    {
        [ObservableProperty]
        private int serialNumber;

        [ObservableProperty]
        private string invoiceNumber = string.Empty;

        [ObservableProperty]
        private string transactionDateText = string.Empty;

        [ObservableProperty]
        private string customerName = string.Empty;

        [ObservableProperty]
        private string companyName = string.Empty;

        [ObservableProperty]
        private string employeeName = string.Empty;

        [ObservableProperty]
        private string status = "محفوظة";

        [ObservableProperty]
        private double totalAmount;

        [ObservableProperty]
        private int itemsCount;

        [ObservableProperty]
        private Transaction transaction = new();

        [ObservableProperty]
        private bool isSelected;

        public double PrintingFeesAmount => Transaction?.Items?.Sum(item => item.Profit) ?? 0;

        public string PrintingFeesAmountText => $"{PrintingFeesAmount:N2} درهم";
    }
}
