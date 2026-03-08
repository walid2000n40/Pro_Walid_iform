using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using ProWalid.Models;
using ProWalid.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace ProWalid.Views
{
    public sealed partial class InvoicePreviewPage : Page
    {
        private const double A4PreviewWidth = 794d;
        private const double A4PreviewHeight = 1123d;
        private const uint PdfRenderPixelWidth = 2480;
        private const uint PdfRenderPixelHeight = 3508;
        private const double PdfCaptureZoomFactor = 2480d / A4PreviewWidth;

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
            await ViewModel.LoadAsync(e.Parameter);
            ApplyViewModelTemplateToPivot();
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
            await EnsureWebViewAsync(A4PrintWebView, true);
        }

        private static async Task EnsureWebViewAsync(WebView2 webView, bool allowContextMenus)
        {
            if (webView.CoreWebView2 != null)
            {
                return;
            }

            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = allowContextMenus;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
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

        private async void PreviewTemplatesPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PreviewTemplatesPivot == null)
            {
                return;
            }

            if (PreviewTemplatesPivot.SelectedItem is PivotItem selectedItem
                && string.Equals(selectedItem.Tag?.ToString(), "A4", StringComparison.OrdinalIgnoreCase))
            {
                await RenderPrintPreviewAsync();
                return;
            }

            SyncSelectedTemplateFromPivot();
        }

        private void SyncSelectedTemplateFromPivot()
        {
            if (PreviewTemplatesPivot?.SelectedItem is not PivotItem selectedItem)
            {
                return;
            }

            var templateKey = selectedItem.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(templateKey)
                || string.Equals(templateKey, "A4", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ViewModel.SelectPreviewTemplate(templateKey);
        }

        private void ApplyViewModelTemplateToPivot()
        {
            if (PreviewTemplatesPivot == null)
            {
                return;
            }

            var templateKey = ViewModel.SelectedPreviewTemplateKey;
            foreach (var item in PreviewTemplatesPivot.Items)
            {
                if (item is PivotItem pivotItem
                    && string.Equals(pivotItem.Tag?.ToString(), templateKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (!ReferenceEquals(PreviewTemplatesPivot.SelectedItem, pivotItem))
                    {
                        PreviewTemplatesPivot.SelectedItem = pivotItem;
                    }

                    return;
                }
            }
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

            var success = await TrySaveRenderedPdfAsync(file);
            if (!success)
            {
                success = await SaveWebViewPdfFallbackAsync(file);
            }

            var dialog = new ContentDialog
            {
                Title = success ? "تم حفظ PDF" : "تعذر حفظ PDF",
                Content = success ? $"تم حفظ ملف PDF بنجاح:\n{file.Path}" : "حدث خطأ أثناء إنشاء ملف PDF.",
                CloseButtonText = "إغلاق",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async Task<bool> TrySaveRenderedPdfAsync(StorageFile file)
        {
            try
            {
                var previewBytes = await CaptureInvoicePreviewPngAsync();
                if (previewBytes.Length == 0)
                {
                    return false;
                }

                var pdfBytes = await BuildRenderedPdfAsync(previewBytes);
                if (pdfBytes.Length == 0)
                {
                    return false;
                }

                await FileIO.WriteBytesAsync(file, pdfBytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<byte[]> CaptureInvoicePreviewPngAsync()
        {
            await EnsurePrintWebViewAsync();
            await WaitForInvoiceAssetsAsync(A4PrintWebView);

            if (PdfRenderHost == null || string.IsNullOrWhiteSpace(ViewModel.PrintHtml))
            {
                return Array.Empty<byte>();
            }

            var renderWebView = new WebView2
            {
                Width = PdfRenderPixelWidth,
                Height = PdfRenderPixelHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                FlowDirection = FlowDirection.LeftToRight,
                IsHitTestVisible = false
            };

            PdfRenderHost.Children.Clear();
            PdfRenderHost.Children.Add(renderWebView);
            PdfRenderHost.UpdateLayout();

            try
            {
                await EnsureWebViewAsync(renderWebView, false);
                renderWebView.NavigateToString(ViewModel.PrintHtml);

                await WaitForDocumentReadyAsync(renderWebView);
                await WaitForInvoiceAssetsAsync(renderWebView);
                await ApplyHighDpiCaptureScalingAsync(renderWebView);
                await Task.Delay(180);

                using var stream = new InMemoryRandomAccessStream();
                await renderWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);

                return await ReadStreamBytesAsync(stream);
            }
            finally
            {
                PdfRenderHost.Children.Clear();
            }
        }

        private static async Task<byte[]> ReadStreamBytesAsync(InMemoryRandomAccessStream stream)
        {
            stream.Seek(0);

            using var inputStream = stream.GetInputStreamAt(0);
            using var reader = new DataReader(inputStream);
            var size = checked((uint)stream.Size);
            await reader.LoadAsync(size);

            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }

        private static async Task WaitForDocumentReadyAsync(WebView2 webView)
        {
            for (var attempt = 0; attempt < 120; attempt++)
            {
                try
                {
                    var readyState = await webView.ExecuteScriptAsync("document.readyState");
                    if (string.Equals(readyState, "\"complete\"", StringComparison.Ordinal))
                    {
                        return;
                    }
                }
                catch
                {
                }

                await Task.Delay(50);
            }
        }

        private static async Task ApplyHighDpiCaptureScalingAsync(WebView2 webView)
        {
            var scaleText = PdfCaptureZoomFactor.ToString("0.####", CultureInfo.InvariantCulture);
            var widthText = A4PreviewWidth.ToString("0.###", CultureInfo.InvariantCulture);
            var heightText = A4PreviewHeight.ToString("0.###", CultureInfo.InvariantCulture);

            await webView.ExecuteScriptAsync($@"(() => {{
                const body = document.body;
                const root = document.documentElement;
                if (!body || !root) {{
                    return false;
                }}

                root.style.overflow = 'hidden';
                root.style.background = '#ffffff';
                body.style.margin = '0';
                body.style.width = '{widthText}px';
                body.style.minHeight = '{heightText}px';
                body.style.transformOrigin = 'top left';
                body.style.zoom = '{scaleText}';
                return true;
            }})();");
        }

        private static async Task WaitForInvoiceAssetsAsync(WebView2 webView)
        {
            try
            {
                await webView.ExecuteScriptAsync(@"(async () => {
                    if (document.fonts && document.fonts.ready) {
                        await document.fonts.ready;
                    }

                    const images = Array.from(document.images || []);
                    await Promise.all(images.map(img => img.complete
                        ? Promise.resolve(true)
                        : new Promise(resolve => {
                            const done = () => resolve(true);
                            img.addEventListener('load', done, { once: true });
                            img.addEventListener('error', done, { once: true });
                        })));

                    return true;
                })();");
            }
            catch
            {
            }

            await Task.Delay(150);
        }

        private async Task<bool> SaveWebViewPdfFallbackAsync(StorageFile file)
        {
            try
            {
                var printSettings = A4PrintWebView.CoreWebView2.Environment.CreatePrintSettings();
                printSettings.ShouldPrintBackgrounds = true;
                printSettings.ShouldPrintHeaderAndFooter = false;
                printSettings.ShouldPrintSelectionOnly = false;

                return await A4PrintWebView.CoreWebView2.PrintToPdfAsync(file.Path, printSettings);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<byte[]> BuildRenderedPdfAsync(byte[] pngBytes)
        {
            if (pngBytes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            using var imageStream = new InMemoryRandomAccessStream();
            await imageStream.WriteAsync(pngBytes.AsBuffer());
            imageStream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(imageStream);
            var pixelProvider = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform
                {
                    ScaledWidth = PdfRenderPixelWidth,
                    ScaledHeight = PdfRenderPixelHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant
                },
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            var bgraBytes = pixelProvider.DetachPixelData();
            var rgbBytes = ConvertBgraToRgb(bgraBytes);
            var compressedBytes = CompressRgbBytes(rgbBytes);

            return BuildPdfDocumentBytes((int)PdfRenderPixelWidth, (int)PdfRenderPixelHeight, compressedBytes);
        }

        private static byte[] ConvertBgraToRgb(byte[] bgraBytes)
        {
            var rgbBytes = new byte[(bgraBytes.Length / 4) * 3];

            var sourceIndex = 0;
            var targetIndex = 0;
            while (sourceIndex + 3 < bgraBytes.Length)
            {
                var blue = bgraBytes[sourceIndex++];
                var green = bgraBytes[sourceIndex++];
                var red = bgraBytes[sourceIndex++];
                var alpha = bgraBytes[sourceIndex++];

                if (alpha < 255)
                {
                    red = (byte)((red * alpha + 255 * (255 - alpha)) / 255);
                    green = (byte)((green * alpha + 255 * (255 - alpha)) / 255);
                    blue = (byte)((blue * alpha + 255 * (255 - alpha)) / 255);
                }

                rgbBytes[targetIndex++] = red;
                rgbBytes[targetIndex++] = green;
                rgbBytes[targetIndex++] = blue;
            }

            return rgbBytes;
        }

        private static byte[] CompressRgbBytes(byte[] rgbBytes)
        {
            using var output = new MemoryStream();
            using (var zlibStream = new ZLibStream(output, CompressionLevel.Optimal, true))
            {
                zlibStream.Write(rgbBytes, 0, rgbBytes.Length);
            }

            return output.ToArray();
        }

        private static byte[] BuildPdfDocumentBytes(int pixelWidth, int pixelHeight, byte[] compressedImageBytes)
        {
            const double pageWidthPoints = 595.276;
            const double pageHeightPoints = 841.89;

            var pageWidthText = pageWidthPoints.ToString("0.###", CultureInfo.InvariantCulture);
            var pageHeightText = pageHeightPoints.ToString("0.###", CultureInfo.InvariantCulture);

            var objects = new List<byte[]>
            {
                EncodePdfText("<< /Type /Catalog /Pages 2 0 R >>"),
                EncodePdfText("<< /Type /Pages /Count 1 /Kids [3 0 R] >>"),
                EncodePdfText($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidthText} {pageHeightText}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>"),
                CombinePdfBytes(
                    EncodePdfText($"<< /Type /XObject /Subtype /Image /Width {pixelWidth} /Height {pixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressedImageBytes.Length} >>\nstream\n"),
                    compressedImageBytes,
                    EncodePdfText("\nendstream")),
                BuildContentStreamObject(pageWidthText, pageHeightText)
            };

            return WritePdfFile(objects);
        }

        private static byte[] BuildContentStreamObject(string pageWidthText, string pageHeightText)
        {
            var contentBytes = EncodePdfText($"q\n{pageWidthText} 0 0 {pageHeightText} 0 0 cm\n/Im0 Do\nQ\n");
            return CombinePdfBytes(
                EncodePdfText($"<< /Length {contentBytes.Length} >>\nstream\n"),
                contentBytes,
                EncodePdfText("endstream"));
        }

        private static byte[] WritePdfFile(IReadOnlyList<byte[]> objects)
        {
            using var output = new MemoryStream();
            WritePdfText(output, "%PDF-1.4\n");

            var offsets = new List<long> { 0 };
            for (var index = 0; index < objects.Count; index++)
            {
                offsets.Add(output.Position);
                WritePdfText(output, $"{index + 1} 0 obj\n");
                output.Write(objects[index], 0, objects[index].Length);
                WritePdfText(output, "\nendobj\n");
            }

            var xrefPosition = output.Position;
            WritePdfText(output, $"xref\n0 {objects.Count + 1}\n");
            WritePdfText(output, "0000000000 65535 f \n");

            for (var index = 1; index < offsets.Count; index++)
            {
                WritePdfText(output, $"{offsets[index]:0000000000} 00000 n \n");
            }

            WritePdfText(output, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF");
            return output.ToArray();
        }

        private static void WritePdfText(Stream stream, string value)
        {
            var bytes = EncodePdfText(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] EncodePdfText(string value)
        {
            return Encoding.ASCII.GetBytes(value);
        }

        private static byte[] CombinePdfBytes(params byte[][] parts)
        {
            using var output = new MemoryStream();
            foreach (var part in parts)
            {
                output.Write(part, 0, part.Length);
            }

            return output.ToArray();
        }
    }
}
