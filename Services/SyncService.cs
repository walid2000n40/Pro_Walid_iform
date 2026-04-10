using Dapper;
using Microsoft.Data.Sqlite;
using ProWalid.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProWalid.Services
{
    public class SyncService
    {
        private readonly ApiService _api;
        private readonly string _connectionString;
        private readonly string _deviceId;
        private readonly string _syncStatePath;

        public event Action<string>? OnProgress;

        public SyncService(string dbPath, string serverUrl, string apiKey)
        {
            _connectionString = $"Data Source={dbPath}";
            _api = new ApiService(serverUrl, apiKey);
            _deviceId = Environment.MachineName + "-" + Environment.UserName;
            var appFolder = Path.GetDirectoryName(dbPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            _syncStatePath = Path.Combine(appFolder, "sync_state.json");
        }

        public async Task EnsureSyncColumnsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var tables = new[] { "Customers", "Transactions", "TransactionItems" };
            var cols = new[] { ("SyncUuid", "TEXT"), ("UpdatedAt", "TEXT"), ("IsDirty", "INTEGER DEFAULT 1"), ("ServerId", "INTEGER DEFAULT 0") };

            foreach (var table in tables)
            {
                var existing = (await connection.QueryAsync($"PRAGMA table_info({table})")).Select(c => (string)c.name).ToList();
                foreach (var (colName, colType) in cols)
                {
                    if (!existing.Any(n => string.Equals(n, colName, StringComparison.OrdinalIgnoreCase)))
                        await connection.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {colName} {colType}");
                }
            }

            await connection.ExecuteAsync("UPDATE Customers SET SyncUuid = lower(hex(randomblob(4))||'-'||hex(randomblob(2))||'-'||hex(randomblob(2))||'-'||hex(randomblob(2))||'-'||hex(randomblob(6))) WHERE SyncUuid IS NULL OR TRIM(SyncUuid) = ''");
            await connection.ExecuteAsync("UPDATE Transactions SET SyncUuid = lower(hex(randomblob(4))||'-'||hex(randomblob(2))||'-'||hex(randomblob(2))||'-'||hex(randomblob(2))||'-'||hex(randomblob(6))) WHERE SyncUuid IS NULL OR TRIM(SyncUuid) = ''");
            await connection.ExecuteAsync("UPDATE TransactionItems SET SyncUuid = lower(hex(randomblob(4))||'-'||hex(randomblob(2))||'-'||hex(randomblob(2))||'-'||hex(randomblob(2))||'-'||hex(randomblob(6))) WHERE SyncUuid IS NULL OR TRIM(SyncUuid) = ''");
            await connection.ExecuteAsync("UPDATE Customers SET UpdatedAt = CreatedAt WHERE UpdatedAt IS NULL");
            await connection.ExecuteAsync("UPDATE Transactions SET UpdatedAt = TransactionDate WHERE UpdatedAt IS NULL");
            await connection.ExecuteAsync("UPDATE TransactionItems SET UpdatedAt = datetime('now') WHERE UpdatedAt IS NULL");
            await connection.ExecuteAsync("UPDATE Customers SET IsDirty = 1 WHERE IsDirty IS NULL");
            await connection.ExecuteAsync("UPDATE Transactions SET IsDirty = 1 WHERE IsDirty IS NULL");
            await connection.ExecuteAsync("UPDATE TransactionItems SET IsDirty = 1 WHERE IsDirty IS NULL");
        }

        public Task<bool> PingServerAsync() => _api.PingAsync();

        public async Task<SyncResult> FullSyncAsync()
        {
            var result = new SyncResult();
            try
            {
                OnProgress?.Invoke("جاري التحقق من الاتصال...");
                if (!await _api.PingAsync())
                {
                    result.Success = false;
                    result.Message = "لا يمكن الاتصال بالسيرفر";
                    return result;
                }

                await EnsureSyncColumnsAsync();

                OnProgress?.Invoke("جاري تجهيز البيانات المحلية...");
                var request = await BuildPushRequestAsync();

                OnProgress?.Invoke("جاري المزامنة مع السيرفر...");
                var response = await _api.SyncAsync(request);

                if (response == null || response.Status != "ok")
                {
                    result.Success = false;
                    result.Message = response?.Error ?? "استجابة غير صالحة من السيرفر";
                    return result;
                }

                OnProgress?.Invoke("جاري حفظ البيانات الواردة...");
                await ApplyPullDataAsync(response);
                await MarkPushedRecordsCleanAsync();
                SaveSyncState(response.ServerTime ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                result.Success = true;
                result.ClientsPushed = response.Stats?.ClientsPushed ?? 0;
                result.ClientsPulled = response.Stats?.ClientsPulled ?? 0;
                result.TransactionsPushed = response.Stats?.TxPushed ?? 0;
                result.TransactionsPulled = response.Stats?.TxPulled ?? 0;
                result.ItemsPushed = response.Stats?.ItemsPushed ?? 0;
                result.ItemsPulled = response.Stats?.ItemsPulled ?? 0;
                int pushed = result.ClientsPushed + result.TransactionsPushed + result.ItemsPushed;
                int pulled = result.ClientsPulled + result.TransactionsPulled + result.ItemsPulled;
                result.Message = $"تمت المزامنة بنجاح\nدفع {pushed} سجل | سحب {pulled} سجل";
                OnProgress?.Invoke(result.Message);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطأ في المزامنة: {ex.Message}";
                OnProgress?.Invoke(result.Message);
            }
            return result;
        }

        private async Task<SyncRequest> BuildPushRequestAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var request = new SyncRequest { DeviceId = _deviceId, LastSync = GetLastSyncTime() };

            var dirtyCustomers = await connection.QueryAsync("SELECT Id, CustomerNumber, Name, Phone, Email, Address, Notes, CreatedAt, SyncUuid, UpdatedAt FROM Customers WHERE IsDirty = 1 OR IsDirty IS NULL");
            foreach (var c in dirtyCustomers)
            {
                request.Clients.Add(new SyncClientPush
                {
                    DesktopId = (long)c.Id,
                    SyncUuid = c.SyncUuid?.ToString() ?? "",
                    Name = c.Name?.ToString() ?? "",
                    Phone = c.Phone?.ToString() ?? "",
                    Notes = c.Notes?.ToString() ?? "",
                    CreatedAt = c.CreatedAt?.ToString() ?? "",
                    UpdatedAt = c.UpdatedAt?.ToString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            var dirtyTx = await connection.QueryAsync("SELECT Id, CustomerId, InvoiceNumber, CompanyName, EmployeeName, TransactionDate, CreatedAt, SyncUuid, UpdatedAt FROM Transactions WHERE IsDirty = 1 OR IsDirty IS NULL");
            foreach (var t in dirtyTx)
            {
                request.Transactions.Add(new SyncTransactionPush
                {
                    DesktopId = (long)t.Id,
                    DesktopClientId = t.CustomerId != null ? (long)t.CustomerId : 0,
                    SyncUuid = t.SyncUuid?.ToString() ?? "",
                    InvoiceNumber = t.InvoiceNumber?.ToString() ?? "",
                    CompanyName = t.CompanyName?.ToString() ?? "",
                    EmployeeName = t.EmployeeName?.ToString() ?? "",
                    CreatedAt = t.CreatedAt?.ToString() ?? t.TransactionDate?.ToString() ?? "",
                    UpdatedAt = t.UpdatedAt?.ToString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            var dirtyItems = await connection.QueryAsync("SELECT ti.Id, ti.TransactionId, ti.ServiceName, ti.Quantity, ti.UnitPrice, ti.Profit, ti.GovFees, ti.SyncUuid, ti.UpdatedAt, t.CompanyName, t.EmployeeName, t.TransactionDate FROM TransactionItems ti JOIN Transactions t ON t.Id = ti.TransactionId WHERE ti.IsDirty = 1 OR ti.IsDirty IS NULL");
            foreach (var item in dirtyItems)
            {
                request.LineItems.Add(new SyncLineItemPush
                {
                    DesktopTransactionId = (long)item.TransactionId,
                    SyncUuid = item.SyncUuid?.ToString() ?? "",
                    ServiceName = item.ServiceName?.ToString() ?? "",
                    Quantity = item.Quantity != null ? (double)item.Quantity : 1,
                    UnitPrice = item.UnitPrice != null ? (double)item.UnitPrice : 0,
                    Total = (item.Quantity != null && item.UnitPrice != null) ? (double)item.Quantity * (double)item.UnitPrice : 0,
                    CompanyName = item.CompanyName?.ToString() ?? "",
                    EmployeeName = item.EmployeeName?.ToString() ?? "",
                    GovFees = item.GovFees?.ToString() ?? "",
                    ItemDate = item.TransactionDate?.ToString() ?? "",
                    UpdatedAt = item.UpdatedAt?.ToString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            return request;
        }

        private async Task ApplyPullDataAsync(SyncResponse response)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            foreach (var sc in response.Clients ?? new List<SyncClientPull>())
            {
                if (string.IsNullOrWhiteSpace(sc.SyncUuid)) continue;
                var existing = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id, UpdatedAt FROM Customers WHERE SyncUuid = @SyncUuid", new { sc.SyncUuid });
                if (existing != null)
                {
                    var localUp = existing.UpdatedAt?.ToString() ?? "2000-01-01";
                    if (string.Compare(sc.UpdatedAt ?? "2000-01-01", localUp, StringComparison.Ordinal) > 0)
                        await connection.ExecuteAsync("UPDATE Customers SET Name=@Name, Phone=@Phone, Notes=@Notes, UpdatedAt=@UpdatedAt, ServerId=@ServerId, IsDirty=0 WHERE SyncUuid=@SyncUuid",
                            new { sc.Name, Phone = sc.Phone ?? "", Notes = sc.Notes ?? "", sc.UpdatedAt, ServerId = sc.Id, sc.SyncUuid });
                }
                else
                {
                    var byName = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Customers WHERE Name = @Name COLLATE NOCASE AND (SyncUuid IS NULL OR TRIM(SyncUuid) = '')", new { sc.Name });
                    if (byName != null)
                        await connection.ExecuteAsync("UPDATE Customers SET SyncUuid=@SyncUuid, Phone=COALESCE(NULLIF(@Phone,''),Phone), ServerId=@ServerId, UpdatedAt=@UpdatedAt, IsDirty=0 WHERE Id=@Id",
                            new { sc.SyncUuid, Phone = sc.Phone ?? "", ServerId = sc.Id, sc.UpdatedAt, Id = (long)byName.Id });
                    else
                    {
                        var nextNum = await connection.QueryFirstOrDefaultAsync<long?>("SELECT MAX(CASE WHEN CustomerNumber > 0 THEN CustomerNumber ELSE Id END) FROM Customers") ?? 0;
                        await connection.ExecuteAsync("INSERT INTO Customers (CustomerNumber, Name, Phone, Email, Address, Notes, CreatedAt, SyncUuid, UpdatedAt, ServerId, IsDirty) VALUES (@CN, @Name, @Phone, '', '', @Notes, @CA, @SyncUuid, @UA, @SI, 0)",
                            new { CN = nextNum + 1, sc.Name, Phone = sc.Phone ?? "", Notes = sc.Notes ?? "", CA = sc.CreatedAt, sc.SyncUuid, UA = sc.UpdatedAt, SI = sc.Id });
                    }
                }
            }

            foreach (var st in response.Transactions ?? new List<SyncTransactionPull>())
            {
                if (string.IsNullOrWhiteSpace(st.SyncUuid)) continue;
                var existing = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id, UpdatedAt FROM Transactions WHERE SyncUuid = @SyncUuid", new { st.SyncUuid });
                if (existing != null)
                {
                    var localUp = existing.UpdatedAt?.ToString() ?? "2000-01-01";
                    if (string.Compare(st.UpdatedAt ?? "2000-01-01", localUp, StringComparison.Ordinal) > 0)
                    {
                        long localCid = 0;
                        if (!string.IsNullOrWhiteSpace(st.ClientSyncUuid))
                        {
                            var cust = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Customers WHERE SyncUuid = @Uuid", new { Uuid = st.ClientSyncUuid });
                            if (cust != null) localCid = (long)cust.Id;
                        }
                        if (localCid > 0)
                            await connection.ExecuteAsync("UPDATE Transactions SET CustomerId=@CId, InvoiceNumber=@Inv, UpdatedAt=@UA, ServerId=@SI, IsDirty=0 WHERE SyncUuid=@SyncUuid",
                                new { CId = localCid, Inv = st.InvoiceNumber, UA = st.UpdatedAt, SI = st.Id, st.SyncUuid });
                    }
                }
                else
                {
                    var byInv = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Transactions WHERE InvoiceNumber = @InvoiceNumber", new { st.InvoiceNumber });
                    if (byInv != null)
                    {
                        await connection.ExecuteAsync("UPDATE Transactions SET SyncUuid=@SyncUuid, ServerId=@SI, IsDirty=0 WHERE Id=@Id", new { st.SyncUuid, SI = st.Id, Id = (long)byInv.Id });
                        continue;
                    }
                    long localCid2 = 0;
                    if (!string.IsNullOrWhiteSpace(st.ClientSyncUuid))
                    {
                        var cust2 = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Customers WHERE SyncUuid = @Uuid", new { Uuid = st.ClientSyncUuid });
                        if (cust2 != null) localCid2 = (long)cust2.Id;
                    }
                    await connection.ExecuteAsync("INSERT INTO Transactions (CustomerId, TransactionStatus, InvoiceNumber, InvoiceTemplateKey, CompanyName, EmployeeName, TransactionDate, GrandTotal, CreatedAt, SyncUuid, UpdatedAt, ServerId, IsDirty) VALUES (@CId, N'معلق', @Inv, '', '', '', @CA, 0, @CA, @SyncUuid, @UA, @SI, 0)",
                        new { CId = localCid2, Inv = st.InvoiceNumber, CA = st.CreatedAt, st.SyncUuid, UA = st.UpdatedAt, SI = st.Id });
                }
            }

            foreach (var si in response.LineItems ?? new List<SyncLineItemPull>())
            {
                if (string.IsNullOrWhiteSpace(si.SyncUuid)) continue;
                var existingI = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM TransactionItems WHERE SyncUuid = @SyncUuid", new { si.SyncUuid });
                if (existingI != null) continue;
                long localTxId = 0;
                if (!string.IsNullOrWhiteSpace(si.TransactionSyncUuid))
                {
                    var tx = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Transactions WHERE SyncUuid = @Uuid", new { Uuid = si.TransactionSyncUuid });
                    if (tx != null) localTxId = (long)tx.Id;
                }
                if (localTxId <= 0) continue;
                double qty = 1;
                if (double.TryParse(si.Quantity, out var pq)) qty = pq;
                await connection.ExecuteAsync("INSERT INTO TransactionItems (TransactionId, ServiceName, Quantity, UnitPrice, Profit, GovFees, AttachmentPath, SyncUuid, UpdatedAt, ServerId, IsDirty) VALUES (@TId, @SN, @Qty, @UP, 0, @GF, '', @SyncUuid, @UA, @SI, 0)",
                    new { TId = localTxId, SN = si.ServiceName ?? "", Qty = qty, UP = si.UnitPrice, GF = si.GovFees ?? "", si.SyncUuid, UA = si.UpdatedAt, SI = si.Id });
            }
        }

        private async Task MarkPushedRecordsCleanAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync("UPDATE Customers SET IsDirty = 0 WHERE IsDirty = 1");
            await connection.ExecuteAsync("UPDATE Transactions SET IsDirty = 0 WHERE IsDirty = 1");
            await connection.ExecuteAsync("UPDATE TransactionItems SET IsDirty = 0 WHERE IsDirty = 1");
        }

        private string GetLastSyncTime()
        {
            try
            {
                if (File.Exists(_syncStatePath))
                {
                    var json = File.ReadAllText(_syncStatePath);
                    var state = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    return state?.GetValueOrDefault("last_sync") ?? "2000-01-01 00:00:00";
                }
            }
            catch { }
            return "2000-01-01 00:00:00";
        }

        private void SaveSyncState(string serverTime)
        {
            try
            {
                var state = new Dictionary<string, string> { ["last_sync"] = serverTime, ["device_id"] = _deviceId };
                File.WriteAllText(_syncStatePath, JsonSerializer.Serialize(state));
            }
            catch { }
        }
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int ClientsPushed { get; set; }
        public int ClientsPulled { get; set; }
        public int TransactionsPushed { get; set; }
        public int TransactionsPulled { get; set; }
        public int ItemsPushed { get; set; }
        public int ItemsPulled { get; set; }
    }
}
