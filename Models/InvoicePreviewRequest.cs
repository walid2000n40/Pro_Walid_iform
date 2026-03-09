namespace ProWalid.Models
{
    public class InvoicePreviewRequest
    {
        public InvoiceSummaryRow Row { get; init; } = new();

        public string TemplateKeyOverride { get; init; } = string.Empty;
    }
}
