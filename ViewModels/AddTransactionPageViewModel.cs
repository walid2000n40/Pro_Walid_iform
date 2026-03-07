using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Models;
using ProWalid.Data;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;

namespace ProWalid.ViewModels
{
    public partial class AddTransactionPageViewModel : ObservableObject
    {
        private static int _lastInvoiceNumber = 811;
        private Frame _frame;
        private Transaction _originalTransaction;
        private bool _isEditMode;
        private readonly DatabaseHelper _databaseHelper;
        private readonly AttachmentManager _attachmentManager;

        [ObservableProperty]
        private string invoiceNumber;

        [ObservableProperty]
        private string companyName = string.Empty;

        [ObservableProperty]
        private string employeeName = string.Empty;

        [ObservableProperty]
        private DateTimeOffset transactionDate = DateTimeOffset.Now;

        [ObservableProperty]
        private ObservableCollection<TransactionItemDetail> items = new();

        [ObservableProperty]
        private string pageTitle = "إضافة معاملة جديدة";

        public double GrandTotal => Items.Sum(item => item.Total + item.Profit - item.Discount);

        public AddTransactionPageViewModel()
        {
            _databaseHelper = new DatabaseHelper();
            _attachmentManager = new AttachmentManager();
            
            InitializeAsync();

            Items.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(GrandTotal));
            };

            AddItemCommand.Execute(null);
        }

        private async void InitializeAsync()
        {
            _lastInvoiceNumber = await _databaseHelper.GetNextInvoiceNumberAsync();
            InvoiceNumber = _lastInvoiceNumber.ToString();
        }

        public void SetFrame(Frame frame)
        {
            _frame = frame;
        }

        public void LoadTransaction(Transaction transaction)
        {
            if (transaction != null)
            {
                _isEditMode = true;
                _originalTransaction = transaction;
                PageTitle = "تعديل معاملة";

                InvoiceNumber = transaction.InvoiceNumber;
                CompanyName = transaction.CompanyName;
                EmployeeName = transaction.EmployeeName;
                TransactionDate = transaction.TransactionDate;

                Items.Clear();
                foreach (var item in transaction.Items)
                {
                    var newItem = new TransactionItemDetail
                    {
                        ServiceName = item.ServiceName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Profit = item.Profit,
                        Discount = item.Discount,
                        AttachmentPath = item.AttachmentPath
                    };

                    newItem.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(TransactionItemDetail.Total) ||
                            e.PropertyName == nameof(TransactionItemDetail.Profit) ||
                            e.PropertyName == nameof(TransactionItemDetail.Discount))
                        {
                            OnPropertyChanged(nameof(GrandTotal));
                        }
                    };

                    Items.Add(newItem);
                }
            }
        }

        [RelayCommand]
        private void AddItem()
        {
            var newItem = new TransactionItemDetail();
            newItem.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TransactionItemDetail.Total) ||
                    e.PropertyName == nameof(TransactionItemDetail.Profit) ||
                    e.PropertyName == nameof(TransactionItemDetail.Discount))
                {
                    OnPropertyChanged(nameof(GrandTotal));
                }
            };
            Items.Add(newItem);
        }

        [RelayCommand]
        private void RemoveItem(TransactionItemDetail item)
        {
            if (Items.Count > 1)
            {
                Items.Remove(item);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            var transaction = new Transaction
            {
                InvoiceNumber = InvoiceNumber,
                CompanyName = CompanyName,
                EmployeeName = EmployeeName,
                TransactionDate = TransactionDate
            };

            foreach (var item in Items)
            {
                transaction.Items.Add(new TransactionItemDetail
                {
                    ServiceName = item.ServiceName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Profit = item.Profit,
                    Discount = item.Discount,
                    AttachmentPath = item.AttachmentPath
                });
            }

            await _databaseHelper.SaveTransactionAsync(transaction);
            
            TransactionViewModel.Instance?.AddOrUpdateTransaction(transaction);

            if (_frame != null && _frame.CanGoBack)
            {
                _frame.GoBack();
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            if (_frame != null && _frame.CanGoBack)
            {
                _frame.GoBack();
            }
        }

        [RelayCommand]
        private async Task PickFileAsync(TransactionItemDetail item)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add("*");
                
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    var savedPath = await _attachmentManager.SaveAttachmentAsync(file.Path);
                    if (!string.IsNullOrEmpty(savedPath))
                    {
                        item.AttachmentPath = savedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error picking file: {ex.Message}");
            }
        }
    }
}
