using CommunityToolkit.Mvvm.ComponentModel;

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
    }
}
