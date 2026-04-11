using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProWalid.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public ApiService(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<bool> PingAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/sync.php?action=ping");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<SyncResponse?> SyncAsync(SyncRequest request)
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/sync.php", content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Sync failed (HTTP {(int)response.StatusCode}): {body}");
            return JsonSerializer.Deserialize<SyncResponse>(body, JsonOptions);
        }

        public async Task<bool> UploadAttachmentAsync(string filePath, string lineItemSyncUuid, string invoiceNumber, int lineIndex)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(lineItemSyncUuid), "line_item_sync_uuid");
                form.Add(new StringContent(invoiceNumber), "invoice_number");
                form.Add(new StringContent(lineIndex.ToString()), "line_index");

                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/upload.php", form);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

    }
}