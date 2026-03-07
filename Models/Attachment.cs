using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace ProWalid.Models
{
    public partial class Attachment : ObservableObject
    {
        [ObservableProperty]
        private long id;

        [ObservableProperty]
        private long transactionItemId;

        [ObservableProperty]
        private string fileName = string.Empty;

        [ObservableProperty]
        private string filePath = string.Empty;

        [ObservableProperty]
        private string originalFileName = string.Empty;

        [ObservableProperty]
        private long fileSize;

        [ObservableProperty]
        private string fileExtension = string.Empty;

        public bool IsPdfAttachment => string.Equals(NormalizedExtension, ".pdf", StringComparison.OrdinalIgnoreCase);

        public bool IsImageAttachment =>
            string.Equals(NormalizedExtension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizedExtension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizedExtension, ".png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizedExtension, ".gif", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizedExtension, ".bmp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizedExtension, ".webp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizedExtension, ".tif", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizedExtension, ".tiff", StringComparison.OrdinalIgnoreCase);

        public bool IsOtherAttachment => !IsPdfAttachment && !IsImageAttachment;

        private string NormalizedExtension
        {
            get
            {
                var extension = FileExtension;

                if (string.IsNullOrWhiteSpace(extension) && !string.IsNullOrWhiteSpace(OriginalFileName))
                {
                    extension = Path.GetExtension(OriginalFileName);
                }

                if (string.IsNullOrWhiteSpace(extension) && !string.IsNullOrWhiteSpace(FileName))
                {
                    extension = Path.GetExtension(FileName);
                }

                return extension?.Trim().StartsWith(".") == true ? extension.Trim() : $".{extension?.Trim()}";
            }
        }

        partial void OnFileExtensionChanged(string value)
        {
            NotifyAttachmentTypeProperties();
        }

        partial void OnOriginalFileNameChanged(string value)
        {
            NotifyAttachmentTypeProperties();
        }

        partial void OnFileNameChanged(string value)
        {
            NotifyAttachmentTypeProperties();
        }

        private void NotifyAttachmentTypeProperties()
        {
            OnPropertyChanged(nameof(IsPdfAttachment));
            OnPropertyChanged(nameof(IsImageAttachment));
            OnPropertyChanged(nameof(IsOtherAttachment));
        }
    }
}
