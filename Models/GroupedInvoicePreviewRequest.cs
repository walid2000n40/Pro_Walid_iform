using System;
using System.Collections.Generic;

namespace ProWalid.Models
{
    public class GroupedInvoicePreviewRequest
    {
        public long CustomerId { get; init; }

        public string CustomerName { get; init; } = string.Empty;

        public string CompanyName { get; init; } = string.Empty;

        public string CustomerIdText { get; init; } = string.Empty;

        public string InvoiceNumber { get; init; } = string.Empty;

        public DateTimeOffset InvoiceDate { get; init; } = DateTimeOffset.Now;

        public string Notes { get; init; } = string.Empty;

        public IReadOnlyList<InvoicePreviewLineItem> Items { get; init; } = Array.Empty<InvoicePreviewLineItem>();

        public IReadOnlyList<string> SourceInvoiceNumbers { get; init; } = Array.Empty<string>();
    }
}
