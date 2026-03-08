using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using ProWalid.Models;
using ProWalid.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ProWalid.Views
{
    public sealed partial class InvoicePreviewPage : Page
    {
        public InvoicePreviewViewModel ViewModel { get; }

        public InvoicePreviewPage()
        {
            this.InitializeComponent();
            ViewModel = new InvoicePreviewViewModel();
            DataContext = ViewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.SetFrame(Frame);
            await ViewModel.LoadAsync(e.Parameter as InvoiceSummaryRow);
            await RenderPrintPreviewAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InvoicePreviewViewModel.PrintHtml))
            {
                await RenderPrintPreviewAsync();
            }
        }

        private async Task EnsurePrintWebViewAsync()
        {
            if (A4PrintWebView.CoreWebView2 != null)
            {
                return;
            }

            await A4PrintWebView.EnsureCoreWebView2Async();
            A4PrintWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            A4PrintWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            A4PrintWebView.CoreWebView2.Settings.IsZoomControlEnabled = true;
        }

        private async Task RenderPrintPreviewAsync()
        {
            if (A4PrintWebView == null)
            {
                return;
            }

            await EnsurePrintWebViewAsync();

            if (!string.IsNullOrWhiteSpace(ViewModel.PrintHtml))
            {
                A4PrintWebView.NavigateToString(ViewModel.PrintHtml);
            }
        }

        private async void RefreshPrintPreviewButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await RenderPrintPreviewAsync();
        }

        private async void PrintInvoiceButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await EnsurePrintWebViewAsync();
            await A4PrintWebView.ExecuteScriptAsync("window.print();");
        }

        private async void SavePdfButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await EnsurePrintWebViewAsync();

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("PDF Document", new[] { ".pdf" });
            picker.SuggestedFileName = string.IsNullOrWhiteSpace(ViewModel.InvoiceNumber)
                ? "Invoice-A4"
                : $"Invoice-{ViewModel.InvoiceNumber}";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile? file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                return;
            }

            var printSettings = A4PrintWebView.CoreWebView2.Environment.CreatePrintSettings();
            printSettings.ShouldPrintBackgrounds = true;
            printSettings.ShouldPrintHeaderAndFooter = false;
            printSettings.ShouldPrintSelectionOnly = false;
            printSettings.ShouldPrintBackgrounds = true;

            var success = await A4PrintWebView.CoreWebView2.PrintToPdfAsync(file.Path, printSettings);

            var dialog = new ContentDialog
            {
                Title = success ? "تم حفظ PDF" : "تعذر حفظ PDF",
                Content = success ? $"تم حفظ ملف PDF بنجاح:\n{file.Path}" : "حدث خطأ أثناء إنشاء ملف PDF.",
                CloseButtonText = "إغلاق",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
