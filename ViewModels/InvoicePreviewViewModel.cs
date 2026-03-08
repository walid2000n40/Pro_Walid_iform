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
        public const string FluentTemplateKey = "fluent";
        public const string ErpTemplateKey = "erp";
        public const string PremiumTemplateKey = "premium";
        public const string HazemTemplateKey = "hazem";
        public const string FinalAggregationTemplateKey = "final-aggregation";

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

        [ObservableProperty]
        private string selectedPreviewTemplateKey = FluentTemplateKey;

        public ObservableCollection<InvoicePreviewLineItem> Items { get; } = new();

        public ObservableCollection<InvoiceSummaryRow> FinalAggregationRows { get; } = new();

        public bool IsFinalAggregationPreview => string.Equals(SelectedPreviewTemplateKey, FinalAggregationTemplateKey, StringComparison.OrdinalIgnoreCase);

        public double Subtotal => IsFinalAggregationPreview
            ? FinalAggregationRows.Sum(row => row.PrintingFeesAmount)
            : Items.Sum(item => item.Total);

        public double Vat => 0;

        public double GrandTotal => Subtotal;

        public string SubtotalText => $"{Subtotal:N2} درهم";

        public string VatText => $"{Vat:N2} درهم";

        public string GrandTotalText => $"{GrandTotal:N2} درهم";

        public InvoicePreviewViewModel()
        {
            Items.CollectionChanged += (_, _) => RefreshTotals();
            FinalAggregationRows.CollectionChanged += (_, _) => RefreshTotals();

            LoadSamplePreview();
        }

        public async Task LoadAsync(object? parameter)
        {
            if (parameter is System.Collections.Generic.IEnumerable<InvoiceSummaryRow> rows)
            {
                var selectedRows = rows.Where(row => row?.Transaction != null).ToList();
                if (selectedRows.Count > 0)
                {
                    await LoadFinalAggregationAsync(selectedRows);
                    return;
                }
            }

            if (parameter is not InvoiceSummaryRow row || row.Transaction == null)
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
            IsHazemInvoice = IsHazemTransaction(row.Transaction.InvoiceTemplateKey, CustomerName);
            SelectedPreviewTemplateKey = IsHazemInvoice ? HazemTemplateKey : FluentTemplateKey;
            Notes = IsHazemInvoice
                ? "نموذج حازم: كل فاتورة مرتبطة بمعاملة واحدة فقط. قيمة GOV-FEES المعروضة لكل بند هي قيمة معلوماتية فقط ولا تدخل ضمن الإجمالي أو أي منطق محاسبي."
                : "هذه معاينة حقيقية مبنية على بيانات الفاتورة المحفوظة."
                ;

            FinalAggregationRows.Clear();
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

        private async Task LoadFinalAggregationAsync(System.Collections.Generic.IReadOnlyList<InvoiceSummaryRow> rows)
        {
            var firstRow = rows[0];
            var customer = (await _databaseHelper.GetAllCustomersAsync())
                .FirstOrDefault(item => item.Id == firstRow.Transaction.CustomerId);

            CustomerName = !string.IsNullOrWhiteSpace(firstRow.CompanyName)
                ? firstRow.CompanyName
                : !string.IsNullOrWhiteSpace(firstRow.Transaction.CompanyName)
                    ? firstRow.Transaction.CompanyName
                    : firstRow.CustomerName;

            CustomerIdText = customer == null
                ? $"ID {firstRow.Transaction.CustomerId}"
                : $"ID {(customer.CustomerNumber > 0 ? customer.CustomerNumber : customer.Id)}";

            InvoiceNumber = $"كشف-{firstRow.Transaction.CustomerId}";
            InvoiceDate = DateTime.Now.ToString("yyyy/MM/dd");
            DueDate = InvoiceDate;
            InvoiceStatus = "كشف تجميع نهائي";
            EmployeeName = string.Empty;
            IsHazemInvoice = false;
            SelectedPreviewTemplateKey = FinalAggregationTemplateKey;
            Notes = "هذا الكشف يعرض رسوم الطباعة للفواتير المحددة فقط، ويتم احتساب رسوم كل فاتورة من إجمالي الفايدة المسجل داخل بنود المعاملة.";

            Items.Clear();
            FinalAggregationRows.Clear();

            var serial = 1;
            foreach (var row in rows.OrderBy(item => item.Transaction.TransactionDate).ThenBy(item => item.InvoiceNumber))
            {
                FinalAggregationRows.Add(new InvoiceSummaryRow
                {
                    SerialNumber = serial++,
                    InvoiceNumber = row.InvoiceNumber,
                    TransactionDateText = row.TransactionDateText,
                    CustomerName = row.CustomerName,
                    CompanyName = row.CompanyName,
                    EmployeeName = row.EmployeeName,
                    Status = row.Status,
                    TotalAmount = row.TotalAmount,
                    ItemsCount = row.ItemsCount,
                    Transaction = row.Transaction,
                    IsSelected = row.IsSelected
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
            SelectedPreviewTemplateKey = FluentTemplateKey;
            Notes = "هذه مجرد معاينة بصرية أولية لاختيار اتجاه التصميم قبل اعتماد النموذج النهائي وحقن بياناتك الحقيقية.";

            FinalAggregationRows.Clear();
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

        public void SelectPreviewTemplate(string templateKey)
        {
            var normalizedTemplateKey = NormalizeTemplateKey(templateKey);
            if (string.Equals(SelectedPreviewTemplateKey, normalizedTemplateKey, StringComparison.OrdinalIgnoreCase))
            {
                PrintHtml = BuildPrintHtml();
                return;
            }

            SelectedPreviewTemplateKey = normalizedTemplateKey;
        }

        partial void OnSelectedPreviewTemplateKeyChanged(string value)
        {
            PrintHtml = BuildPrintHtml();
        }

        private static bool IsHazemCustomer(string customerName)
        {
            return !string.IsNullOrWhiteSpace(customerName)
                && (customerName.Contains("حازم", StringComparison.OrdinalIgnoreCase)
                    || customerName.Contains("hazem", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsHazemTransaction(string invoiceTemplateKey, string customerName)
        {
            if (string.Equals(invoiceTemplateKey, HazemTemplateKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsHazemCustomer(customerName);
        }

        private static string NormalizeTemplateKey(string? templateKey)
        {
            if (string.Equals(templateKey, ErpTemplateKey, StringComparison.OrdinalIgnoreCase))
            {
                return ErpTemplateKey;
            }

            if (string.Equals(templateKey, PremiumTemplateKey, StringComparison.OrdinalIgnoreCase))
            {
                return PremiumTemplateKey;
            }

            if (string.Equals(templateKey, HazemTemplateKey, StringComparison.OrdinalIgnoreCase))
            {
                return HazemTemplateKey;
            }

            if (string.Equals(templateKey, FinalAggregationTemplateKey, StringComparison.OrdinalIgnoreCase))
            {
                return FinalAggregationTemplateKey;
            }

            return FluentTemplateKey;
        }

        private void RefreshTotals()
        {
            OnPropertyChanged(nameof(IsHazemInvoice));
            OnPropertyChanged(nameof(IsFinalAggregationPreview));
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
            return NormalizeTemplateKey(SelectedPreviewTemplateKey) switch
            {
                ErpTemplateKey => BuildErpPrintHtml(),
                PremiumTemplateKey => BuildPremiumPrintHtml(),
                HazemTemplateKey => BuildHazemPrintHtml(),
                FinalAggregationTemplateKey => BuildFinalAggregationPrintHtml(),
                _ => BuildFluentPrintHtml()
            };
        }

        private string BuildFinalAggregationPrintHtml()
        {
            var rows = new StringBuilder();
            foreach (var row in FinalAggregationRows)
            {
                rows.Append($@"
                    <tr>
                        <td class='num'>{row.SerialNumber}</td>
                        <td class='num'>{Escape(row.InvoiceNumber)}</td>
                        <td class='num total'>{row.PrintingFeesAmount:N2}</td>
                    </tr>");
            }

            return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='utf-8' />
    <title>{Escape(InvoiceNumber)}</title>
    <style>
        @page {{ size: A4; margin: 8mm; }}
        * {{ box-sizing: border-box; }}
        html, body {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; forced-color-adjust: none; }}
        body {{ margin: 0; background: #f7fbff; font-family: 'Cairo', 'Segoe UI', sans-serif; color: #1f2937; }}
        .sheet {{ width: 100%; min-height: calc(297mm - 16mm); background: #ffffff; border: 1px solid #dbe7f3; padding: 10mm 9mm; }}
        .office-name {{ text-align: center; font-size: 28px; font-weight: 800; color: #123b66; margin-bottom: 5mm; }}
        .company-name {{ text-align: center; font-size: 22px; font-weight: 700; color: #1e4f82; margin-bottom: 3mm; }}
        .statement-title {{ text-align: center; font-size: 20px; font-weight: 800; color: #166534; margin-bottom: 6mm; }}
        .meta {{ display: flex; justify-content: space-between; margin-bottom: 5mm; font-size: 14px; font-weight: 700; color: #475569; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 2mm; }}
        thead th {{ background: #e8f3ff; color: #123b66; border: 1px solid #c7dbef; padding: 12px 10px; font-size: 14px; }}
        tbody td {{ border: 1px solid #dbe7f3; padding: 12px 10px; font-size: 14px; }}
        tbody tr:nth-child(odd) td {{ background: #fbfdff; }}
        tbody tr:nth-child(even) td {{ background: #f3f8fd; }}
        .num {{ text-align: center; }}
        .total {{ font-weight: 800; color: #166534; }}
        .summary {{ margin-top: 6mm; display: flex; justify-content: flex-end; }}
        .summary-card {{ min-width: 70mm; border: 1px solid #b8ddf1; background: #dff3ff; border-radius: 14px; padding: 12px 14px; }}
        .summary-label {{ font-size: 14px; color: #123b66; font-weight: 700; margin-bottom: 6px; }}
        .summary-value {{ font-size: 24px; color: #0f3f71; font-weight: 800; }}
    </style>
</head>
<body>
    <div class='sheet'>
        <div class='office-name'>{Escape(CompanyName)}</div>
        <div class='company-name'>{Escape(CustomerName)}</div>
        <div class='statement-title'>رسوم الطباعة</div>
        <div class='meta'>
            <div>التاريخ: {Escape(InvoiceDate)}</div>
            <div>عدد الفواتير: {FinalAggregationRows.Count}</div>
        </div>
        <table>
            <thead>
                <tr>
                    <th style='width:18%;'>رقم مسلسل</th>
                    <th style='width:42%;'>رقم الفاتورة</th>
                    <th style='width:40%;'>رسوم الطباعة</th>
                </tr>
            </thead>
            <tbody>
                {rows}
            </tbody>
        </table>
        <div class='summary'>
            <div class='summary-card'>
                <div class='summary-label'>إجمالي الكشف</div>
                <div class='summary-value'>{Escape(GrandTotalText)}</div>
            </div>
        </div>
    </div>
</body>
</html>";
        }

        private string BuildFluentPrintHtml()
        {
            return BuildStyledPrintHtml(
                templateTitle: "INVOICE",
                templateSubtitle: "فاتورة",
                titleAccent: "#5a9fc5",
                bodyBackground: "#eff7fc",
                heroGradientStart: "#e9f8ff",
                heroGradientEnd: "#d7f0ff",
                heroBorder: "#b9e4fb",
                metaGradientStart: "#d7f0ff",
                metaGradientEnd: "#c7e9ff",
                metaBorder: "#abd9f4",
                tableHeaderStart: "#b8e3fb",
                tableHeaderEnd: "#9fd4f3",
                tableHeaderBorder: "#90c7e8",
                totalBackground: "#dff3ff",
                totalBorder: "#b8ddf1",
                footerGradientStart: "#e9f8ff",
                footerGradientEnd: "#d7f0ff",
                footerBorder: "#b9e4fb",
                signatureBorder: "#78aecd",
                useGovFeesColumn: false,
                employeeColor: "#7f1d1d");
        }

        private string BuildErpPrintHtml()
        {
            return BuildStyledPrintHtml(
                templateTitle: "ERP ACCOUNTING",
                templateSubtitle: "فاتورة ضريبية",
                titleAccent: "#64748b",
                bodyBackground: "#f5f7fb",
                heroGradientStart: "#f8fafc",
                heroGradientEnd: "#eef2f7",
                heroBorder: "#d8e0ea",
                metaGradientStart: "#f8fafc",
                metaGradientEnd: "#eef2f7",
                metaBorder: "#d7dee8",
                tableHeaderStart: "#eef2f7",
                tableHeaderEnd: "#e2e8f0",
                tableHeaderBorder: "#cbd5e1",
                totalBackground: "#f8fafc",
                totalBorder: "#d7dee8",
                footerGradientStart: "#f8fafc",
                footerGradientEnd: "#eef2f7",
                footerBorder: "#d8e0ea",
                signatureBorder: "#94a3b8",
                useGovFeesColumn: false,
                employeeColor: "#7f1d1d");
        }

        private string BuildPremiumPrintHtml()
        {
            return BuildStyledPrintHtml(
                templateTitle: "PREMIUM",
                templateSubtitle: "فاتورة احترافية",
                titleAccent: "#7c3aed",
                bodyBackground: "#f7f3ff",
                heroGradientStart: "#4c1d95",
                heroGradientEnd: "#7c3aed",
                heroBorder: "#c4b5fd",
                metaGradientStart: "#fcfaff",
                metaGradientEnd: "#f3e8ff",
                metaBorder: "#ddd6fe",
                tableHeaderStart: "#f5f3ff",
                tableHeaderEnd: "#ede9fe",
                tableHeaderBorder: "#c4b5fd",
                totalBackground: "#312e81",
                totalBorder: "#4338ca",
                footerGradientStart: "#4c1d95",
                footerGradientEnd: "#7c3aed",
                footerBorder: "#c4b5fd",
                signatureBorder: "#8b5cf6",
                useGovFeesColumn: false,
                employeeColor: "#fecaca",
                darkSurfaceText: "#ffffff",
                footerTextColor: "#f5f3ff");
        }

        private string BuildHazemPrintHtml()
        {
            return BuildStyledPrintHtml(
                templateTitle: "INVOICE",
                templateSubtitle: "فاتورة",
                titleAccent: "#5a9fc5",
                bodyBackground: "#eff7fc",
                heroGradientStart: "#e9f8ff",
                heroGradientEnd: "#d7f0ff",
                heroBorder: "#b9e4fb",
                metaGradientStart: "#d7f0ff",
                metaGradientEnd: "#c7e9ff",
                metaBorder: "#abd9f4",
                tableHeaderStart: "#b8e3fb",
                tableHeaderEnd: "#9fd4f3",
                tableHeaderBorder: "#90c7e8",
                totalBackground: "#dff3ff",
                totalBorder: "#b8ddf1",
                footerGradientStart: "#e9f8ff",
                footerGradientEnd: "#d7f0ff",
                footerBorder: "#b9e4fb",
                signatureBorder: "#78aecd",
                useGovFeesColumn: true,
                employeeColor: "#7f1d1d");
        }

        private string BuildStyledPrintHtml(
            string templateTitle,
            string templateSubtitle,
            string titleAccent,
            string bodyBackground,
            string heroGradientStart,
            string heroGradientEnd,
            string heroBorder,
            string metaGradientStart,
            string metaGradientEnd,
            string metaBorder,
            string tableHeaderStart,
            string tableHeaderEnd,
            string tableHeaderBorder,
            string totalBackground,
            string totalBorder,
            string footerGradientStart,
            string footerGradientEnd,
            string footerBorder,
            string signatureBorder,
            bool useGovFeesColumn,
            string employeeColor,
            string darkSurfaceText = "#3d434a",
            string footerTextColor = "#3d434a")
        {
            var logoDataUri = GetImageDataUri("Assets", "invoice", "LOGO1.png");
            var stampDataUri = GetImageDataUri("Assets", "invoice", "STAMP (1).png");

            var phoneLines = (CompanyPhone ?? string.Empty)
                .Split(new[] { '/', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Replace("Mob:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();

            var phoneLinesHtml = string.Join(string.Empty, phoneLines.Select(part => $"<div class='contact-line'>Mob: {Escape(part)}</div>"));

            var tableHeadColumns = useGovFeesColumn
                ? @"<th style='width:10%;'>#</th>
                    <th style='width:36%;'>الخدمة / Service</th>
                    <th style='width:12%;'>الكمية / Qty</th>
                    <th style='width:14%;'>سعر الوحدة / Unit Price</th>
                    <th style='width:12%;'>GOV-FEES</th>
                    <th style='width:16%;'>الإجمالي / Total</th>"
                : @"<th style='width:12%;'>#</th>
                    <th style='width:42%;'>الخدمة / Service</th>
                    <th style='width:14%;'>الكمية / Qty</th>
                    <th style='width:16%;'>سعر الوحدة / Unit Price</th>
                    <th style='width:16%;'>الإجمالي / Total</th>";

            var rows = new StringBuilder();
            foreach (var item in Items)
            {
                rows.Append(useGovFeesColumn
                    ? $@"
                    <tr>
                        <td class='num'>{item.LineNumber}</td>
                        <td class='service'>{Escape(item.Description)}</td>
                        <td class='num'>{item.Quantity:0.##}</td>
                        <td class='num'>{item.UnitPrice:N2}</td>
                        <td class='num gov'>{Escape(item.GovFeesDisplay)}</td>
                        <td class='num total'>{item.Total:N2}</td>
                    </tr>"
                    : $@"
                    <tr>
                        <td class='num'>{item.LineNumber}</td>
                        <td class='service'>{Escape(item.Description)}</td>
                        <td class='num'>{item.Quantity:0.##}</td>
                        <td class='num'>{item.UnitPrice:N2}</td>
                        <td class='num total'>{item.Total:N2}</td>
                    </tr>");
            }

            return $@"<!DOCTYPE html>
<html lang='ar' dir='rtl'>
<head>
    <meta charset='utf-8' />
    <title>{Escape(InvoiceNumber)}</title>
    <style>
        @page {{ size: A4; margin: 4mm; }}
        * {{ box-sizing: border-box; }}
        html, body {{ -webkit-print-color-adjust: exact; print-color-adjust: exact; forced-color-adjust: none; }}
        body {{ margin: 0; background: {bodyBackground}; font-family: 'Cairo', 'Segoe UI', sans-serif; color: #3d434a; font-size: 14px; }}
        body, body * {{ font-family: 'Cairo', 'Segoe UI', sans-serif; }}
        .sheet {{ width: 100%; min-height: calc(297mm - 8mm); margin: 0 auto; background: #ffffff; padding: 4mm 4.5mm 5mm 4.5mm; display: flex; flex-direction: column; }}
        .content-section {{ display: block; }}
        .hero {{ position: relative; background: linear-gradient(135deg, {heroGradientStart} 0%, {heroGradientEnd} 100%); border: 1px solid {heroBorder}; border-radius: 22px; padding: 2.9mm 4.2mm; margin-bottom: 2.8mm; break-inside: avoid; page-break-inside: avoid; }}
        .hero-grid {{ display: grid; grid-template-columns: 28mm minmax(0, 1fr) 58mm; gap: 3.2mm; align-items: start; direction: ltr; }}
        .hero-logo-wrap {{ display: flex; align-items: center; justify-content: flex-start; min-height: 100%; }}
        .hero-logo {{ max-width: 28mm; max-height: 21mm; width: auto; height: auto; object-fit: contain; }}
        .hero-brand {{ direction: rtl; text-align: center; padding: 0 4mm; }}
        .brand-name {{ font-size: 24px; font-weight: 800; color: #3d434a; margin: 0 0 0.8mm 0; }}
        .brand-subtitle {{ font-size: 15px; font-weight: 700; color: #3d434a; margin: 0 0 1.2mm 0; }}
        .brand-line {{ font-size: 13px; color: #3d434a; margin: 0.45mm 0; }}
        .tax-line {{ font-size: 13px; font-weight: 700; color: #3d434a; margin-top: 1mm; }}
        .hero-contact {{ direction: rtl; text-align: right; background: rgba(255, 255, 255, 0.58); border: 1px solid #b9def2; border-radius: 16px; padding: 1.9mm 2.5mm; }}
        .contact-line {{ font-size: 12px; font-weight: 700; color: #3d434a; line-height: 1.45; white-space: nowrap; }}
        .contact-line + .contact-line {{ margin-top: 0.5mm; }}
        .invoice-heading-row {{ display: grid; grid-template-columns: 1fr auto 1fr; align-items: start; gap: 4mm; margin: 0 0 2.1mm 0; direction: ltr; break-inside: avoid; page-break-inside: avoid; }}
        .invoice-heading-block {{ display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; direction: rtl; }}
        .invoice-heading-spacer {{ min-height: 1px; }}
        .invoice-main-title {{ font-size: 23px; font-weight: 800; letter-spacing: 0.8px; color: #3d434a; line-height: 1; margin-bottom: 0.8mm; direction: ltr; }}
        .invoice-sub-title {{ font-size: 16px; font-weight: 700; color: #3d434a; line-height: 1; margin-bottom: 1.2mm; direction: rtl; text-align: center; width: 100%; }}
        .invoice-title-line {{ width: 30mm; border-top: 2px solid {titleAccent}; }}
        .meta-grid {{ display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 4mm; }}
        .meta-card, .client-card, .total-card {{ border: 1px solid #cfeaf8; border-radius: 18px; background: #ffffff; padding: 3.2mm 3.8mm; }}
        .meta-label, .section-label {{ font-size: 12.5px; color: #3d434a; margin-bottom: 1mm; }}
        .meta-value {{ font-size: 17px; font-weight: 800; color: #3d434a; }}
        .client-row {{ display: grid; grid-template-columns: 0.78fr 1.22fr; gap: 4mm; align-items: stretch; margin-bottom: 2.8mm; direction: ltr; break-inside: avoid; page-break-inside: avoid; }}
        .client-card {{ background: linear-gradient(135deg, {metaGradientStart} 0%, {metaGradientEnd} 100%); border: 1px solid {metaBorder}; direction: rtl; text-align: right; display: flex; flex-direction: column; justify-content: center; }}
        .client-side-card {{ background: #f4fbff; direction: rtl; text-align: right; display: flex; align-items: stretch; }}
        .client-title {{ font-size: 14.5px; font-weight: 800; color: #3d434a; margin: 0 0 1.4mm 0; text-align: right; }}
        .client-name-wrap {{ background: transparent; border-radius: 0; padding: 0; margin: 0 0 1.2mm 0; text-align: right; }}
        .client-name {{ font-size: 19.5px; font-weight: 800; color: #3d434a; margin: 0 0 1.1mm 0; line-height: 1.4; text-align: right; }}
        .muted {{ color: #3d434a; font-size: 14px; }}
        .id-text {{ color: #3d434a; font-size: 13.8px; font-weight: 700; margin-top: 0; text-align: right; }}
        .side-meta-wrap {{ width: 100%; background: linear-gradient(135deg, {metaGradientStart} 0%, {metaGradientEnd} 100%); border: 1px solid {metaBorder}; border-radius: 14px; padding: 2.5mm 3mm; }}
        .side-meta-item + .side-meta-item {{ margin-top: 2mm; padding-top: 2mm; border-top: 1px solid #9fcee9; }}
        .side-meta-item {{ display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: 3mm; direction: rtl; background: transparent; border-radius: 0; padding: 0; }}
        .side-meta-item .meta-label {{ margin: 0; text-align: right; direction: rtl; font-size: 15px; font-weight: 800; color: #3d434a; }}
        .side-meta-item .meta-value {{ min-width: 26mm; text-align: left; direction: ltr; font-size: 15px; font-weight: 800; color: #3d434a; }}
        .employee-line {{ color: {employeeColor}; font-size: 14px; font-weight: 800; direction: rtl; text-align: right; align-self: center; justify-self: end; white-space: nowrap; }}
        table {{ width: 100%; border-collapse: separate; border-spacing: 0; table-layout: fixed; margin-top: 1.2mm; border: 1px solid #cae8f9; border-radius: 18px; overflow: hidden; font-family: 'Cairo', 'Segoe UI', sans-serif; }}
        thead th {{ background: linear-gradient(135deg, {tableHeaderStart} 0%, {tableHeaderEnd} 100%); color: #3d434a; font-size: 15px; font-weight: 800; padding: 3.6mm 2.5mm; border-bottom: 1px solid {tableHeaderBorder}; font-family: 'Cairo', 'Segoe UI', sans-serif; }}
        tbody td {{ padding: 3.6mm 2.5mm; border-bottom: 1px solid #e7f4fb; font-size: 14px; vertical-align: top; font-family: 'Cairo', 'Segoe UI', sans-serif; color: #3d434a; }}
        tbody tr:nth-child(odd) td {{ background: #fafdff; }}
        tbody tr:nth-child(even) td {{ background: #f1f9fe; }}
        tbody tr:last-child td {{ border-bottom: 0; }}
        .num {{ text-align: center; }}
        .service {{ text-align: right; }}
        .gov {{ color: #17873a; font-weight: 700; }}
        .total {{ color: #3d434a; font-weight: 800; }}
        .stamp-wrap {{ display: flex; justify-content: center; margin-top: 3mm; break-inside: avoid; page-break-inside: avoid; }}
        .stamp-image {{ max-width: 34mm; max-height: 34mm; width: auto; height: auto; object-fit: contain; }}
        .footer-row {{ display: flex; justify-content: flex-end; align-items: center; gap: 4mm; margin-top: 2.2mm; break-inside: avoid; page-break-inside: avoid; }}
        .total-card {{ min-width: 54mm; border: 1px solid {totalBorder}; border-radius: 14px; background: {totalBackground}; color: {darkSurfaceText}; padding: 2.4mm 3mm; }}
        .total-label {{ font-size: 12.5px; color: #3d434a; margin-bottom: 0.8mm; font-weight: 700; }}
        .total-value {{ font-size: 19px; font-weight: 700; color: {darkSurfaceText}; }}
        .bottom-section {{ margin-top: auto; display: flex; flex-direction: column; gap: 4mm; }}
        .signature-row {{ display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18mm; margin-top: 0; align-items: end; direction: rtl; break-inside: avoid; page-break-inside: avoid; }}
        .signature-block {{ text-align: center; direction: rtl; }}
        .signature-line {{ width: 88%; margin: 0 auto; border-top: 2px dotted {signatureBorder}; padding-top: 3.2mm; color: #3d434a; direction: rtl; }}
        .signature-ar {{ font-size: 13px; font-weight: 700; margin-bottom: 1.2mm; }}
        .signature-en {{ font-size: 15px; font-weight: 800; }}
        .invoice-footer {{ margin-top: 0; background: linear-gradient(135deg, {footerGradientStart} 0%, {footerGradientEnd} 100%); border: 1px solid {footerBorder}; border-radius: 18px; padding: 3.1mm 5mm; text-align: center; color: {footerTextColor}; font-size: 14px; font-weight: 700; direction: rtl; break-inside: avoid; page-break-inside: avoid; }}
        .invoice-footer-text {{ display: inline-flex; align-items: center; justify-content: center; gap: 3mm; flex-wrap: wrap; direction: rtl; }}
        @media print {{
            body {{ background: #ffffff !important; }}
            html, body {{ width: 210mm; min-height: 297mm; }}
            .sheet {{ width: 100%; min-height: calc(297mm - 8mm); margin: 0; padding: 1.5mm 1.8mm 2mm 1.8mm; }}
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
                <div class='hero-brand'>
                    <div class='brand-name'>{Escape(CompanyName)}</div>
                    <div class='brand-subtitle'>{Escape(CompanySubtitle)}</div>
                    <div class='tax-line'>{Escape(TaxNumber)}</div>
                </div>
                <div class='hero-contact'>
                    {phoneLinesHtml}
                    <div class='contact-line'>{Escape(CompanyEmail)}</div>
                    <div class='contact-line'>{Escape(CompanyAddress)}</div>
                </div>
            </div>
        </div>

        <div class='client-row'>
            <div class='meta-card client-side-card'>
                <div class='side-meta-wrap'>
                    <div class='side-meta-item'>
                        <div class='meta-label'>رقم الفاتورة / Invoice No</div>
                        <div class='meta-value'>{Escape(InvoiceNumber)}</div>
                    </div>
                    <div class='side-meta-item'>
                        <div class='meta-label'>التاريخ / Date</div>
                        <div class='meta-value'>{Escape(InvoiceDate)}</div>
                    </div>
                </div>
            </div>
            <div class='client-card'>
                <div class='client-title'>بيانات العميل / Client Details</div>
                <div class='client-name'>{Escape(CustomerName)}</div>
                <div class='id-text'>رقم العميل / Customer ID: {Escape(CustomerIdText)}</div>
            </div>
        </div>

        <div class='invoice-heading-row'>
            <div class='invoice-heading-spacer'></div>
            <div class='invoice-heading-block'>
                <div class='invoice-main-title'>{Escape(templateTitle)}</div>
                <div class='invoice-sub-title'>{Escape(templateSubtitle)}</div>
                <div class='invoice-title-line'></div>
            </div>
            <div class='employee-line'>اسم الموظف / Employee: {Escape(EmployeeName)}</div>
        </div>

        <table>
            <thead>
                <tr>
                    {tableHeadColumns}
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
