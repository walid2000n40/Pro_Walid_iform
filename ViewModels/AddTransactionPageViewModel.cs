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

        [ObservableProperty]
        private long selectedCustomerId;

        [ObservableProperty]
        private string selectedCustomerName = string.Empty;

        public string SelectedCustomerContextText => string.IsNullOrWhiteSpace(SelectedCustomerName)
            ? "العميل: غير محدد"
            : $"العميل المحدد: {SelectedCustomerName}";

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

        public void LoadCustomer(Customer customer)
        {
            if (customer == null)
            {
                return;
            }

            SelectedCustomerId = customer.Id;
            SelectedCustomerName = customer.Name;
        }

        public async void LoadTransaction(Transaction transaction)
        {
            if (transaction != null)
            {
                _isEditMode = true;
                _originalTransaction = transaction;
                PageTitle = "تعديل معاملة";
                SelectedCustomerId = transaction.CustomerId;
                if (TransactionViewModel.Instance?.SelectedCustomer?.Id == transaction.CustomerId)
                {
                    SelectedCustomerName = TransactionViewModel.Instance.SelectedCustomer.Name;
                }

                InvoiceNumber = transaction.InvoiceNumber;
                CompanyName = transaction.CompanyName;
                EmployeeName = transaction.EmployeeName;
                TransactionDate = transaction.TransactionDate;

                Items.Clear();
                foreach (var item in transaction.Items)
                {
                    var newItem = new TransactionItemDetail
                    {
                        Id = item.Id,
                        ServiceName = item.ServiceName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Profit = item.Profit,
                        Discount = item.Discount,
                        AttachmentPath = item.AttachmentPath
                    };

                    var existingAttachments = await _databaseHelper.GetAttachmentsAsync(item.Id);
                    foreach (var attachment in existingAttachments)
                    {
                        newItem.Attachments.Add(attachment);
                    }

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
            if (SelectedCustomerId <= 0)
            {
                System.Diagnostics.Debug.WriteLine("يجب اختيار عميل قبل حفظ المعاملة");
                return;
            }

            if (string.IsNullOrWhiteSpace(InvoiceNumber))
            {
                System.Diagnostics.Debug.WriteLine("رقم الفاتورة مطلوب");
                return;
            }

            if (string.IsNullOrWhiteSpace(CompanyName))
            {
                System.Diagnostics.Debug.WriteLine("اسم الشركة مطلوب");
                return;
            }

            if (string.IsNullOrWhiteSpace(EmployeeName))
            {
                System.Diagnostics.Debug.WriteLine("اسم الموظف مطلوب");
                return;
            }

            if (Items.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("يجب إضافة بند واحد على الأقل");
                return;
            }

            var transaction = new Transaction
            {
                CustomerId = SelectedCustomerId,
                InvoiceNumber = InvoiceNumber,
                CompanyName = CompanyName,
                EmployeeName = EmployeeName,
                TransactionDate = TransactionDate
            };

            foreach (var item in Items)
            {
                if (string.IsNullOrWhiteSpace(item.ServiceName))
                {
                    System.Diagnostics.Debug.WriteLine("اسم الخدمة مطلوب لكل بند");
                    return;
                }

                var newItem = new TransactionItemDetail
                {
                    Id = item.Id,
                    ServiceName = item.ServiceName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Profit = item.Profit,
                    Discount = item.Discount,
                    AttachmentPath = item.AttachmentPath
                };

                foreach (var existingAttachment in item.Attachments.Where(a => a.Id > 0))
                {
                    newItem.Attachments.Add(existingAttachment);
                }

                var newAttachments = item.Attachments.Where(a => a.Id == 0).ToList();
                if (newAttachments.Count > 0)
                {
                    var filePaths = newAttachments.Select(a => a.FilePath).ToList();
                    var savedAttachments = await _attachmentManager.SaveMultipleAttachmentsAsync(filePaths, InvoiceNumber);
                    
                    foreach (var attachment in savedAttachments)
                    {
                        newItem.Attachments.Add(attachment);
                    }
                }

                transaction.Items.Add(newItem);
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
        private async Task PickFilesAsync(TransactionItemDetail item)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add("*");
                
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Count > 0)
                {
                    var filePaths = files.Select(f => f.Path).ToList();
                    
                    foreach (var file in files)
                    {
                        var fileInfo = new System.IO.FileInfo(file.Path);
                        
                        var tempAttachment = new Attachment
                        {
                            OriginalFileName = file.Name,
                            FilePath = file.Path,
                            FileSize = (long)fileInfo.Length,
                            FileExtension = fileInfo.Extension,
                            FileName = file.Name
                        };
                        
                        item.Attachments.Add(tempAttachment);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error picking files: {ex.Message}");
            }
        }

        [RelayCommand]
        private void RemoveAttachment(object parameter)
        {
            if (parameter is Tuple<TransactionItemDetail, Attachment> tuple)
            {
                var item = tuple.Item1;
                var attachment = tuple.Item2;
                
                item.Attachments.Remove(attachment);
            }
        }

        [RelayCommand]
        private async Task PreviewAttachmentAsync(Attachment attachment)
        {
            if (attachment == null || string.IsNullOrEmpty(attachment.FilePath))
                return;

            try
            {
                if (System.IO.File.Exists(attachment.FilePath))
                {
                    var file = await StorageFile.GetFileFromPathAsync(attachment.FilePath);
                    await Windows.System.Launcher.LaunchFileAsync(file);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error previewing attachment: {ex.Message}");
            }
        }
    }
}
