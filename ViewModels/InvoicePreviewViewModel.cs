using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Data;
using ProWalid.Models;
using System;
using System.Collections.ObjectModel;
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
        private string companyName = "مؤسسة برو وليد للخدمات الرقمية";

        [ObservableProperty]
        private string companySubtitle = "حلول أعمال - معاملات - خدمات تقنية";

        [ObservableProperty]
        private string companyPhone = "+971 50 123 4567";

        [ObservableProperty]
        private string companyEmail = "info@prowalid.com";

        [ObservableProperty]
        private string companyAddress = "دبي - الإمارات العربية المتحدة";

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

            CustomerName = !string.IsNullOrWhiteSpace(row.CustomerName)
                ? row.CustomerName
                : customer?.Name ?? "غير محدد";

            CustomerIdText = customer == null
                ? $"ID {row.Transaction.CustomerId}"
                : $"ID {(customer.CustomerNumber > 0 ? customer.CustomerNumber : customer.Id)}";

            CompanyName = string.IsNullOrWhiteSpace(row.CompanyName)
                ? "مؤسسة برو وليد للخدمات الرقمية"
                : row.CompanyName;

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
            CompanyName = "مؤسسة برو وليد للخدمات الرقمية";
            CompanySubtitle = "حلول أعمال - معاملات - خدمات تقنية";
            CompanyPhone = "+971 50 123 4567";
            CompanyEmail = "info@prowalid.com";
            CompanyAddress = "دبي - الإمارات العربية المتحدة";
            TaxNumber = "TRN 100245889900003";
            CustomerName = "شركة الأفق للتجارة العامة";
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

            var vatLabel = IsHazemInvoice ? "الضريبة" : "ضريبة القيمة المضافة 5%";
            var hazardNotice = IsHazemInvoice
                ? "<div class='note-badge'>GOV-FEES قيمة معلوماتية فقط ولا تدخل في الإجمالي</div>"
                : string.Empty;

            return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='utf-8' />
    <title>{Escape(InvoiceNumber)}</title>
    <style>
        @page {{ size: A4; margin: 12mm; }}
        * {{ box-sizing: border-box; }}
        body {{ margin: 0; background: #eaf1f8; font-family: 'Cairo', 'Segoe UI', sans-serif; color: #17324d; }}
        .sheet {{ width: 210mm; min-height: 297mm; margin: 0 auto; background: #ffffff; padding: 14mm; }}
        .header {{ display: flex; justify-content: space-between; align-items: flex-start; gap: 10mm; margin-bottom: 8mm; }}
        .title {{ font-size: 28px; font-weight: 700; color: #123b66; margin: 0 0 3mm 0; }}
        .subtitle {{ color: #5a7490; font-size: 12px; margin: 0; }}
        .meta-grid {{ display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 4mm; width: 72mm; }}
        .meta-card, .info-card, .summary-card, .notes-card {{ border: 1px solid #d7e5f3; border-radius: 14px; background: #f8fbff; padding: 4mm; }}
        .meta-label, .section-label {{ font-size: 11px; color: #6b8198; margin-bottom: 1.5mm; }}
        .meta-value {{ font-size: 16px; font-weight: 700; color: #153e75; }}
        .info-grid {{ display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 4mm; margin-bottom: 6mm; }}
        .info-title {{ font-size: 13px; font-weight: 700; color: #153e75; margin-bottom: 2mm; }}
        .info-value {{ font-size: 17px; font-weight: 600; color: #15304d; }}
        .muted {{ color: #60758c; font-size: 12px; }}
        .note-badge {{ display: inline-block; margin-top: 2mm; padding: 2mm 3mm; border-radius: 999px; background: #fff7d6; color: #7a5a00; font-size: 11px; font-weight: 700; }}
        table {{ width: 100%; border-collapse: collapse; table-layout: fixed; margin-top: 4mm; }}
        thead th {{ background: #eaf2fb; color: #163b63; font-size: 13px; font-weight: 700; padding: 3.5mm 2.5mm; border-bottom: 1px solid #d9e6f4; }}
        tbody td {{ padding: 3.5mm 2.5mm; border-bottom: 1px solid #edf3f9; font-size: 12px; vertical-align: top; }}
        .num {{ text-align: center; }}
        .service {{ text-align: right; }}
        .gov {{ color: #7a5a00; font-weight: 700; }}
        .total {{ color: #0f766e; font-weight: 700; }}
        .bottom-grid {{ display: grid; grid-template-columns: 1fr 78mm; gap: 5mm; margin-top: 6mm; align-items: start; }}
        .summary-card {{ background: #0f3f71; color: #ffffff; border-color: #0f3f71; }}
        .summary-row {{ display: flex; justify-content: space-between; gap: 3mm; margin-top: 3mm; font-size: 13px; }}
        .summary-row strong {{ color: #fde68a; font-size: 18px; }}
        .notes-card {{ min-height: 34mm; }}
        .notes-text {{ color: #4e6580; line-height: 1.9; font-size: 12px; white-space: pre-wrap; }}
    </style>
</head>
<body>
    <div class='sheet'>
        <div class='header'>
            <div>
                <h1 class='title'>{Escape(IsHazemInvoice ? "فاتورة حازم - A4" : "فاتورة A4 جاهزة للطباعة")}</h1>
                <p class='subtitle'>{Escape(CompanyName)}</p>
                <p class='subtitle'>{Escape(CompanySubtitle)}</p>
                <p class='subtitle'>{Escape(CompanyAddress)}</p>
                {hazardNotice}
            </div>
            <div class='meta-grid'>
                <div class='meta-card'>
                    <div class='meta-label'>رقم الفاتورة</div>
                    <div class='meta-value'>{Escape(InvoiceNumber)}</div>
                </div>
                <div class='meta-card'>
                    <div class='meta-label'>الحالة</div>
                    <div class='meta-value'>{Escape(InvoiceStatus)}</div>
                </div>
                <div class='meta-card'>
                    <div class='meta-label'>التاريخ</div>
                    <div class='meta-value'>{Escape(InvoiceDate)}</div>
                </div>
                <div class='meta-card'>
                    <div class='meta-label'>الموظف</div>
                    <div class='meta-value'>{Escape(EmployeeName)}</div>
                </div>
            </div>
        </div>

        <div class='info-grid'>
            <div class='info-card'>
                <div class='info-title'>بيانات العميل</div>
                <div class='info-value'>{Escape(CustomerName)}</div>
                <div class='muted'>{Escape(CustomerIdText)}</div>
            </div>
            <div class='info-card'>
                <div class='info-title'>الاستحقاق</div>
                <div class='info-value'>{Escape(DueDate)}</div>
            </div>
            <div class='info-card'>
                <div class='info-title'>نوع الفاتورة</div>
                <div class='info-value'>{Escape(IsHazemInvoice ? "مخصصة لحازم" : "قياسية")}</div>
            </div>
        </div>

        <table>
            <thead>
                <tr>
                    <th style='width:10%;'>#</th>
                    <th style='width:38%;'>الخدمة</th>
                    <th style='width:12%;'>الكمية</th>
                    <th style='width:14%;'>سعر الوحدة</th>
                    <th style='width:12%;'>GOV-FEES</th>
                    <th style='width:14%;'>الإجمالي</th>
                </tr>
            </thead>
            <tbody>
                {rows}
            </tbody>
        </table>

        <div class='bottom-grid'>
            <div class='notes-card'>
                <div class='section-label'>ملاحظات</div>
                <div class='notes-text'>{Escape(Notes)}</div>
            </div>
            <div class='summary-card'>
                <div class='section-label' style='color:#dcebff;'>الملخص المالي</div>
                <div class='summary-row'><span>الإجمالي الفرعي</span><span>{Escape(SubtotalText)}</span></div>
                <div class='summary-row'><span>{Escape(vatLabel)}</span><span>{Escape(VatText)}</span></div>
                <div class='summary-row'><span>الإجمالي النهائي</span><strong>{Escape(GrandTotalText)}</strong></div>
            </div>
        </div>
    </div>
</body>
</html>";
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
