using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ProWalid.Models;
using ProWalid.Views;
using ProWalid.Data;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        private readonly List<Transaction> _allTransactions = new();
        private bool _isApplyingTransactionFilter;

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

        public string SelectedCustomerHeaderText => SelectedCustomer == null ? "لم يتم اختيار عميل بعد" : $"العميل المحدد: {SelectedCustomer.Name}";

        public bool HasSelectedCustomer => SelectedCustomer != null;

        public int TransactionCount => Transactions.Count;

        public double TotalAmount => Transactions.Sum(t => t.GrandTotal);

        public TransactionViewModel()
        {
            _instance = this;
            _databaseHelper = new DatabaseHelper();
            _attachmentManager = new AttachmentManager();
            
            Transactions.CollectionChanged += (s, e) =>
            {
                if (_isApplyingTransactionFilter)
                {
                    return;
                }

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

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            OnPropertyChanged(nameof(SelectedCustomerHeaderText));
            OnPropertyChanged(nameof(HasSelectedCustomer));
            ApplyCustomerFilter();
        }

        [RelayCommand]
        private void ToggleCustomerPanel()
        {
            IsCustomerPanelVisible = !IsCustomerPanelVisible;
        }

        [RelayCommand]
        private async Task AddCustomerAsync()
        {
            var nameBox = new TextBox { PlaceholderText = "اسم العميل" };
            var phoneBox = new TextBox { PlaceholderText = "رقم الهاتف" };
            var emailBox = new TextBox { PlaceholderText = "البريد الإلكتروني (اختياري)" };
            var addressBox = new TextBox { PlaceholderText = "العنوان (اختياري)" };

            var dialog = new ContentDialog
            {
                Title = "إضافة عميل جديد",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = "اسم العميل:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        nameBox,
                        new TextBlock { Text = "رقم الهاتف:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        phoneBox,
                        new TextBlock { Text = "البريد الإلكتروني:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        emailBox,
                        new TextBlock { Text = "العنوان:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        addressBox
                    }
                },
                PrimaryButtonText = "حفظ",
                CloseButtonText = "إلغاء",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _frame?.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "خطأ",
                        Content = "يجب إدخال اسم العميل!",
                        CloseButtonText = "حسناً",
                        XamlRoot = _frame?.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }

                var newCustomer = new Customer
                {
                    Name = nameBox.Text,
                    Phone = phoneBox.Text,
                    Email = emailBox.Text,
                    Address = addressBox.Text
                };

                var customerId = await _databaseHelper.SaveCustomerAsync(newCustomer);
                newCustomer.Id = customerId;
                Customers.Add(newCustomer);
                SelectedCustomer = newCustomer;
            }
        }

        [RelayCommand]
        private async Task EditCustomerAsync()
        {
            if (SelectedCustomer == null)
            {
                var noSelectionDialog = new ContentDialog
                {
                    Title = "تنبيه",
                    Content = "يرجى اختيار عميل للتعديل!",
                    CloseButtonText = "حسناً",
                    XamlRoot = _frame?.XamlRoot
                };
                await noSelectionDialog.ShowAsync();
                return;
            }

            var nameBox = new TextBox { Text = SelectedCustomer.Name };
            var phoneBox = new TextBox { Text = SelectedCustomer.Phone };
            var emailBox = new TextBox { Text = SelectedCustomer.Email };
            var addressBox = new TextBox { Text = SelectedCustomer.Address };
            var idText = new TextBlock 
            { 
                Text = $"رقم العميل: {SelectedCustomer.Id}",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkBlue)
            };

            var dialog = new ContentDialog
            {
                Title = "تعديل بيانات العميل",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        idText,
                        new TextBlock { Text = "اسم العميل:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        nameBox,
                        new TextBlock { Text = "رقم الهاتف:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        phoneBox,
                        new TextBlock { Text = "البريد الإلكتروني:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        emailBox,
                        new TextBlock { Text = "العنوان:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                        addressBox
                    }
                },
                PrimaryButtonText = "حفظ التعديلات",
                CloseButtonText = "إلغاء",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _frame?.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "خطأ",
                        Content = "يجب إدخال اسم العميل!",
                        CloseButtonText = "حسناً",
                        XamlRoot = _frame?.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }

                SelectedCustomer.Name = nameBox.Text;
                SelectedCustomer.Phone = phoneBox.Text;
                SelectedCustomer.Email = emailBox.Text;
                SelectedCustomer.Address = addressBox.Text;

                await _databaseHelper.SaveCustomerAsync(SelectedCustomer);
                ApplyCustomerFilter();
                
                var successDialog = new ContentDialog
                {
                    Title = "نجاح",
                    Content = "تم تحديث بيانات العميل بنجاح!",
                    CloseButtonText = "حسناً",
                    XamlRoot = _frame?.XamlRoot
                };
                await successDialog.ShowAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteCustomerAsync()
        {
            if (SelectedCustomer == null)
            {
                var noSelectionDialog = new ContentDialog
                {
                    Title = "تنبيه",
                    Content = "يرجى اختيار عميل للحذف!",
                    CloseButtonText = "حسناً",
                    XamlRoot = _frame?.XamlRoot
                };
                await noSelectionDialog.ShowAsync();
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "تأكيد حذف العميل",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = $"هل أنت متأكد من حذف العميل: {SelectedCustomer.Name}؟",
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                        },
                        new TextBlock 
                        { 
                            Text = $"رقم العميل: {SelectedCustomer.Id}",
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
                        },
                        new TextBlock 
                        { 
                            Text = "تحذير: لا يمكن التراجع عن هذا الإجراء!",
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
                        }
                    }
                },
                PrimaryButtonText = "حذف",
                CloseButtonText = "إلغاء",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _frame?.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await _databaseHelper.DeleteCustomerAsync(SelectedCustomer.Id);
                Customers.Remove(SelectedCustomer);
                SelectedCustomer = null;
            }
        }

        public static TransactionViewModel Instance => _instance;

        private async void LoadTransactionsAsync()
        {
            var loadedTransactions = await _databaseHelper.GetAllTransactionsAsync();
            _allTransactions.Clear();
            _allTransactions.AddRange(loadedTransactions);
            ApplyCustomerFilter();
        }

        private void ApplyCustomerFilter()
        {
            _isApplyingTransactionFilter = true;

            Transactions.Clear();
            AllItems.Clear();
            SelectedItem = null;
            SelectedTransaction = null;

            if (SelectedCustomer != null)
            {
                foreach (var transaction in _allTransactions.Where(t => CustomerMatchesTransaction(SelectedCustomer, t)))
                {
                    Transactions.Add(transaction);
                }
            }

            _isApplyingTransactionFilter = false;
            OnPropertyChanged(nameof(TransactionCount));
            OnPropertyChanged(nameof(TotalAmount));
            RefreshAllItems();
        }

        private static bool CustomerMatchesTransaction(Customer customer, Transaction transaction)
        {
            return transaction.CustomerId > 0 && transaction.CustomerId == customer.Id;
        }

        private void RefreshAllItems()
        {
            AllItems.Clear();
            foreach (var transaction in Transactions)
            {
                bool isFirst = true;
                int itemCount = transaction.Items.Count;
                int currentIndex = 0;
                
                foreach (var item in transaction.Items)
                {
                    currentIndex++;
                    bool isLast = (currentIndex == itemCount);
                    
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
                        IsLastItemInTransaction = isLast,
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
            var existing = _allTransactions.FirstOrDefault(t => t.InvoiceNumber == transaction.InvoiceNumber);
            
            if (existing != null)
            {
                var index = _allTransactions.IndexOf(existing);
                _allTransactions[index] = transaction;
            }
            else
            {
                _allTransactions.Add(transaction);
            }
            
            ApplyCustomerFilter();
        }

        [RelayCommand]
        private void AddNewTransaction()
        {
            if (_frame != null)
            {
                _frame.Navigate(typeof(AddTransactionPage), SelectedCustomer);
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
            if (SelectedTransaction == null)
                return;

            var dialog = new ContentDialog
            {
                Title = "تأكيد الحذف",
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock 
                        { 
                            Text = $"هل أنت متأكد من حذف المعاملة رقم {SelectedTransaction.InvoiceNumber}؟",
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                        },
                        new TextBlock 
                        { 
                            Text = "أدخل كود الحذف للتأكيد:",
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Margin = new Microsoft.UI.Xaml.Thickness(0, 8, 0, 0)
                        },
                        new PasswordBox 
                        { 
                            Name = "DeleteCodeBox",
                            PlaceholderText = "1234"
                        }
                    }
                },
                PrimaryButtonText = "حذف",
                CloseButtonText = "إلغاء",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _frame?.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var passwordBox = (dialog.Content as StackPanel)?.Children
                    .OfType<PasswordBox>()
                    .FirstOrDefault();

                if (passwordBox?.Password == "1234")
                {
                    await _databaseHelper.DeleteTransactionAsync(SelectedTransaction.InvoiceNumber);
                    var cachedTransaction = _allTransactions.FirstOrDefault(t => t.InvoiceNumber == SelectedTransaction.InvoiceNumber);
                    if (cachedTransaction != null)
                    {
                        _allTransactions.Remove(cachedTransaction);
                    }

                    SelectedTransaction = null;
                    ApplyCustomerFilter();
                }
                else
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "خطأ",
                        Content = "كود الحذف غير صحيح!",
                        CloseButtonText = "حسناً",
                        XamlRoot = _frame?.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
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

        [RelayCommand]
        private async Task ShowStatusAsync()
        {
            if (SelectedTransaction == null)
            {
                await ShowMessageAsync("الحالة", "يرجى تحديد معاملة أولاً.");
                return;
            }

            var selectedItems = GetSelectedTransactionItems();
            var attachmentsCount = selectedItems.Sum(item => item.Attachments.Count);
            var message = new StringBuilder();
            message.AppendLine($"رقم الفاتورة: {SelectedTransaction.InvoiceNumber}");
            message.AppendLine("الحالة: مكتملة");
            message.AppendLine($"عدد البنود: {SelectedTransaction.Items.Count}");
            message.AppendLine($"عدد المرفقات: {attachmentsCount}");
            message.AppendLine($"الإجمالي: {SelectedTransaction.GrandTotal} درهم إماراتي");

            await ShowMessageAsync("حالة المعاملة", message.ToString());
        }

        [RelayCommand]
        private async Task ShowInvoicesAsync()
        {
            if (Transactions.Count == 0)
            {
                await ShowMessageAsync("الفواتير", "لا توجد فواتير متاحة حالياً.");
                return;
            }

            var invoices = string.Join(Environment.NewLine, Transactions.Select(t => $"- {t.InvoiceNumber} | {t.CompanyName}"));
            await ShowMessageAsync("الفواتير", invoices);
        }

        [RelayCommand]
        private async Task ShowReportsAsync()
        {
            var report = new StringBuilder();
            report.AppendLine($"عدد المعاملات: {Transactions.Count}");
            report.AppendLine($"إجمالي المبلغ: {TotalAmount} درهم إماراتي");
            report.AppendLine($"عدد العملاء: {Customers.Count}");
            report.AppendLine($"عدد البنود: {AllItems.Count}");

            await ShowMessageAsync("التقارير", report.ToString());
        }

        [RelayCommand]
        private async Task DownloadAttachmentsAsync()
        {
            if (SelectedTransaction == null)
            {
                await ShowMessageAsync("تحميل المرفقات", "يرجى تحديد معاملة أولاً.");
                return;
            }

            var attachments = GetSelectedAttachments();
            if (attachments.Count == 0)
            {
                await ShowMessageAsync("تحميل المرفقات", "لا توجد مرفقات للمعاملة المحددة.");
                return;
            }

            var invoiceNumber = string.IsNullOrWhiteSpace(SelectedTransaction.InvoiceNumber)
                ? "TransactionAttachments"
                : $"Attachments_{SelectedTransaction.InvoiceNumber}";

            var zipPath = await _attachmentManager.CreateZipForAttachmentsAsync(
                attachments,
                invoiceNumber,
                invoiceNumber);

            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                await ShowMessageAsync("تحميل المرفقات", "تعذر إنشاء الملف المضغوط للمرفقات.");
                return;
            }

            await _attachmentManager.OpenAttachmentAsync(zipPath);
            await ShowMessageAsync("تحميل المرفقات", $"تم إنشاء ملف مضغوط للمرفقات بنجاح:\n{zipPath}");
        }

        [RelayCommand]
        private async Task DeleteAttachmentsAsync()
        {
            if (SelectedTransaction == null)
            {
                await ShowMessageAsync("حذف المرفقات", "يرجى تحديد معاملة أولاً.");
                return;
            }

            var selectedItems = GetSelectedTransactionItems();
            var attachments = selectedItems.SelectMany(item => item.Attachments).ToList();
            if (attachments.Count == 0)
            {
                await ShowMessageAsync("حذف المرفقات", "لا توجد مرفقات لحذفها في المعاملة المحددة.");
                return;
            }

            var itemIds = SelectedTransaction.Items
                .Select(item => item.Id)
                .Where(id => id > 0)
                .ToList();

            foreach (var attachment in attachments)
            {
                _attachmentManager.DeleteAttachment(attachment.FilePath);
            }

            await _databaseHelper.DeleteAttachmentsByTransactionItemIdsAsync(itemIds);

            foreach (var item in selectedItems)
            {
                item.Attachments.Clear();
                item.AttachmentPath = string.Empty;
            }

            foreach (var item in SelectedTransaction.Items)
            {
                item.Attachments.Clear();
                item.AttachmentPath = string.Empty;
            }

            await _databaseHelper.SaveTransactionAsync(SelectedTransaction);
            RefreshAllItems();
            await ShowMessageAsync("حذف المرفقات", "تم حذف مرفقات المعاملة المحددة بنجاح.");
        }

        private List<TransactionItemWithDetails> GetSelectedTransactionItems()
        {
            if (SelectedTransaction == null)
            {
                return new List<TransactionItemWithDetails>();
            }

            return AllItems.Where(i => i.InvoiceNumber == SelectedTransaction.InvoiceNumber).ToList();
        }

        private List<Attachment> GetSelectedAttachments()
        {
            return GetSelectedTransactionItems()
                .SelectMany(item => item.Attachments)
                .Where(attachment => attachment != null && !string.IsNullOrWhiteSpace(attachment.FilePath) && File.Exists(attachment.FilePath))
                .ToList();
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                    MaxWidth = 520
                },
                CloseButtonText = "حسناً",
                XamlRoot = _frame?.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
