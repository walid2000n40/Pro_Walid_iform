using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Data;
using ProWalid.Models;
using ProWalid.Views;
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

            SavedInvoiceRows.Clear();
            foreach (var row in rows)
            {
                SavedInvoiceRows.Add(row);
            }

            OnPropertyChanged(nameof(SavedInvoicesCount));
            OnPropertyChanged(nameof(TotalAmount));
            OnPropertyChanged(nameof(TotalAmountText));
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
    }
}
