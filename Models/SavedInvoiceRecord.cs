using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace ProWalid.Models
{
    public partial class SavedInvoiceRecord : ObservableObject
    {
        [ObservableProperty]
        private long id;

        [ObservableProperty]
        private int serialNumber;

        [ObservableProperty]
        private string savedInvoiceNumber = string.Empty;

        [ObservableProperty]
        private string rootInvoiceNumber = string.Empty;

        [ObservableProperty]
        private string sourceInvoiceNumber = string.Empty;

        [ObservableProperty]
        private int groupedSequenceNumber;

        [ObservableProperty]
        private string savedKind = "single";

        [ObservableProperty]
        private string templateKey = string.Empty;

        [ObservableProperty]
        private long customerId;

        [ObservableProperty]
        private string customerName = string.Empty;

        [ObservableProperty]
        private string companyName = string.Empty;

        [ObservableProperty]
        private string invoiceDateText = string.Empty;

        [ObservableProperty]
        private double totalAmount;

        [ObservableProperty]
        private string notes = string.Empty;

        [ObservableProperty]
        private string printHtml = string.Empty;

        [ObservableProperty]
        private string payloadJson = string.Empty;

        [ObservableProperty]
        private DateTimeOffset savedAt = DateTimeOffset.Now;

        [ObservableProperty]
        private int groupedInvoicesCount = 1;

        [ObservableProperty]
        private string groupedInvoiceNumbersSummary = string.Empty;

        public bool IsGrouped => string.Equals(SavedKind, "grouped", StringComparison.OrdinalIgnoreCase);

        public string SavedKindDisplay => IsGrouped ? "فاتورة مجمعة" : "فاتورة مفردة";

        public string DisplayInvoiceNumber => IsGrouped && !string.IsNullOrWhiteSpace(RootInvoiceNumber)
            ? RootInvoiceNumber
            : SavedInvoiceNumber;

        public string GroupedInvoicesCountText => IsGrouped
            ? $"{GroupedInvoicesCount} فواتير أصلية"
            : "فاتورة واحدة";

        public bool CanShowGroupedInvoiceNumbers => IsGrouped && !string.IsNullOrWhiteSpace(GroupedInvoiceNumbersSummary);

        public string TotalAmountText => $"{TotalAmount:N2} درهم";

        public string SavedAtText => SavedAt.ToString("yyyy/MM/dd HH:mm");
    }
}
