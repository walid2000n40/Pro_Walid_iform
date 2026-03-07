using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProWalid.ViewModels
{
    public partial class AddTransactionPageViewModel : ObservableObject
    {
        private static int _lastInvoiceNumber = 811;
        private Frame _frame;
        private Transaction _originalTransaction;
        private bool _isEditMode;

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
            _lastInvoiceNumber++;
            InvoiceNumber = _lastInvoiceNumber.ToString();

            Items.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(GrandTotal));
            };

            AddItemCommand.Execute(null);
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
        private void Save()
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
    }
}
