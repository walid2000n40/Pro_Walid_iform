using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Data;
using ProWalid.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ProWalid.ViewModels
{
    public partial class InvoicePreviewViewModel : ObservableObject
    {
        private Frame? _frame;
        private readonly DatabaseHelper _databaseHelper = new();

        [ObservableProperty]
        private string companyName = "انفورم للطباعة والتصوير";

        [ObservableProperty]
        private string companySubtitle = "Inform Typing Photo Copy";

        [ObservableProperty]
        private string companyPhone = "Mob: 971528047909 / 528047909";

        [ObservableProperty]
        private string companyEmail = "Email: alzaeemtyping@hotmail.com";

        [ObservableProperty]
        private string companyAddress = "العنوان / Address: أبوظبي مصفح م7";

        [ObservableProperty]
        private string taxNumber = "TRN 100245889900003";

        [ObservableProperty]
        private string customerName = "شركة الأفق للتجارة العامة";

        [ObservableProperty]
        private string customerIdText = "ID 1048";

        [ObservableProperty]
        private string invoiceNumber = "INV-2026-084";

        [ObservableProperty]
        private string invoiceDate = "2026/03/08";

        [ObservableProperty]
        private string dueDate = "2026/03/15";

        [ObservableProperty]
        private string invoiceStatus = "معلق";

        [ObservableProperty]
        private string notes = "هذه مجرد معاينة بصرية أولية لاختيار اتجاه التصميم قبل اعتماد النموذج النهائي وحقن بياناتك الحقيقية.";

        [ObservableProperty]
        private string employeeName = string.Empty;

        [ObservableProperty]
        private bool isHazemInvoice;

        [ObservableProperty]
        private string printHtml = string.Empty;

        public ObservableCollection<InvoicePreviewLineItem> Items { get; } = new();

        public double Subtotal => Items.Sum(item => item.Total);

        public double Vat => IsHazemInvoice ? 0 : Subtotal * 0.05;

        public double GrandTotal => Subtotal + Vat;

        public string SubtotalText => $"{Subtotal:N2} درهم";

        public string VatText => $"{Vat:N2} درهم";

        public string GrandTotalText => $"{GrandTotal:N2} درهم";

        public InvoicePreviewViewModel()
        {
            Items.CollectionChanged += (_, _) => RefreshTotals();

            LoadSamplePreview();
        }

        public async Task LoadAsync(InvoiceSummaryRow? row)
        {
            if (row?.Transaction == null)
            {
                LoadSamplePreview();
                return;
            }

            var customer = (await _databaseHelper.GetAllCustomersAsync())
                .FirstOrDefault(item => item.Id == row.Transaction.CustomerId);

            CustomerName = !string.IsNullOrWhiteSpace(row.Transaction.CompanyName)
                ? row.Transaction.CompanyName
                : !string.IsNullOrWhiteSpace(row.CompanyName)
                    ? row.CompanyName
                    : !string.IsNullOrWhiteSpace(row.CustomerName)
                        ? row.CustomerName
                        : customer?.Name ?? "غير محدد";

            CustomerIdText = customer == null
                ? $"ID {row.Transaction.CustomerId}"
                : $"ID {(customer.CustomerNumber > 0 ? customer.CustomerNumber : customer.Id)}";

            InvoiceNumber = row.InvoiceNumber;
            InvoiceDate = row.Transaction.TransactionDate.ToString("yyyy/MM/dd");
            DueDate = row.Transaction.TransactionDate.ToString("yyyy/MM/dd");
            InvoiceStatus = string.IsNullOrWhiteSpace(row.Transaction.TransactionStatus) ? "معلق" : row.Transaction.TransactionStatus;
            EmployeeName = string.IsNullOrWhiteSpace(row.EmployeeName) ? "غير محدد" : row.EmployeeName;
            IsHazemInvoice = IsHazemCustomer(CustomerName);
            Notes = IsHazemInvoice
                ? "نموذج حازم: كل فاتورة مرتبطة بمعاملة واحدة فقط. قيمة GOV-FEES المعروضة لكل بند هي قيمة معلوماتية فقط ولا تدخل ضمن الإجمالي أو أي منطق محاسبي."
                : "هذه معاينة حقيقية مبنية على بيانات الفاتورة المحفوظة."
                ;

            Items.Clear();

            var lineNumber = 1;
            foreach (var item in row.Transaction.Items)
            {
                Items.Add(new InvoicePreviewLineItem
                {
                    LineNumber = lineNumber++,
                    Description = item.ServiceName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    GovFees = item.GovFees
                });
            }

            RefreshTotals();
        }

        private void LoadSamplePreview()
        {
            CompanyName = "انفورم للطباعة والتصوير";
            CompanySubtitle = "Inform Typing Photo Copy";
            CompanyPhone = "Mob: 971528047909 / 528047909";
            CompanyEmail = "Email: alzaeemtyping@hotmail.com";
            CompanyAddress = "العنوان / Address: أبوظبي مصفح م7";
            TaxNumber = "TRN 100245889900003";
            CustomerName = "اسم الشركة من المعاملة";
            CustomerIdText = "ID 1048";
            InvoiceNumber = "INV-2026-084";
            InvoiceDate = DateTime.Now.ToString("yyyy/MM/dd");
            DueDate = DateTime.Now.AddDays(7).ToString("yyyy/MM/dd");
            InvoiceStatus = "معلق";
            EmployeeName = "غير محدد";
            IsHazemInvoice = false;
            Notes = "هذه مجرد معاينة بصرية أولية لاختيار اتجاه التصميم قبل اعتماد النموذج النهائي وحقن بياناتك الحقيقية.";

            Items.Clear();
            Items.Add(new InvoicePreviewLineItem { LineNumber = 1, Description = "إدارة معاملة متكاملة", Quantity = 1, UnitPrice = 1250, GovFees = "125" });
            Items.Add(new InvoicePreviewLineItem { LineNumber = 2, Description = "إرفاق ومراجعة مستندات", Quantity = 3, UnitPrice = 240, GovFees = "80" });
            Items.Add(new InvoicePreviewLineItem { LineNumber = 3, Description = "تنسيق كشف وتسليم نهائي", Quantity = 1, UnitPrice = 680, GovFees = "50" });

            RefreshTotals();
        }

        public void SetFrame(Frame frame)
        {
            _frame = frame;
        }

        private static bool IsHazemCustomer(string customerName)
        {
            return !string.IsNullOrWhiteSpace(customerName)
                && (customerName.Contains("حازم", StringComparison.OrdinalIgnoreCase)
                    || customerName.Contains("hazem", StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshTotals()
        {
            OnPropertyChanged(nameof(IsHazemInvoice));
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Vat));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(SubtotalText));
            OnPropertyChanged(nameof(VatText));
            OnPropertyChanged(nameof(GrandTotalText));
            PrintHtml = BuildPrintHtml();
        }

        private string BuildPrintHtml()
        {
            var logoDataUri = GetImageDataUri("Assets", "invoice", "LOGO1.png");
            var stampDataUri = GetImageDataUri("Assets", "invoice", "STAMP (1).png");

            var rows = new StringBuilder();
            foreach (var item in Items)
            {
                rows.Append($@"
                    <tr>
                        <td class='num'>{item.LineNumber}</td>
                        <td class='service'>{Escape(item.Description)}</td>
                        <td class='num'>{item.Quantity:0.##}</td>
                        <td class='num'>{item.UnitPrice:N2}</td>
                        <td class='num gov'>{Escape(item.GovFeesDisplay)}</td>
                        <td class='num total'>{item.Total:N2}</td>
                    </tr>");
            }
            return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='utf-8' />
    <title>{Escape(InvoiceNumber)}</title>
    <style>
        @page {{ size: A4; margin: 9mm 10mm 10mm 10mm; }}
        * {{ box-sizing: border-box; }}
        html, body {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; forced-color-adjust: none; }}
        body {{ margin: 0; background: #eff7fc; font-family: 'Cairo', 'Segoe UI', sans-serif; color: #17324d; }}
        .sheet {{ width: 210mm; min-height: 297mm; margin: 0 auto; background: #ffffff; padding: 7mm 10mm 10mm 10mm; display: flex; flex-direction: column; }}
        .content-section {{ display: block; }}
        .hero {{ position: relative; background: linear-gradient(135deg, #e9f8ff 0%, #d7f0ff 100%); border: 1px solid #b9e4fb; border-radius: 22px; padding: 4.1mm 5.2mm; margin-bottom: 3.4mm; }}
        .hero-grid {{ display: grid; grid-template-columns: 32mm 1fr; gap: 4mm; align-items: center; direction: ltr; }}
        .hero-logo-wrap {{ display: flex; align-items: center; justify-content: flex-start; min-height: 100%; }}
        .hero-logo {{ max-width: 28mm; max-height: 21mm; width: auto; height: auto; object-fit: contain; }}
        .hero-invoice-title {{ position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; direction: ltr; width: 42mm; pointer-events: none; }}
        .invoice-main-title {{ font-size: 21px; font-weight: 800; letter-spacing: 0.8px; color: #0d4f7a; line-height: 1; margin-bottom: 0.8mm; }}
        .invoice-sub-title {{ font-size: 14px; font-weight: 700; color: #1f5e84; line-height: 1; margin-bottom: 1mm; direction: rtl; text-align: center; width: 100%; }}
        .invoice-title-line {{ width: 26mm; border-top: 1px solid #78aecd; }}
        .hero-brand {{ direction: rtl; text-align: right; padding-left: 44mm; }}
        .brand-name {{ font-size: 22px; font-weight: 800; color: #0d4f7a; margin: 0 0 0.8mm 0; }}
        .brand-subtitle {{ font-size: 13px; font-weight: 700; color: #286489; margin: 0 0 1.4mm 0; }}
        .brand-line {{ font-size: 11px; color: #3b6787; margin: 0.6mm 0; }}
        .meta-grid {{ display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 4mm; }}
        .meta-card, .client-card, .total-card {{ border: 1px solid #cfeaf8; border-radius: 18px; background: #ffffff; padding: 3.2mm 3.8mm; }}
        .meta-label, .section-label {{ font-size: 10.5px; color: #5d7b95; margin-bottom: 1mm; }}
        .meta-value {{ font-size: 15px; font-weight: 800; color: #144b71; }}
        .client-row {{ display: grid; grid-template-columns: 0.78fr 1.22fr; gap: 4mm; align-items: stretch; margin-bottom: 3.6mm; direction: ltr; }}
        .client-card {{ background: #f7fcff; }}
        .client-side-card {{ background: #f7fcff; direction: rtl; text-align: right; }}
        .client-title {{ font-size: 12.5px; font-weight: 700; color: #15507c; margin-bottom: 1.2mm; text-align: center; }}
        .client-name-wrap {{ background: rgba(255, 255, 255, 0.72); border-radius: 12px; padding: 2.2mm 3mm; margin: 1.2mm 0 1.6mm 0; text-align: center; }}
        .client-name {{ font-size: 17.5px; font-weight: 800; color: #0f3556; margin: 0; line-height: 1.4; }}
        .muted {{ color: #648099; font-size: 12px; }}
        .id-text {{ color: #0e5b86; font-size: 11.5px; font-weight: 700; margin-top: 1mm; text-align: center; }}
        .side-meta-item + .side-meta-item {{ margin-top: 2.2mm; }}
        .side-meta-item {{ display: flex; align-items: center; justify-content: space-between; gap: 2.5mm; direction: ltr; background: rgba(255, 255, 255, 0.72); border-radius: 12px; padding: 2.1mm 2.7mm; }}
        .side-meta-item .meta-label {{ margin: 0; text-align: right; direction: rtl; flex: 1; }}
        .side-meta-item .meta-value {{ min-width: 25mm; text-align: left; direction: ltr; font-size: 16px; }}
        .employee-row {{ display: flex; justify-content: flex-start; direction: ltr; margin: 0 0 2.2mm 0; }}
        .employee-line {{ color: #8b1e1e; font-size: 13.5px; font-weight: 800; direction: rtl; text-align: right; padding-left: 6mm; }}
        table {{ width: 100%; border-collapse: separate; border-spacing: 0; table-layout: fixed; margin-top: 2mm; border: 1px solid #cae8f9; border-radius: 18px; overflow: hidden; }}
        thead th {{ background: #dff3ff; color: #114566; font-size: 13px; font-weight: 800; padding: 3.6mm 2.5mm; border-bottom: 1px solid #c6e8fa; }}
        tbody td {{ padding: 3.6mm 2.5mm; border-bottom: 1px solid #e7f4fb; font-size: 12px; vertical-align: top; }}
        tbody tr:nth-child(odd) td {{ background: #fafdff; }}
        tbody tr:nth-child(even) td {{ background: #f1f9fe; }}
        tbody tr:last-child td {{ border-bottom: 0; }}
        .num {{ text-align: center; }}
        .service {{ text-align: right; }}
        .gov {{ color: #7a5a00; font-weight: 700; }}
        .total {{ color: #0b7a75; font-weight: 800; }}
        .stamp-wrap {{ display: flex; justify-content: center; margin-top: 3.4mm; }}
        .stamp-image {{ max-width: 34mm; max-height: 34mm; width: auto; height: auto; object-fit: contain; }}
        .footer-row {{ display: flex; justify-content: flex-end; align-items: center; gap: 4mm; margin-top: 3mm; }}
        .total-card {{ min-width: 54mm; border: 1px solid #b8ddf1; border-radius: 14px; background: #dff3ff; color: #111111; padding: 2.4mm 3mm; }}
        .total-label {{ font-size: 10.5px; color: #111111; margin-bottom: 0.8mm; font-weight: 700; }}
        .total-value {{ font-size: 17px; font-weight: 700; color: #111111; }}
        .bottom-section {{ margin-top: auto; }}
        .signature-row {{ display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18mm; margin-top: 12mm; align-items: end; direction: rtl; }}
        .signature-block {{ text-align: center; direction: rtl; }}
        .signature-line {{ width: 88%; margin: 0 auto; border-top: 2px dotted #78aecd; padding-top: 3.2mm; color: #144b71; direction: rtl; }}
        .signature-ar {{ font-size: 11px; font-weight: 700; margin-bottom: 1.2mm; }}
        .signature-en {{ font-size: 13px; font-weight: 800; }}
        .invoice-footer {{ margin-top: 6.5mm; background: linear-gradient(135deg, #e9f8ff 0%, #d7f0ff 100%); border: 1px solid #b9e4fb; border-radius: 18px; padding: 3.1mm 5mm; text-align: center; color: #15507c; font-size: 12px; font-weight: 700; direction: rtl; }}
        .invoice-footer-text {{ display: inline-flex; align-items: center; justify-content: center; gap: 3mm; flex-wrap: wrap; direction: rtl; }}
        @media print {{
            body {{ background: #ffffff !important; }}
            .sheet {{ width: auto; min-height: calc(297mm - 24mm); margin: 0; padding: 0; }}
            .hero, .meta-card, .client-card, .client-side-card, .total-card, .invoice-footer, thead th, tbody td {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; }}
        }}
    </style>
</head>
<body>
    <div class='sheet'>
        <div class='content-section'>
        <div class='hero'>
            <div class='hero-grid'>
                <div class='hero-logo-wrap'>
                    {(string.IsNullOrWhiteSpace(logoDataUri) ? string.Empty : $"<img class='hero-logo' src='{logoDataUri}' alt='Logo' />")}
                </div>
                <div class='hero-invoice-title'>
                    <div class='invoice-main-title'>INVOICE</div>
                    <div class='invoice-sub-title'>فاتورة</div>
                    <div class='invoice-title-line'></div>
                </div>
                <div class='hero-brand'>
                    <div class='brand-name'>{Escape(CompanyName)}</div>
                    <div class='brand-subtitle'>{Escape(CompanySubtitle)}</div>
                    <div class='brand-line'>{Escape(CompanyAddress)}</div>
                </div>
            </div>
        </div>

        <div class='client-row'>
            <div class='meta-card client-side-card'>
                <div class='side-meta-item'>
                    <div class='meta-value'>{Escape(InvoiceNumber)}</div>
                    <div class='meta-label'>رقم الفاتورة / Invoice No</div>
                </div>
                <div class='side-meta-item'>
                    <div class='meta-value'>{Escape(InvoiceDate)}</div>
                    <div class='meta-label'>التاريخ / Date</div>
                </div>
            </div>
            <div class='client-card'>
                <div class='client-title'>بيانات العميل / Client Details</div>
                <div class='client-name-wrap'>
                    <div class='client-name'>{Escape(CustomerName)}</div>
                </div>
                <div class='id-text'>رقم العميل / Customer ID: {Escape(CustomerIdText)}</div>
            </div>
        </div>

        <div class='employee-row'>
            <div class='employee-line'>اسم الموظف / Employee: {Escape(EmployeeName)}</div>
        </div>

        <table>
            <thead>
                <tr>
                    <th style='width:10%;'>#</th>
                    <th style='width:38%;'>الخدمة / Service</th>
                    <th style='width:12%;'>الكمية / Qty</th>
                    <th style='width:14%;'>سعر الوحدة / Unit Price</th>
                    <th style='width:12%;'>GOV-FEES</th>
                    <th style='width:14%;'>الإجمالي / Total</th>
                </tr>
            </thead>
            <tbody>
                {rows}
            </tbody>
        </table>

        <div class='footer-row'>
            <div class='total-card'>
                <div class='total-label'>الإجمالي النهائي / Grand Total</div>
                <div class='total-value'>{Escape(GrandTotalText)}</div>
            </div>
        </div>

        {(string.IsNullOrWhiteSpace(stampDataUri) ? string.Empty : $"<div class='stamp-wrap'><img class='stamp-image' src='{stampDataUri}' alt='Stamp' /></div>")}

        </div>

        <div class='bottom-section'>
        <div class='signature-row'>
            <div class='signature-block'>
                <div class='signature-line'>
                    <div class='signature-ar'>انفورم للطباعة والتصوير</div>
                    <div class='signature-en'>Inform Typing Photo Copy</div>
                </div>
            </div>
            <div class='signature-block'>
                <div class='signature-line'>
                    <div class='signature-ar'>توقيع المستلم</div>
                    <div class='signature-en'>Receiver Signature</div>
                </div>
            </div>
        </div>

        <div class='invoice-footer'>
            <div class='invoice-footer-text'>
                <span>{Escape(CompanyName)}</span>
                <span>-</span>
                <span>{Escape(CompanyEmail)}</span>
                <span>-</span>
                <span>{Escape(CompanyPhone)}</span>
            </div>
        </div>
        </div>
    </div>
</body>
</html>";
        }

        private static string GetImageDataUri(params string[] relativePathParts)
        {
            try
            {
                var fullPath = Path.Combine(AppContext.BaseDirectory, Path.Combine(relativePathParts));
                if (!File.Exists(fullPath))
                {
                    fullPath = Path.Combine(Environment.CurrentDirectory, Path.Combine(relativePathParts));
                }

                if (!File.Exists(fullPath))
                {
                    return string.Empty;
                }

                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                var mimeType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".svg" => "image/svg+xml",
                    _ => "application/octet-stream"
                };

                var bytes = File.ReadAllBytes(fullPath);
                return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Escape(string? value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        [RelayCommand]
        private void GoBack()
        {
            if (_frame != null && _frame.CanGoBack)
            {
                _frame.GoBack();
            }
        }
    }
}
