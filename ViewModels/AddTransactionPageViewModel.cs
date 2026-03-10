using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Models;
using ProWalid.Data;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;

namespace ProWalid.ViewModels
{
    public partial class AddTransactionPageViewModel : ObservableObject
    {
        private const string UniversalInvoiceTemplateKey = InvoicePreviewViewModel.UniversalServerTemplateKey;
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

        [ObservableProperty]
        private long selectedCustomerNumber;

        [ObservableProperty]
        private ObservableCollection<SuggestionEntry> companySuggestions = new();

        [ObservableProperty]
        private ObservableCollection<SuggestionEntry> employeeSuggestions = new();

        public string SelectedCustomerContextText => string.IsNullOrWhiteSpace(SelectedCustomerName)
            ? "العميل: غير محدد"
            : $"العميل المحدد: {SelectedCustomerName} - ID {SelectedCustomerNumber}";

        public double GrandTotal => Items.Sum(item => item.Total);

        public double TotalProfit => Items.Sum(item => item.Profit);

        public AddTransactionPageViewModel()
        {
            _databaseHelper = new DatabaseHelper();
            _attachmentManager = new AttachmentManager();

            Items.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(TotalProfit));
            };

            AddItemCommand.Execute(null);
        }

        public void SetFrame(Frame frame)
        {
            _frame = frame;
            _ = EnsureProvisionalInvoiceNumberAsync();
        }

        public void LoadCustomer(Customer customer)
        {
            if (customer == null)
            {
                return;
            }

            SelectedCustomerId = customer.Id;
            SelectedCustomerName = customer.Name;
            SelectedCustomerNumber = customer.CustomerNumber > 0 ? customer.CustomerNumber : customer.Id;
            _ = EnsureProvisionalInvoiceNumberAsync();
        }

        public async void LoadTransaction(Transaction transaction)
        {
            if (transaction != null)
            {
                _isEditMode = true;
                _originalTransaction = transaction;
                PageTitle = "تعديل معاملة";
                SelectedCustomerId = transaction.CustomerId;
                var matchedCustomer = TransactionViewModel.Instance?.Customers?.FirstOrDefault(customer => customer.Id == transaction.CustomerId);
                if (matchedCustomer != null)
                {
                    SelectedCustomerName = matchedCustomer.Name;
                    SelectedCustomerNumber = matchedCustomer.CustomerNumber > 0 ? matchedCustomer.CustomerNumber : matchedCustomer.Id;
                }

                InvoiceNumber = transaction.InvoiceNumber;
                _originalTransaction.InvoiceTemplateKey = transaction.InvoiceTemplateKey;
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
                        GovFees = item.GovFees,
                        AttachmentPath = item.AttachmentPath
                    };

                    var existingAttachments = await _databaseHelper.GetAttachmentsAsync(item.Id);
                    foreach (var attachment in existingAttachments)
                    {
                        newItem.Attachments.Add(attachment);
                    }

                    RegisterItem(newItem);
                    Items.Add(newItem);
                }
            }
        }

        [RelayCommand]
        private void AddItem()
        {
            var newItem = new TransactionItemDetail();
            RegisterItem(newItem);
            Items.Add(newItem);
        }

        [RelayCommand]
        private void RemoveItem(TransactionItemDetail item)
        {
            if (item == null)
            {
                return;
            }

            item.PropertyChanged -= TransactionItem_PropertyChanged;
            Items.Remove(item);

            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(TotalProfit));
        }

        private void RegisterItem(TransactionItemDetail item)
        {
            item.PropertyChanged -= TransactionItem_PropertyChanged;
            item.PropertyChanged += TransactionItem_PropertyChanged;
        }

        private static string CleanInput(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(" ", value
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();
        }

        private static void ReplaceSuggestions(ObservableCollection<SuggestionEntry> target, IEnumerable<SuggestionEntry> values)
        {
            target.Clear();
            foreach (var value in values.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Value)).GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase).Select(group => group.First()))
            {
                target.Add(value);
            }
        }

        public async Task UpdateCompanySuggestionsAsync(string? searchText)
        {
            var suggestions = await _databaseHelper.GetCompanySuggestionsAsync(searchText);
            ReplaceSuggestions(CompanySuggestions, suggestions);
        }

        public async Task UpdateEmployeeSuggestionsAsync(string? searchText)
        {
            var suggestions = await _databaseHelper.GetEmployeeSuggestionsAsync(searchText);
            ReplaceSuggestions(EmployeeSuggestions, suggestions);
        }

        public async Task UpdateItemSuggestionsAsync(TransactionItemDetail? item, string? searchText)
        {
            if (item == null)
            {
                return;
            }

            var suggestions = await _databaseHelper.GetItemSuggestionsAsync(searchText);
            ReplaceSuggestions(item.ItemSuggestions, suggestions);
        }

        public async Task DeleteSuggestionAsync(SuggestionEntry? suggestion)
        {
            if (suggestion == null || string.IsNullOrWhiteSpace(suggestion.Value))
            {
                return;
            }

            if (string.Equals(suggestion.SuggestionType, "company", StringComparison.OrdinalIgnoreCase))
            {
                await _databaseHelper.DeleteCompanySuggestionAsync(suggestion.Value);
                var existingEntry = CompanySuggestions.FirstOrDefault(item => string.Equals(item.Value, suggestion.Value, StringComparison.OrdinalIgnoreCase));
                if (existingEntry != null)
                {
                    CompanySuggestions.Remove(existingEntry);
                }
                return;
            }

            if (string.Equals(suggestion.SuggestionType, "employee", StringComparison.OrdinalIgnoreCase))
            {
                await _databaseHelper.DeleteEmployeeSuggestionAsync(suggestion.Value);
                var existingEntry = EmployeeSuggestions.FirstOrDefault(item => string.Equals(item.Value, suggestion.Value, StringComparison.OrdinalIgnoreCase));
                if (existingEntry != null)
                {
                    EmployeeSuggestions.Remove(existingEntry);
                }
                return;
            }

            if (string.Equals(suggestion.SuggestionType, "item", StringComparison.OrdinalIgnoreCase))
            {
                await _databaseHelper.DeleteItemSuggestionAsync(suggestion.Value);
                foreach (var transactionItem in Items)
                {
                    var existingEntry = transactionItem.ItemSuggestions.FirstOrDefault(item => string.Equals(item.Value, suggestion.Value, StringComparison.OrdinalIgnoreCase));
                    if (existingEntry != null)
                    {
                        transactionItem.ItemSuggestions.Remove(existingEntry);
                    }
                }
            }
        }

        private void TransactionItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TransactionItemDetail.Total)
                || e.PropertyName == nameof(TransactionItemDetail.Quantity)
                || e.PropertyName == nameof(TransactionItemDetail.UnitPrice))
            {
                OnPropertyChanged(nameof(GrandTotal));
            }

            if (e.PropertyName == nameof(TransactionItemDetail.Profit))
            {
                OnPropertyChanged(nameof(TotalProfit));
            }
        }

        private async Task EnsureProvisionalInvoiceNumberAsync(bool forceRefresh = false)
        {
            if (_isEditMode)
            {
                return;
            }

            if (!forceRefresh && !string.IsNullOrWhiteSpace(InvoiceNumber))
            {
                return;
            }

            InvoiceNumber = await _databaseHelper.GetNextInvoiceNumberAsync();
        }

        private bool ShouldUseHazemTemplate()
        {
            return !string.IsNullOrWhiteSpace(SelectedCustomerName)
                && (SelectedCustomerName.Contains("حازم", StringComparison.OrdinalIgnoreCase)
                    || SelectedCustomerName.Contains("hazem", StringComparison.OrdinalIgnoreCase));
        }

        private string ResolveInvoiceTemplateKey()
        {
            if (_isEditMode && !string.IsNullOrWhiteSpace(_originalTransaction?.InvoiceTemplateKey))
            {
                return _originalTransaction.InvoiceTemplateKey;
            }

            return UniversalInvoiceTemplateKey;
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            if (_frame?.XamlRoot == null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "إغلاق",
                XamlRoot = _frame.XamlRoot
            };

            await dialog.ShowAsync();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (SelectedCustomerId <= 0)
            {
                await ShowMessageAsync("تعذر الحفظ", "يجب اختيار عميل قبل حفظ المعاملة.");
                return;
            }

            if (string.IsNullOrWhiteSpace(CompanyName))
            {
                await ShowMessageAsync("تعذر الحفظ", "اسم الشركة مطلوب.");
                return;
            }

            if (string.IsNullOrWhiteSpace(EmployeeName))
            {
                await ShowMessageAsync("تعذر الحفظ", "اسم الموظف مطلوب.");
                return;
            }

            if (Items.Count == 0)
            {
                await ShowMessageAsync("تعذر الحفظ", "يجب إضافة بند واحد على الأقل.");
                return;
            }

            if (!_isEditMode)
            {
                await EnsureProvisionalInvoiceNumberAsync();
            }

            CompanyName = CleanInput(CompanyName);
            EmployeeName = CleanInput(EmployeeName);

            var transaction = new Transaction
            {
                CustomerId = SelectedCustomerId,
                TransactionStatus = _originalTransaction?.TransactionStatus ?? "معلق",
                InvoiceNumber = InvoiceNumber,
                InvoiceTemplateKey = ResolveInvoiceTemplateKey(),
                CompanyName = CompanyName,
                EmployeeName = EmployeeName,
                TransactionDate = TransactionDate
            };

            foreach (var item in Items)
            {
                item.ServiceName = CleanInput(item.ServiceName);

                if (string.IsNullOrWhiteSpace(item.ServiceName))
                {
                    await ShowMessageAsync("تعذر الحفظ", "اسم الخدمة مطلوب لكل بند.");
                    return;
                }

                var newItem = new TransactionItemDetail
                {
                    Id = item.Id,
                    ServiceName = item.ServiceName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Profit = item.Profit,
                    GovFees = item.GovFees,
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

            try
            {
                await _databaseHelper.SaveTransactionAsync(transaction);
                TransactionViewModel.Instance?.AddOrUpdateTransaction(transaction);

                if (_frame != null && _frame.CanGoBack)
                {
                    _frame.GoBack();
                }
            }
            catch (Exception ex) when (!_isEditMode && ex.Message.Contains("InvoiceNumber", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureProvisionalInvoiceNumberAsync(forceRefresh: true);
                transaction.InvoiceNumber = InvoiceNumber;

                await _databaseHelper.SaveTransactionAsync(transaction);
                TransactionViewModel.Instance?.AddOrUpdateTransaction(transaction);

                if (_frame != null && _frame.CanGoBack)
                {
                    _frame.GoBack();
                }
            }
            catch (Exception ex)
            {
                if (!_isEditMode)
                {
                    await EnsureProvisionalInvoiceNumberAsync(forceRefresh: true);
                }

                await ShowMessageAsync("فشل حفظ المعاملة", ex.Message);
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
