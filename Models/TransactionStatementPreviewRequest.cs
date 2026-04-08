using System;
using System.Collections.Generic;

namespace ProWalid.Models
{
    public class TransactionStatementPreviewRequest
    {
        public long CustomerId { get; init; }

        public string CustomerName { get; init; } = string.Empty;

        public string CompanyName { get; init; } = string.Empty;

        public string CustomerIdText { get; init; } = string.Empty;

        public string StatementNumber { get; init; } = string.Empty;

        public DateTimeOffset StatementDate { get; init; } = DateTimeOffset.Now;

        public string Notes { get; init; } = string.Empty;

        public IReadOnlyList<TransactionStatementPreviewRow> Rows { get; init; } = Array.Empty<TransactionStatementPreviewRow>();
    }
}
