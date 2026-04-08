namespace ProWalid.Models
{
    public class TransactionStatementPreviewRow
    {
        public int SerialNumber { get; init; }

        public string ServiceName { get; init; } = string.Empty;

        public string TransactionDateText { get; init; } = string.Empty;

        public double Amount { get; init; }
    }
}
