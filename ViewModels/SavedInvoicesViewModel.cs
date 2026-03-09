using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Data;
using ProWalid.Models;
using ProWalid.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProWalid.ViewModels
{
    public partial class SavedInvoicesViewModel : ObservableObject
    {
        private readonly DatabaseHelper _databaseHelper = new();
        private Frame? _frame;
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private ObservableCollection<SavedInvoiceRecord> savedInvoiceRows = new();

        [ObservableProperty]
        private string pageSubtitle = "الفواتير المحفوظة لجميع العملاء";

        public string HeaderTitle => _selectedCustomer == null ? "الفواتير المحفوظة" : $"الفواتير المحفوظة - {_selectedCustomer.Name}";

        public int SavedInvoicesCount => SavedInvoiceRows.Count;

        public double TotalAmount => SavedInvoiceRows.Sum(row => row.TotalAmount);

        public string TotalAmountText => $"إجمالي الفواتير المحفوظة: {TotalAmount:N2} درهم";

        public void SetFrame(Frame frame)
        {
            _frame = frame;
        }

        public async Task LoadAsync(Customer? customer)
        {
            _selectedCustomer = customer;
            PageSubtitle = customer == null
                ? "الفواتير المحفوظة لجميع العملاء"
                : $"الفواتير المحفوظة للعميل: {customer.Name}";

            OnPropertyChanged(nameof(HeaderTitle));

            var rows = await _databaseHelper.GetAllSavedInvoicesAsync(customer?.Id);
            var displayRows = BuildDisplayRows(rows);

            SavedInvoiceRows.Clear();
            foreach (var row in displayRows)
            {
                SavedInvoiceRows.Add(row);
            }

            OnPropertyChanged(nameof(SavedInvoicesCount));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(TotalAmountText));
        }

        private static List<SavedInvoiceRecord> BuildDisplayRows(IReadOnlyList<SavedInvoiceRecord> rows)
        {
            var displayRows = new List<SavedInvoiceRecord>();

            var groupedRows = rows
                .Where(row => row.IsGrouped)
                .GroupBy(row => new
                {
                    row.GroupedSequenceNumber,
                    GroupKey = string.IsNullOrWhiteSpace(row.RootInvoiceNumber) ? row.SavedInvoiceNumber : row.RootInvoiceNumber
                })
                .Select(group =>
                {
                    var first = group.First();
                    var sourceInvoiceNumbers = group
                        .Select(item => string.IsNullOrWhiteSpace(item.SourceInvoiceNumber) ? item.SavedInvoiceNumber : item.SourceInvoiceNumber)
                        .Where(number => !string.IsNullOrWhiteSpace(number))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(number => number, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return new SavedInvoiceRecord
                    {
                        Id = first.Id,
                        SavedInvoiceNumber = first.SavedInvoiceNumber,
                        RootInvoiceNumber = string.IsNullOrWhiteSpace(first.RootInvoiceNumber) ? first.SavedInvoiceNumber : first.RootInvoiceNumber,
                        SourceInvoiceNumber = first.SourceInvoiceNumber,
                        GroupedSequenceNumber = first.GroupedSequenceNumber,
                        SavedKind = first.SavedKind,
                        TemplateKey = first.TemplateKey,
                        CustomerId = first.CustomerId,
                        CustomerName = first.CustomerName,
                        CompanyName = first.CompanyName,
                        InvoiceDateText = first.InvoiceDateText,
                        TotalAmount = first.TotalAmount,
                        Notes = first.Notes,
                        PrintHtml = first.PrintHtml,
                        PayloadJson = first.PayloadJson,
                        SavedAt = first.SavedAt,
                        GroupedInvoicesCount = sourceInvoiceNumbers.Count,
                        GroupedInvoiceNumbersSummary = string.Join(Environment.NewLine, sourceInvoiceNumbers)
                    };
                });

            var singleRows = rows
                .Where(row => !row.IsGrouped)
                .Select(row => new SavedInvoiceRecord
                {
                    Id = row.Id,
                    SavedInvoiceNumber = row.SavedInvoiceNumber,
                    RootInvoiceNumber = row.RootInvoiceNumber,
                    SourceInvoiceNumber = row.SourceInvoiceNumber,
                    GroupedSequenceNumber = row.GroupedSequenceNumber,
                    SavedKind = row.SavedKind,
                    TemplateKey = row.TemplateKey,
                    CustomerId = row.CustomerId,
                    CustomerName = row.CustomerName,
                    CompanyName = row.CompanyName,
                    InvoiceDateText = row.InvoiceDateText,
                    TotalAmount = row.TotalAmount,
                    Notes = row.Notes,
                    PrintHtml = row.PrintHtml,
                    PayloadJson = row.PayloadJson,
                    SavedAt = row.SavedAt,
                    GroupedInvoicesCount = 1,
                    GroupedInvoiceNumbersSummary = string.IsNullOrWhiteSpace(row.SourceInvoiceNumber) ? row.SavedInvoiceNumber : row.SourceInvoiceNumber
                });

            displayRows.AddRange(groupedRows);
            displayRows.AddRange(singleRows);

            var orderedRows = displayRows
                .OrderByDescending(row => row.SavedAt)
                .ThenByDescending(row => row.Id)
                .ToList();

            for (var index = 0; index < orderedRows.Count; index++)
            {
                orderedRows[index].SerialNumber = index + 1;
            }

            return orderedRows;
        }

        [RelayCommand]
        private void GoBack()
        {
            if (_frame != null && _frame.CanGoBack)
            {
                _frame.GoBack();
                return;
            }

            _frame?.Navigate(typeof(InvoicesPage), _selectedCustomer);
        }

        [RelayCommand]
        private Task ViewSavedInvoiceAsync(SavedInvoiceRecord? record)
        {
            if (record == null)
            {
                return Task.CompletedTask;
            }

            _frame?.Navigate(typeof(InvoicePreviewPage), new SavedInvoicePreviewRequest
            {
                Record = record,
                AutoPrint = false
            });

            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task PrintSavedInvoiceAsync(SavedInvoiceRecord? record)
        {
            if (record == null)
            {
                return Task.CompletedTask;
            }

            _frame?.Navigate(typeof(InvoicePreviewPage), new SavedInvoicePreviewRequest
            {
                Record = record,
                AutoPrint = true
            });

            return Task.CompletedTask;
        }

        [RelayCommand]
        private async Task ShowGroupedInvoicesAsync(SavedInvoiceRecord? record)
        {
            if (record == null || !record.CanShowGroupedInvoiceNumbers)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = $"الفواتير الأصلية داخل التجميعة {record.DisplayInvoiceNumber}",
                Content = new ScrollViewer
                {
                    MaxHeight = 420,
                    Content = new TextBlock
                    {
                        Text = record.GroupedInvoiceNumbersSummary,
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                        FontSize = 15,
                        LineHeight = 28,
                        MaxWidth = 540
                    }
                },
                CloseButtonText = "إغلاق",
                XamlRoot = _frame?.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
