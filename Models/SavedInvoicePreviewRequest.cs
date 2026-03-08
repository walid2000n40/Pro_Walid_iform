namespace ProWalid.Models
{
    public class SavedInvoicePreviewRequest
    {
        public SavedInvoiceRecord Record { get; init; } = new();

        public bool AutoPrint { get; init; }
    }
}
