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
        private readonly object _syncLock = new();
        private bool _isSyncing;

        public bool IsSyncing => _isSyncing;
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
            lock (_syncLock)
            {
                if (_isSyncing)
                {
                    result.Success = false;
                    result.Message = "المزامنة قيد التنفيذ بالفعل";
                    return result;
                }
                _isSyncing = true;
            }
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
                await DeduplicateAndFixSequenceAsync();
                await UploadPendingAttachmentsAsync();
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
            finally
            {
                _isSyncing = false;
            }
            return result;
        }

        private async Task<SyncRequest> BuildPushRequestAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var request = new SyncRequest { DeviceId = _deviceId, LastSync = GetLastSyncTime() };

            var dirtyCustomers = await connection.QueryAsync("SELECT Id, CustomerNumber, Name, Phone, Email, Address, Notes, CreatedAt, SyncUuid, UpdatedAt, COALESCE(IsDeleted,0) AS IsDeleted FROM Customers WHERE IsDirty = 1 OR IsDirty IS NULL");
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
                    UpdatedAt = c.UpdatedAt?.ToString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    IsDeleted = c.IsDeleted != null ? (int)(long)c.IsDeleted : 0
                });
            }

            var dirtyTx = await connection.QueryAsync("SELECT t.Id, t.CustomerId, t.InvoiceNumber, t.CompanyName, t.EmployeeName, t.TransactionDate, t.CreatedAt, t.SyncUuid, t.UpdatedAt, COALESCE(t.IsDeleted,0) AS IsDeleted, c.ServerId AS ClientServerId, c.SyncUuid AS ClientSyncUuid FROM Transactions t LEFT JOIN Customers c ON c.Id = t.CustomerId WHERE t.IsDirty = 1 OR t.IsDirty IS NULL");
            foreach (var t in dirtyTx)
            {
                request.Transactions.Add(new SyncTransactionPush
                {
                    DesktopId = (long)t.Id,
                    DesktopClientId = t.CustomerId != null ? (long)t.CustomerId : 0,
                    ServerClientId = t.ClientServerId != null ? (long)t.ClientServerId : 0,
                    ClientSyncUuid = t.ClientSyncUuid?.ToString() ?? "",
                    SyncUuid = t.SyncUuid?.ToString() ?? "",
                    InvoiceNumber = t.InvoiceNumber?.ToString() ?? "",
                    CompanyName = t.CompanyName?.ToString() ?? "",
                    EmployeeName = t.EmployeeName?.ToString() ?? "",
                    CreatedAt = t.CreatedAt?.ToString() ?? t.TransactionDate?.ToString() ?? "",
                    UpdatedAt = t.UpdatedAt?.ToString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    IsDeleted = t.IsDeleted != null ? (int)(long)t.IsDeleted : 0
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

            // Push saved invoices
            try
            {
                var savedInvoices = await connection.QueryAsync(
                    @"SELECT SavedInvoiceNumber, RootInvoiceNumber, SourceInvoiceNumber, GroupedSequenceNumber,
                             SavedKind, TemplateKey, CustomerId, CustomerName, CompanyName, InvoiceDate,
                             TotalAmount, Notes, PrintHtml, PayloadJson, SavedAt, RelatedIndividualIds
                      FROM SavedInvoices");
                foreach (var si in savedInvoices)
                {
                    request.SavedInvoices.Add(new SyncSavedInvoicePush
                    {
                        SavedInvoiceNumber = si.SavedInvoiceNumber?.ToString() ?? "",
                        RootInvoiceNumber = si.RootInvoiceNumber?.ToString() ?? "",
                        SourceInvoiceNumber = si.SourceInvoiceNumber?.ToString() ?? "",
                        GroupedSequenceNumber = si.GroupedSequenceNumber != null ? (int)(long)si.GroupedSequenceNumber : 0,
                        SavedKind = si.SavedKind?.ToString() ?? "single",
                        TemplateKey = si.TemplateKey?.ToString() ?? "",
                        CustomerId = si.CustomerId != null ? (long)si.CustomerId : 0,
                        CustomerName = si.CustomerName?.ToString() ?? "",
                        CompanyName = si.CompanyName?.ToString() ?? "",
                        InvoiceDate = si.InvoiceDate?.ToString() ?? "",
                        TotalAmount = si.TotalAmount != null ? (double)si.TotalAmount : 0,
                        Notes = si.Notes?.ToString() ?? "",
                        PrintHtml = si.PrintHtml?.ToString() ?? "",
                        PayloadJson = si.PayloadJson?.ToString() ?? "",
                        SavedAt = si.SavedAt?.ToString() ?? "",
                        RelatedIndividualIds = si.RelatedIndividualIds?.ToString() ?? ""
                    });
                }
            }
            catch { }

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
                        await connection.ExecuteAsync("UPDATE Customers SET Name=@Name, Phone=@Phone, Notes=@Notes, UpdatedAt=@UpdatedAt, ServerId=@ServerId, IsDeleted=@IsDeleted, IsDirty=0 WHERE SyncUuid=@SyncUuid",
                            new { sc.Name, Phone = sc.Phone ?? "", Notes = sc.Notes ?? "", sc.UpdatedAt, ServerId = sc.IdLong, sc.IsDeleted, sc.SyncUuid });
                }
                else
                {
                    var byName = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Customers WHERE Name = @Name COLLATE NOCASE AND (SyncUuid IS NULL OR TRIM(SyncUuid) = '')", new { sc.Name });
                    if (byName != null)
                        await connection.ExecuteAsync("UPDATE Customers SET SyncUuid=@SyncUuid, Phone=COALESCE(NULLIF(@Phone,''),Phone), ServerId=@ServerId, UpdatedAt=@UpdatedAt, IsDeleted=@IsDeleted, IsDirty=0 WHERE Id=@Id",
                            new { sc.SyncUuid, Phone = sc.Phone ?? "", ServerId = sc.IdLong, sc.UpdatedAt, sc.IsDeleted, Id = (long)byName.Id });
                    else
                    {
                        var serverId = sc.IdLong;
                        var existingId = await connection.QueryFirstOrDefaultAsync<long?>("SELECT Id FROM Customers WHERE Id = @Id", new { Id = serverId });
                        if (existingId == null && serverId > 0)
                        {
                            await connection.ExecuteAsync("INSERT INTO Customers (Id, CustomerNumber, Name, Phone, Email, Address, Notes, CreatedAt, SyncUuid, UpdatedAt, ServerId, IsDirty, IsDeleted) VALUES (@Id, @CN, @Name, @Phone, '', '', @Notes, @CA, @SyncUuid, @UA, @SI, 0, @IsD)",
                                new { Id = serverId, CN = serverId, sc.Name, Phone = sc.Phone ?? "", Notes = sc.Notes ?? "", CA = sc.CreatedAt, sc.SyncUuid, UA = sc.UpdatedAt, SI = serverId, IsD = sc.IsDeleted });
                        }
                        else
                        {
                            var nextNum = await connection.QueryFirstOrDefaultAsync<long?>("SELECT MAX(CASE WHEN CustomerNumber > 0 THEN CustomerNumber ELSE Id END) FROM Customers") ?? 0;
                            await connection.ExecuteAsync("INSERT INTO Customers (CustomerNumber, Name, Phone, Email, Address, Notes, CreatedAt, SyncUuid, UpdatedAt, ServerId, IsDirty, IsDeleted) VALUES (@CN, @Name, @Phone, '', '', @Notes, @CA, @SyncUuid, @UA, @SI, 0, @IsD)",
                                new { CN = nextNum + 1, sc.Name, Phone = sc.Phone ?? "", Notes = sc.Notes ?? "", CA = sc.CreatedAt, sc.SyncUuid, UA = sc.UpdatedAt, SI = sc.IdLong, IsD = sc.IsDeleted });
                        }
                    }
                }
            }

            foreach (var st in response.Transactions ?? new List<SyncTransactionPull>())
            {
                if (string.IsNullOrWhiteSpace(st.SyncUuid)) continue;
                long localCid = 0;
                if (!string.IsNullOrWhiteSpace(st.ClientSyncUuid))
                {
                    var cust = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Customers WHERE SyncUuid = @Uuid", new { Uuid = st.ClientSyncUuid });
                    if (cust != null) localCid = (long)cust.Id;
                }
                string status = (!string.IsNullOrWhiteSpace(st.Status)) ? st.Status : "\u0645\u0639\u0644\u0642";
                var existing = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Transactions WHERE SyncUuid = @SyncUuid", new { st.SyncUuid });
                if (existing != null)
                {
                    await connection.ExecuteAsync("UPDATE Transactions SET TransactionStatus=@ST, CompanyName=@CN, EmployeeName=@EN, CustomerId=CASE WHEN @CId > 0 THEN @CId ELSE CustomerId END, InvoiceNumber=@Inv, UpdatedAt=@UA, ServerId=@SI, IsDeleted=@IsD, IsDirty=0 WHERE SyncUuid=@SyncUuid",
                        new { ST = status, CId = localCid, Inv = st.InvoiceNumber, CN = st.CompanyName ?? "", EN = st.EmployeeName ?? "", UA = st.UpdatedAt, SI = st.IdLong, IsD = st.IsDeleted, st.SyncUuid });
                }
                else
                {
                    var conflicting = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Transactions WHERE InvoiceNumber = @Inv AND SyncUuid != @SyncUuid", new { Inv = st.InvoiceNumber, st.SyncUuid });
                    if (conflicting != null)
                    {
                        var newInv = await connection.QueryFirstOrDefaultAsync<long?>("SELECT MAX(CAST(InvoiceNumber AS INTEGER)) FROM Transactions WHERE InvoiceNumber IS NOT NULL AND TRIM(InvoiceNumber) != ''") ?? 999;
                        await connection.ExecuteAsync("UPDATE Transactions SET InvoiceNumber = @NewInv WHERE Id = @Id", new { NewInv = (newInv + 1).ToString("D5"), Id = (long)conflicting.Id });
                    }
                    await connection.ExecuteAsync("INSERT INTO Transactions (CustomerId, TransactionStatus, InvoiceNumber, InvoiceTemplateKey, CompanyName, EmployeeName, TransactionDate, GrandTotal, CreatedAt, SyncUuid, UpdatedAt, ServerId, IsDirty) VALUES (@CId, @ST, @Inv, \'\', @CN, @EN, @CA, 0, @CA, @SyncUuid, @UA, @SI, 0)",
                        new { CId = localCid, ST = status, Inv = st.InvoiceNumber, CN = st.CompanyName ?? "", EN = st.EmployeeName ?? "", CA = st.CreatedAt, st.SyncUuid, UA = st.UpdatedAt, SI = st.IdLong, IsD = st.IsDeleted });
                }
            }

            // Duplicate-safe: no longer bulk-delete remote attachments; dedup check below handles it

            foreach (var si in response.LineItems ?? new List<SyncLineItemPull>())
            {
                if (string.IsNullOrWhiteSpace(si.SyncUuid)) continue;
                long localTxId = 0;
                if (!string.IsNullOrWhiteSpace(si.TransactionSyncUuid))
                {
                    var tx = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM Transactions WHERE SyncUuid = @Uuid", new { Uuid = si.TransactionSyncUuid });
                    if (tx != null) localTxId = (long)tx.Id;
                }
                if (localTxId <= 0) continue;
                double qty = 1;
                if (double.TryParse(si.Quantity, out var pq)) qty = pq;
                var existingI = await connection.QueryFirstOrDefaultAsync<dynamic>("SELECT Id FROM TransactionItems WHERE SyncUuid = @SyncUuid", new { si.SyncUuid });
                long itemId;
                if (existingI != null)
                {
                    itemId = (long)existingI.Id;
                    await connection.ExecuteAsync("UPDATE TransactionItems SET ServiceName=@SN, Quantity=@Qty, UnitPrice=@UP, GovFees=@GF, UpdatedAt=@UA, ServerId=@SI, IsDirty=0 WHERE Id=@Id",
                        new { SN = si.ServiceName ?? "", Qty = qty, UP = si.UnitPriceDouble, GF = si.GovFees ?? "", UA = si.UpdatedAt, SI = si.IdLong, Id = itemId });
                }
                else
                {
                    itemId = await connection.QuerySingleAsync<long>("INSERT INTO TransactionItems (TransactionId, ServiceName, Quantity, UnitPrice, Profit, GovFees, AttachmentPath, SyncUuid, UpdatedAt, ServerId, IsDirty) VALUES (@TId, @SN, @Qty, @UP, 0, @GF, \'\', @SyncUuid, @UA, @SI, 0); SELECT last_insert_rowid();",
                        new { TId = localTxId, SN = si.ServiceName ?? "", Qty = qty, UP = si.UnitPriceDouble, GF = si.GovFees ?? "", si.SyncUuid, UA = si.UpdatedAt, SI = si.IdLong });
                }
                if (!string.IsNullOrWhiteSpace(si.Attachments) && si.Attachments != "[]")
                {
                    try
                    {
                        var attachments = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, string>>>(si.Attachments);
                        if (attachments != null)
                        {
                            foreach (var att in attachments)
                            {
                                var fileName = att.GetValueOrDefault("name") ?? "file";
                                var url = att.GetValueOrDefault("url") ?? "";
                                if (string.IsNullOrWhiteSpace(url)) continue;
                                var relPath = url.Replace("\\/", "/");
                                if (relPath.StartsWith("/v2_test/uploads/")) relPath = relPath.Substring("/v2_test/uploads/".Length);
                                var fullUrl = "https://informtyping.com/v2_test/api/serve_file.php?api_key=85d7bd6243258f6d4d057ffa3885263566f69422a457b2b11a04edd6fbeb456b&path=" + relPath;
                                var existingAtt = await connection.QueryFirstOrDefaultAsync<dynamic>(
                                    @"SELECT Id FROM Attachments 
                                      WHERE TransactionItemId=@ItemId 
                                        AND OriginalFileName=@FN", 
                                    new { ItemId = itemId, FN = fileName });
                                if (existingAtt == null)
                                {
                                    // Also check if there's an uploaded:// variant for the same file
                                    var uploadedVariant = await connection.QueryFirstOrDefaultAsync<dynamic>(
                                        @"SELECT Id FROM Attachments 
                                          WHERE TransactionItemId=@ItemId 
                                            AND (OriginalFileName=@FN OR FileName=@FN)
                                            AND (FilePath LIKE 'uploaded://%' OR FilePath LIKE 'http%')",
                                        new { ItemId = itemId, FN = fileName });
                                    if (uploadedVariant == null)
                                        await connection.ExecuteAsync("INSERT INTO Attachments (TransactionItemId, FileName, FilePath, OriginalFileName, FileSize, FileExtension, CreatedAt) VALUES (@ItemId, @FN, @FP, @OFN, 0, @Ext, datetime(\'now\'))", new { ItemId = itemId, FN = fileName, FP = fullUrl, OFN = fileName, Ext = System.IO.Path.GetExtension(fileName) });
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // Pull saved invoices from server
            foreach (var psi in response.SavedInvoices ?? new List<SyncSavedInvoicePull>())
            {
                try
                {
                    string savedInvNo = psi.SavedInvoiceNumber ?? "";
                    if (string.IsNullOrWhiteSpace(savedInvNo)) continue;

                    string savedKind = psi.IsBulk == 1 ? "grouped" : (psi.SavedKind ?? "single");
                    string companyName = psi.CompanyName ?? "";
                    string customerName = psi.CustomerName ?? psi.EmployeeName ?? "";
                    double totalAmount = psi.IsBulk == 1 ? psi.GrandTotalDouble : psi.TotalAmountDouble;
                    string printHtml = psi.PrintHtml ?? "";
                    string savedAt = psi.SavedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    long custId = psi.CustomerIdLong;
                    string rootInvNo = psi.RootInvoiceNumber ?? "";
                    string sourceInvNo = psi.SourceInvoiceNumber ?? psi.InvoiceNumber ?? savedInvNo;
                    string relIds = psi.RelatedIndividualIds ?? psi.TxIds ?? "";
                    string bulkRef = psi.BulkRefNumber ?? "";

                    // Use a unique key: SavedInvoiceNumber for single, or grouped prefix for bulk
                    string uniqueKey = savedKind == "grouped" ? $"G-{custId}-{savedInvNo}" : savedInvNo;
                    if (savedKind == "grouped" && !string.IsNullOrWhiteSpace(bulkRef))
                        uniqueKey = bulkRef;

                    var existingSaved = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT Id FROM SavedInvoices WHERE SavedInvoiceNumber = @Key",
                        new { Key = uniqueKey });

                    if (existingSaved == null)
                    {
                        await connection.ExecuteAsync(
                            @"INSERT INTO SavedInvoices (SavedInvoiceNumber, RootInvoiceNumber, SourceInvoiceNumber,
                                  GroupedSequenceNumber, SavedKind, TemplateKey, CustomerId, CustomerName,
                                  CompanyName, InvoiceDate, TotalAmount, Notes, PrintHtml, PayloadJson,
                                  SavedAt, RelatedIndividualIds)
                              VALUES (@Key, @Root, @Source, 0, @Kind, @Template, @CustId, @CustName,
                                  @Company, @InvDate, @Total, @Notes, @Html, @Payload, @SavedAt, @RelIds)",
                            new
                            {
                                Key = uniqueKey,
                                Root = rootInvNo != "" ? rootInvNo : bulkRef,
                                Source = sourceInvNo,
                                Kind = savedKind,
                                Template = psi.TemplateKey ?? "",
                                CustId = custId,
                                CustName = customerName,
                                Company = companyName,
                                InvDate = psi.InvoiceDate ?? savedAt,
                                Total = totalAmount,
                                Notes = psi.Notes ?? "",
                                Html = printHtml,
                                Payload = psi.PayloadJson ?? "",
                                SavedAt = savedAt,
                                RelIds = relIds
                            });
                    }
                    else
                    {
                        // Update if server has newer HTML
                        if (!string.IsNullOrWhiteSpace(printHtml))
                        {
                            await connection.ExecuteAsync(
                                @"UPDATE SavedInvoices SET PrintHtml=@Html, TotalAmount=@Total, CompanyName=@Company,
                                      CustomerName=@CustName, SavedAt=@SavedAt WHERE Id=@Id",
                                new { Html = printHtml, Total = totalAmount, Company = companyName,
                                      CustName = customerName, SavedAt = savedAt, Id = (long)existingSaved.Id });
                        }
                    }
                }
                catch { }
            }
        }

        private async Task DeduplicateAndFixSequenceAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Deduplicate: if two transactions share the same InvoiceNumber, keep the one with ServerId > 0 or the newer one
            var dupes = await connection.QueryAsync("SELECT InvoiceNumber, COUNT(*) as cnt FROM Transactions WHERE InvoiceNumber IS NOT NULL AND TRIM(InvoiceNumber) != '' GROUP BY InvoiceNumber HAVING cnt > 1");
            foreach (var d in dupes)
            {
                string invNum = d.InvoiceNumber?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(invNum)) continue;
                var rows = (await connection.QueryAsync("SELECT Id, ServerId, CreatedAt FROM Transactions WHERE InvoiceNumber = @Inv ORDER BY CASE WHEN ServerId > 0 THEN 0 ELSE 1 END, CreatedAt DESC", new { Inv = invNum })).ToList();
                for (int i = 1; i < rows.Count; i++)
                {
                    long delId = (long)rows[i].Id;
                    await connection.ExecuteAsync("DELETE FROM TransactionItems WHERE TransactionId = @Id", new { Id = delId });
                    await connection.ExecuteAsync("DELETE FROM Transactions WHERE Id = @Id", new { Id = delId });
                }
            }

            // Auto-update invoice sequence: next invoice = MAX(all invoice numbers) + 1
            var maxInv = await connection.QueryFirstOrDefaultAsync<long?>("SELECT MAX(CAST(InvoiceNumber AS INTEGER)) FROM Transactions WHERE InvoiceNumber IS NOT NULL AND InvoiceNumber != ''") ?? 0;
            if (maxInv >= 1000)
            {
                var nextInv = maxInv + 1;
                var stateJson = new Dictionary<string, string>();
                if (File.Exists(_syncStatePath))
                {
                    try { stateJson = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_syncStatePath)) ?? new(); } catch { }
                }
                stateJson["next_invoice_number"] = nextInv.ToString();
                stateJson["last_sync"] = stateJson.GetValueOrDefault("last_sync", "2000-01-01 00:00:00");
                stateJson["device_id"] = _deviceId;
                File.WriteAllText(_syncStatePath, JsonSerializer.Serialize(stateJson));
            }
        }


        private async Task UploadPendingAttachmentsAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var pendingAttachments = await connection.QueryAsync(
                    @"SELECT a.Id, a.FilePath, a.OriginalFileName, ti.SyncUuid AS ItemSyncUuid, t.InvoiceNumber,
                       (SELECT COUNT(*) FROM TransactionItems ti2 WHERE ti2.TransactionId = ti.TransactionId AND ti2.Id <= ti.Id) AS LineIndex
                     FROM Attachments a
                     JOIN TransactionItems ti ON ti.Id = a.TransactionItemId
                     JOIN Transactions t ON t.Id = ti.TransactionId
                     WHERE a.FilePath NOT LIKE 'http%' AND a.FilePath NOT LIKE 'uploaded://%' AND a.FilePath != ''
                       AND ti.SyncUuid IS NOT NULL AND ti.SyncUuid != ''
                       AND t.InvoiceNumber IS NOT NULL AND t.InvoiceNumber != ''");

                foreach (var att in pendingAttachments)
                {
                    string filePath = att.FilePath?.ToString() ?? "";
                    string itemSyncUuid = att.ItemSyncUuid?.ToString() ?? "";
                    string invoiceNumber = att.InvoiceNumber?.ToString() ?? "";
                    int lineIndex = att.LineIndex != null ? (int)att.LineIndex : 1;

                    if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath)) continue;
                    if (string.IsNullOrWhiteSpace(itemSyncUuid)) continue;

                    OnProgress?.Invoke($"جاري رفع مرفق: {att.OriginalFileName}...");
                    bool ok = await _api.UploadAttachmentAsync(filePath, itemSyncUuid, invoiceNumber, lineIndex);
                    if (ok)
                    {
                        await connection.ExecuteAsync("UPDATE Attachments SET FilePath = @NewPath WHERE Id = @Id",
                            new { NewPath = "uploaded://" + filePath, Id = (long)att.Id });
                    }
                }
            }
            catch (Exception ex)
            {
                OnProgress?.Invoke($"تحذير: فشل رفع بعض المرفقات: {ex.Message}");
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
