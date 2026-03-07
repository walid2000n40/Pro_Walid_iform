using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Models;
using ProWalid.Views;
using ProWalid.Data;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace ProWalid.ViewModels
{
    public partial class TransactionViewModel : ObservableObject
    {
        private static TransactionViewModel _instance;
        private Frame _frame;
        private readonly DatabaseHelper _databaseHelper;
        private readonly AttachmentManager _attachmentManager;

        [ObservableProperty]
        private ObservableCollection<Transaction> transactions = new();

        [ObservableProperty]
        private Transaction? selectedTransaction;

        [ObservableProperty]
        private TransactionItemWithDetails? selectedItem;

        [ObservableProperty]
        private ObservableCollection<Customer> customers = new();

        [ObservableProperty]
        private Customer? selectedCustomer;

        [ObservableProperty]
        private bool isCustomerPanelVisible;

        public ObservableCollection<TransactionItemWithDetails> AllItems { get; } = new();

        public int TransactionCount => Transactions.Count;

        public double TotalAmount => Transactions.Sum(t => t.GrandTotal);

        public TransactionViewModel()
        {
            _instance = this;
            _databaseHelper = new DatabaseHelper();
            _attachmentManager = new AttachmentManager();
            
            Transactions.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TransactionCount));
                OnPropertyChanged(nameof(TotalAmount));
                RefreshAllItems();
            };

            LoadTransactionsAsync();
            LoadCustomersAsync();
        }

        private async void LoadCustomersAsync()
        {
            var loadedCustomers = await _databaseHelper.GetAllCustomersAsync();
            foreach (var customer in loadedCustomers)
            {
                Customers.Add(customer);
            }
        }

        [RelayCommand]
        private void ToggleCustomerPanel()
        {
            IsCustomerPanelVisible = !IsCustomerPanelVisible;
        }

        [RelayCommand]
        private void AddCustomer()
        {
            var newCustomer = new Customer { Name = "عميل جديد" };
            Customers.Add(newCustomer);
            SelectedCustomer = newCustomer;
        }

        [RelayCommand]
        private async Task SaveCustomerAsync()
        {
            if (SelectedCustomer == null) return;

            var customerId = await _databaseHelper.SaveCustomerAsync(SelectedCustomer);
            SelectedCustomer.Id = customerId;
        }

        [RelayCommand]
        private async Task DeleteCustomerAsync()
        {
            if (SelectedCustomer == null) return;

            await _databaseHelper.DeleteCustomerAsync(SelectedCustomer.Id);
            Customers.Remove(SelectedCustomer);
            SelectedCustomer = null;
        }

        public static TransactionViewModel Instance => _instance;

        private async void LoadTransactionsAsync()
        {
            var loadedTransactions = await _databaseHelper.GetAllTransactionsAsync();
            foreach (var transaction in loadedTransactions)
            {
                Transactions.Add(transaction);
            }
        }

        private void RefreshAllItems()
        {
            AllItems.Clear();
            foreach (var transaction in Transactions)
            {
                bool isFirst = true;
                foreach (var item in transaction.Items)
                {
                    var itemWithDetails = new TransactionItemWithDetails
                    {
                        ItemId = item.Id,
                        ServiceName = item.ServiceName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Profit = item.Profit,
                        Discount = item.Discount,
                        CompanyName = transaction.CompanyName,
                        EmployeeName = transaction.EmployeeName,
                        AttachmentPath = item.AttachmentPath,
                        InvoiceNumber = transaction.InvoiceNumber,
                        IsFirstItemInTransaction = isFirst,
                        TransactionTotal = transaction.GrandTotal,
                        TransactionStatus = "مكتملة"
                    };

                    foreach (var attachment in item.Attachments)
                    {
                        itemWithDetails.Attachments.Add(attachment);
                    }

                    itemWithDetails.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(TransactionItemWithDetails.IsSelected))
                        {
                            UpdateTransactionSelection(itemWithDetails);
                        }
                    };

                    AllItems.Add(itemWithDetails);
                    isFirst = false;
                }
            }
        }

        private void UpdateTransactionSelection(TransactionItemWithDetails changedItem)
        {
            var invoiceNumber = changedItem.InvoiceNumber;
            var transactionItems = AllItems.Where(i => i.InvoiceNumber == invoiceNumber).ToList();
            
            foreach (var item in transactionItems)
            {
                if (item != changedItem)
                {
                    item.IsSelected = changedItem.IsSelected;
                }
            }

            if (changedItem.IsSelected)
            {
                SelectedTransaction = Transactions.FirstOrDefault(t => t.InvoiceNumber == invoiceNumber);
            }
            else
            {
                SelectedTransaction = null;
            }
        }

        public void SetFrame(Frame frame)
        {
            _frame = frame;
        }

        [RelayCommand]
        private void Logout()
        {
            if (_frame != null)
            {
                _frame.Navigate(typeof(LoginPage));
            }
        }

        public void AddOrUpdateTransaction(Transaction transaction)
        {
            var existing = Transactions.FirstOrDefault(t => t.InvoiceNumber == transaction.InvoiceNumber);
            
            if (existing != null)
            {
                var index = Transactions.IndexOf(existing);
                Transactions[index] = transaction;
            }
            else
            {
                Transactions.Add(transaction);
            }
            
            RefreshAllItems();
        }

        [RelayCommand]
        private void AddNewTransaction()
        {
            if (_frame != null)
            {
                _frame.Navigate(typeof(AddTransactionPage), null);
            }
        }

        [RelayCommand]
        private void EditTransaction()
        {
            if (_frame != null && SelectedTransaction != null)
            {
                _frame.Navigate(typeof(AddTransactionPage), SelectedTransaction);
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (SelectedTransaction != null)
            {
                await _databaseHelper.DeleteTransactionAsync(SelectedTransaction.InvoiceNumber);
                Transactions.Remove(SelectedTransaction);
            }
        }

        [RelayCommand]
        private async Task OpenAttachmentAsync(Attachment attachment)
        {
            if (attachment == null || string.IsNullOrEmpty(attachment.FilePath))
                return;

            try
            {
                if (System.IO.File.Exists(attachment.FilePath))
                {
                    var file = await StorageFile.GetFileFromPathAsync(attachment.FilePath).AsTask();
                    await Windows.System.Launcher.LaunchFileAsync(file).AsTask();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"File not found: {attachment.FilePath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening attachment: {ex.Message}");
            }
        }
    }
}
