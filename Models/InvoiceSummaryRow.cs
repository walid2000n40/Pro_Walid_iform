using ProWalid.Models;

namespace ProWalid.Models
{
    public class InvoiceSummaryRow
    {
        public int SerialNumber { get; init; }

        public string InvoiceNumber { get; init; } = string.Empty;

        public string TransactionDateText { get; init; } = string.Empty;

        public string CustomerName { get; init; } = string.Empty;

        public string CompanyName { get; init; } = string.Empty;

        public string EmployeeName { get; init; } = string.Empty;

        public string Status { get; init; } = "محفوظة";

        public double TotalAmount { get; init; }

        public int ItemsCount { get; init; }

        public Transaction Transaction { get; init; } = new();
    }
}
