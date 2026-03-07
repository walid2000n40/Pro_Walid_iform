using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Models;
using ProWalid.Views;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProWalid.ViewModels
{
    public partial class TransactionViewModel : ObservableObject
    {
        private static TransactionViewModel _instance;
        private Frame _frame;

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
            
            Transactions.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TransactionCount));
                OnPropertyChanged(nameof(TotalAmount));
                RefreshAllItems();
            };
        }

        public static TransactionViewModel Instance => _instance;

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
        private void DeleteSelected()
        {
            if (SelectedTransaction != null)
            {
                Transactions.Remove(SelectedTransaction);
            }
        }
    }
}
