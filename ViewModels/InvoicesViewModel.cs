using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Data;
using ProWalid.Models;
using ProWalid.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProWalid.ViewModels
{
    public partial class InvoicesViewModel : ObservableObject
    {
        private const long GroupedInvoiceCustomerId = 3;
        private static readonly HashSet<long> FinalAggregationCustomerIds = new() { 2, 4, 7, 8 };

        private readonly DatabaseHelper _databaseHelper;
        private Frame? _frame;
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private ObservableCollection<InvoiceSummaryRow> invoiceRows = new();

        [ObservableProperty]
        private string pageSubtitle = "ملخص الفواتير والمعاملات المحفوظة لجميع العملاء";

        [ObservableProperty]
        private int savedInvoicesCount;

        public string HeaderTitle => _selectedCustomer == null ? "الفواتير والتقارير" : $"الفواتير والتقارير - {_selectedCustomer.Name}";

        public int InvoicesCount => InvoiceRows.Count;

        public double TotalAmount => InvoiceRows.Sum(row => row.TotalAmount);

        public string TotalAmountText => $"إجمالي المبالغ: {TotalAmount:N2} درهم";

        public string CustomerScopeText => _selectedCustomer == null ? "كل العملاء" : $"العميل المحدد: {_selectedCustomer.Name}";

        public string SavedInvoicesBadgeText => SavedInvoicesCount.ToString();

        public int SelectedInvoicesCount => InvoiceRows.Count(row => row.IsSelected);

        public double SelectedPrintingFeesTotal => InvoiceRows.Where(row => row.IsSelected).Sum(row => row.PrintingFeesAmount);

        public string SelectedPrintingFeesTotalText => $"رسوم الطباعة المحددة: {SelectedPrintingFeesTotal:N2} درهم";

        public InvoicesViewModel()
        {
            _databaseHelper = new DatabaseHelper();
            InvoiceRows.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(InvoicesCount));
                OnPropertyChanged(nameof(TotalAmount));
                OnPropertyChanged(nameof(TotalAmountText));
                OnPropertyChanged(nameof(SavedInvoicesBadgeText));
                OnPropertyChanged(nameof(SelectedInvoicesCount));
                OnPropertyChanged(nameof(SelectedPrintingFeesTotal));
                OnPropertyChanged(nameof(SelectedPrintingFeesTotalText));
            };
        }

        public void SetFrame(Frame frame)
        {
            _frame = frame;
        }

        public async Task LoadAsync(Customer? customer)
        {
            _selectedCustomer = customer;
            PageSubtitle = customer == null
                ? "ملخص الفواتير والمعاملات المحفوظة لجميع العملاء"
                : $"ملخص الفواتير والمعاملات المحفوظة للعميل: {customer.Name}";

            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(CustomerScopeText));

            var transactions = await _databaseHelper.GetAllTransactionsAsync();
            var customers = await _databaseHelper.GetAllCustomersAsync();
            var customerLookup = customers.ToDictionary(c => c.Id, c => c.Name);

            var filteredTransactions = transactions
                .Where(transaction => customer == null || transaction.CustomerId == customer.Id)
                .OrderByDescending(transaction => transaction.TransactionDate)
                .ThenByDescending(transaction => transaction.InvoiceNumber)
                .ToList();

            SavedInvoicesCount = await _databaseHelper.GetSavedInvoicesCountAsync(customer?.Id);

            foreach (var row in InvoiceRows)
            {
                row.PropertyChanged -= InvoiceRow_PropertyChanged;
            }

            InvoiceRows.Clear();

            var serial = 1;
            foreach (var transaction in filteredTransactions)
            {
                var customerName = customerLookup.TryGetValue(transaction.CustomerId, out var name)
                    ? name
                    : "غير محدد";

                var row = new InvoiceSummaryRow
                {
                    SerialNumber = serial++,
                    InvoiceNumber = transaction.InvoiceNumber,
                    TransactionDateText = transaction.TransactionDate.ToString("yyyy-MM-dd"),
                    CustomerName = customerName,
                    CompanyName = transaction.CompanyName,
                    EmployeeName = transaction.EmployeeName,
                    Status = "محفوظة",
                    TotalAmount = transaction.GrandTotal,
                    ItemsCount = transaction.ItemsCount,
                    Transaction = transaction
                };

                row.PropertyChanged += InvoiceRow_PropertyChanged;
                InvoiceRows.Add(row);
            }

            OnPropertyChanged(nameof(InvoicesCount));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(TotalAmountText));
            OnPropertyChanged(nameof(SavedInvoicesBadgeText));
            OnPropertyChanged(nameof(SelectedInvoicesCount));
            OnPropertyChanged(nameof(SelectedPrintingFeesTotal));
            OnPropertyChanged(nameof(SelectedPrintingFeesTotalText));
        }

        private void InvoiceRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InvoiceSummaryRow.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedInvoicesCount));
                OnPropertyChanged(nameof(SelectedPrintingFeesTotal));
                OnPropertyChanged(nameof(SelectedPrintingFeesTotalText));
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            if (_frame != null && _frame.CanGoBack)
            {
                _frame.GoBack();
                return;
            }

            _frame?.Navigate(typeof(TransactionPage));
        }

        [RelayCommand]
        private async Task ShowSavedInvoicesAsync()
        {
            _frame?.Navigate(typeof(SavedInvoicesPage), _selectedCustomer);
        }

        [RelayCommand]
        private void ShowInvoicePreview()
        {
            _frame?.Navigate(typeof(InvoicePreviewPage), InvoiceRows.FirstOrDefault());
        }

        [RelayCommand]
        private async Task ShowGroupedInvoiceAsync()
        {
            var selectedRows = InvoiceRows
                .Where(row => row.IsSelected)
                .ToList();

            if (_selectedCustomer != null && _selectedCustomer.Id != GroupedInvoiceCustomerId)
            {
                await ShowMessageAsync("فاتورة مجمعة", "فاتورة مجمعة مفعلة فقط للعميل ذي المعرف 3.");
                return;
            }

            if (selectedRows.Count < 2)
            {
                await ShowMessageAsync("فاتورة مجمعة", "يجب تحديد معاملتين أو أكثر لإنشاء فاتورة مجمعة.");
                return;
            }

            var customerIds = selectedRows
                .Select(row => row.Transaction.CustomerId)
                .Distinct()
                .ToList();

            if (customerIds.Count != 1 || customerIds[0] != GroupedInvoiceCustomerId)
            {
                await ShowMessageAsync("فاتورة مجمعة", "الفاتورة المجمعة متاحة فقط عند تحديد معاملات العميل 3 فقط.");
                return;
            }

            var groupedItems = selectedRows
                .SelectMany(row => row.Transaction.Items.Select(item => new
                {
                    Row = row,
                    Item = item,
                    Employee = string.IsNullOrWhiteSpace(row.EmployeeName) ? row.Transaction.EmployeeName : row.EmployeeName
                }))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Item.ServiceName))
                .GroupBy(entry => new
                {
                    ServiceName = entry.Item.ServiceName.Trim(),
                    UnitPrice = Math.Round(entry.Item.UnitPrice, 2)
                })
                .OrderBy(group => group.Key.ServiceName)
                .ThenBy(group => group.Key.UnitPrice)
                .Select((group, index) => new InvoicePreviewLineItem
                {
                    LineNumber = index + 1,
                    Description = group.Key.ServiceName,
                    Quantity = group.Sum(entry => entry.Item.Quantity),
                    UnitPrice = group.First().Item.UnitPrice,
                    EmployeeNames = string.Join(" - ", group
                        .Select(entry => entry.Employee)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase))
                })
                .ToList();

            if (groupedItems.Count == 0)
            {
                await ShowMessageAsync("فاتورة مجمعة", "لم يتم العثور على بنود صالحة للتجميع داخل معاملات العميل 3.");
                return;
            }

            var customer = (await _databaseHelper.GetAllCustomersAsync())
                .FirstOrDefault(item => item.Id == GroupedInvoiceCustomerId);

            var displayCompanyName = selectedRows
                .Select(row => !string.IsNullOrWhiteSpace(row.Transaction.CompanyName) ? row.Transaction.CompanyName : row.CompanyName)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? selectedRows.First().CustomerName;

            var request = new GroupedInvoicePreviewRequest
            {
                CustomerId = GroupedInvoiceCustomerId,
                CustomerName = customer?.Name ?? selectedRows.First().CustomerName,
                CompanyName = displayCompanyName,
                CustomerIdText = customer == null
                    ? $"ID {GroupedInvoiceCustomerId}"
                    : $"ID {(customer.CustomerNumber > 0 ? customer.CustomerNumber : customer.Id)}",
                InvoiceNumber = $"GRP-{GroupedInvoiceCustomerId}-{DateTime.Now:yyyyMMddHHmm}",
                InvoiceDate = DateTimeOffset.Now,
                Notes = $"Grouped invoice for customer 3 based on {selectedRows.Count} selected transactions. Similar items are merged using service name and unit price, with employee names listed for each grouped line before the total.",
                Items = groupedItems,
                SourceInvoiceNumbers = selectedRows.Select(row => row.InvoiceNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };

            _frame?.Navigate(typeof(InvoicePreviewPage), request);
        }

        [RelayCommand]
        private async Task ShowInvoiceNumberTwoAsync()
        {
            var secondInvoice = InvoiceRows.Skip(1).FirstOrDefault();
            if (secondInvoice == null)
            {
                await ShowMessageAsync("فاتورة رقم 2", "لا توجد فاتورة ثانية ضمن القائمة الحالية.");
                return;
            }

            await ShowInvoiceAsync(secondInvoice);
        }

        [RelayCommand]
        private async Task ShowFinalAggregationAsync()
        {
            var selectedRows = InvoiceRows.Where(row => row.IsSelected).ToList();
            if (selectedRows.Count == 0)
            {
                await ShowMessageAsync("التجميع النهائي", "حدد فاتورة واحدة على الأقل لإنشاء كشف التجميع النهائي.");
                return;
            }

            var customerIds = selectedRows
                .Select(row => row.Transaction.CustomerId)
                .Distinct()
                .ToList();

            if (customerIds.Count != 1)
            {
                await ShowMessageAsync("التجميع النهائي", "يجب أن تكون كل الفواتير المحددة لنفس العميل فقط.");
                return;
            }

            if (!FinalAggregationCustomerIds.Contains(customerIds[0]))
            {
                await ShowMessageAsync("التجميع النهائي", "هذا النموذج متاح فقط للعملاء ذوي المعرفات: 2، 4، 7، 8.");
                return;
            }

            _frame?.Navigate(typeof(InvoicePreviewPage), selectedRows);
        }

        [RelayCommand]
        private async Task ShowTransactionStatementAsync()
        {
            if (InvoiceRows.Count == 0)
            {
                await ShowMessageAsync("كشف المعاملات", "لا توجد معاملات لعرض الكشف.");
                return;
            }

            var text = new StringBuilder();
            text.AppendLine("كشف المعاملات");
            text.AppendLine(string.Empty);

            foreach (var row in InvoiceRows)
            {
                text.AppendLine($"- {row.InvoiceNumber} | {row.TransactionDateText} | {row.CustomerName} | {row.TotalAmount:N2} درهم");
            }

            await ShowMessageAsync("كشف المعاملات", text.ToString());
        }

        [RelayCommand]
        private async Task ViewTransactionAsync(InvoiceSummaryRow? row)
        {
            if (row?.Transaction == null)
            {
                return;
            }

            var text = new StringBuilder();
            text.AppendLine($"رقم الفاتورة: {row.InvoiceNumber}");
            text.AppendLine($"التاريخ: {row.TransactionDateText}");
            text.AppendLine($"العميل: {row.CustomerName}");
            text.AppendLine($"الشركة: {row.CompanyName}");
            text.AppendLine($"الموظف: {row.EmployeeName}");
            text.AppendLine($"عدد البنود: {row.ItemsCount}");
            text.AppendLine($"الإجمالي: {row.TotalAmount:N2} درهم");
            text.AppendLine(string.Empty);
            text.AppendLine("تفاصيل البنود:");

            foreach (var item in row.Transaction.Items)
            {
                var govFeesSegment = string.IsNullOrWhiteSpace(item.GovFees)
                    ? string.Empty
                    : $" | GOV-FEES: {item.GovFees}";

                text.AppendLine($"- {item.ServiceName} | العدد: {item.Quantity} | سعر الوحدة: {item.UnitPrice:N2}{govFeesSegment} | الإجمالي: {item.Total:N2}");
            }

            await ShowMessageAsync("عرض المعاملة", text.ToString());
        }

        [RelayCommand]
        private Task ShowInvoiceAsync(InvoiceSummaryRow? row)
        {
            if (row?.Transaction == null)
            {
                return Task.CompletedTask;
            }

            _frame?.Navigate(typeof(InvoicePreviewPage), row);
            return Task.CompletedTask;
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new ScrollViewer
                {
                    MaxHeight = 520,
                    Content = new TextBlock
                    {
                        Text = message,
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                        MaxWidth = 720,
                        FontSize = 15,
                        LineHeight = 28
                    }
                },
                CloseButtonText = "إغلاق",
                XamlRoot = _frame?.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
