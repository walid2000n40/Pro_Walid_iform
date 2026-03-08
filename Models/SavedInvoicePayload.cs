using System.Collections.Generic;

namespace ProWalid.Models
{
    public class SavedInvoicePayload
    {
        public string CompanyName { get; init; } = string.Empty;

        public string CompanySubtitle { get; init; } = string.Empty;

        public string CompanyPhone { get; init; } = string.Empty;

        public string CompanyEmail { get; init; } = string.Empty;

        public string CompanyAddress { get; init; } = string.Empty;

        public string TaxNumber { get; init; } = string.Empty;

        public string CustomerName { get; init; } = string.Empty;

        public string CustomerIdText { get; init; } = string.Empty;

        public string InvoiceNumber { get; init; } = string.Empty;

        public string InvoiceDate { get; init; } = string.Empty;

        public string DueDate { get; init; } = string.Empty;

        public string InvoiceStatus { get; init; } = string.Empty;

        public string Notes { get; init; } = string.Empty;

        public string EmployeeName { get; init; } = string.Empty;

        public bool IsHazemInvoice { get; init; }

        public string SelectedPreviewTemplateKey { get; init; } = string.Empty;

        public List<SavedInvoiceLineItemPayload> Items { get; init; } = new();
    }

    public class SavedInvoiceLineItemPayload
    {
        public int LineNumber { get; init; }

        public string Description { get; init; } = string.Empty;

        public double Quantity { get; init; }

        public double UnitPrice { get; init; }

        public string GovFees { get; init; } = string.Empty;

        public string EmployeeNames { get; init; } = string.Empty;
    }
}
