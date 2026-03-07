using System;
using System.IO;
using System.Threading.Tasks;

namespace ProWalid.Data
{
    public class AttachmentManager
    {
        private readonly string _attachmentsFolder;

        public AttachmentManager()
        {
            var appFolder = AppDomain.CurrentDomain.BaseDirectory;
            _attachmentsFolder = Path.Combine(appFolder, "Attachments");
            
            if (!Directory.Exists(_attachmentsFolder))
            {
                Directory.CreateDirectory(_attachmentsFolder);
            }
        }

        public async Task<string> SaveAttachmentAsync(string sourceFilePath)
        {
            if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
                return string.Empty;

            try
            {
                var fileName = Path.GetFileName(sourceFilePath);
                var uniqueFileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{fileName}";
                var destinationPath = Path.Combine(_attachmentsFolder, uniqueFileName);

                await Task.Run(() => File.Copy(sourceFilePath, destinationPath, true));

                return destinationPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving attachment: {ex.Message}");
                return string.Empty;
            }
        }

        public bool DeleteAttachment(string attachmentPath)
        {
            if (string.IsNullOrEmpty(attachmentPath) || !File.Exists(attachmentPath))
                return false;

            try
            {
                File.Delete(attachmentPath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting attachment: {ex.Message}");
                return false;
            }
        }

        public bool AttachmentExists(string attachmentPath)
        {
            return !string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath);
        }

        public async Task OpenAttachmentAsync(string attachmentPath)
        {
            if (!AttachmentExists(attachmentPath))
                return;

            try
            {
                await Task.Run(() =>
                {
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = attachmentPath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(processStartInfo);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening attachment: {ex.Message}");
            }
        }
    }
}
