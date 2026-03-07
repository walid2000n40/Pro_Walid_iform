using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Models;
using ProWalid.Views;
using ProWalid.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

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
                foreach (var item in transaction.Items)
                {
                    AllItems.Add(new TransactionItemWithDetails
                    {
                        ServiceName = item.ServiceName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Profit = item.Profit,
                        Discount = item.Discount,
                        CompanyName = transaction.CompanyName,
                        EmployeeName = transaction.EmployeeName,
                        AttachmentPath = item.AttachmentPath
                    });
                }
            }
        }

        public void SetFrame(Frame frame)
        {
            _frame = frame;
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
        private async Task OpenAttachmentAsync(TransactionItemWithDetails item)
        {
            if (!string.IsNullOrEmpty(item.AttachmentPath))
            {
                await _attachmentManager.OpenAttachmentAsync(item.AttachmentPath);
            }
        }
    }
}
