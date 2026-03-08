using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

        public async Task<List<ProWalid.Models.Attachment>> SaveMultipleAttachmentsAsync(List<string> sourceFilePaths, string transactionId)
        {
            var savedAttachments = new List<ProWalid.Models.Attachment>();

            foreach (var sourceFilePath in sourceFilePaths)
            {
                if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
                    continue;

                try
                {
                    var fileInfo = new FileInfo(sourceFilePath);
                    var originalFileName = fileInfo.Name;
                    var fileExtension = fileInfo.Extension;
                    var fileSize = fileInfo.Length;
                    
                    var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    var uniqueFileName = $"{transactionId}_{timestamp}_{Guid.NewGuid():N}{fileExtension}";
                    var destinationPath = Path.Combine(_attachmentsFolder, uniqueFileName);

                    await Task.Run(() => File.Copy(sourceFilePath, destinationPath, true));

                    savedAttachments.Add(new ProWalid.Models.Attachment
                    {
                        FileName = uniqueFileName,
                        FilePath = destinationPath,
                        OriginalFileName = originalFileName,
                        FileSize = fileSize,
                        FileExtension = fileExtension
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving attachment {sourceFilePath}: {ex.Message}");
                }
            }

            return savedAttachments;
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

        public async Task<string> CreateZipForAttachmentsAsync(IEnumerable<ProWalid.Models.Attachment> attachments, string folderName, string zipFileName)
        {
            var validAttachments = attachments
                .Where(attachment => attachment != null && !string.IsNullOrWhiteSpace(attachment.FilePath) && File.Exists(attachment.FilePath))
                .ToList();

            if (validAttachments.Count == 0)
            {
                return string.Empty;
            }

            var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloadsFolder);

            var safeFolderName = SanitizeFileName(string.IsNullOrWhiteSpace(folderName) ? "Attachments" : folderName);
            var safeZipFileName = SanitizeFileName(string.IsNullOrWhiteSpace(zipFileName) ? "Attachments" : zipFileName);
            var zipPath = Path.Combine(downloadsFolder, $"{safeZipFileName}.zip");

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var attachment in validAttachments)
                {
                    var sourcePath = attachment.FilePath;
                    var originalName = !string.IsNullOrWhiteSpace(attachment.OriginalFileName)
                        ? attachment.OriginalFileName
                        : Path.GetFileName(sourcePath);

                    var safeEntryName = GetUniqueFileName(SanitizeFileName(originalName), usedEntryNames);
                    var entryPath = Path.Combine(safeFolderName, safeEntryName).Replace('\\', '/');
                    archive.CreateEntryFromFile(sourcePath, entryPath);
                }
            });

            return zipPath;
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "Attachments" : sanitized;
        }

        private static string GetUniqueFileName(string fileName, ISet<string> usedFileNames)
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var candidate = string.IsNullOrWhiteSpace(fileName) ? $"file{extension}" : fileName;
            var counter = 1;

            while (!usedFileNames.Add(candidate))
            {
                candidate = $"{baseName} ({counter}){extension}";
                counter++;
            }

            return candidate;
        }
    }
}
