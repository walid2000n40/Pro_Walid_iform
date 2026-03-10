using Dapper;
using Microsoft.Data.Sqlite;
using ProWalid.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProWalid.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public DatabaseHelper()
        {
            var appFolder = AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = Path.Combine(appFolder, "ProWalid.db");
            _connectionString = $"Data Source={_dbPath}";
            
            InitializeDatabaseAsync().Wait();
        }

        private async Task InitializeDatabaseAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var createTransactionsTable = @"
                CREATE TABLE IF NOT EXISTS Transactions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerId INTEGER NOT NULL DEFAULT 0,
                    TransactionStatus TEXT NOT NULL DEFAULT 'معلق',
                    InvoiceNumber TEXT NOT NULL UNIQUE,
                    InvoiceTemplateKey TEXT NOT NULL DEFAULT '',
                    CompanyName TEXT NOT NULL,
                    EmployeeName TEXT NOT NULL,
                    TransactionDate TEXT NOT NULL,
                    GrandTotal REAL NOT NULL,
                    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )";

            var createItemsTable = @"
                CREATE TABLE IF NOT EXISTS TransactionItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TransactionId INTEGER NOT NULL,
                    ServiceName TEXT NOT NULL,
                    Quantity REAL NOT NULL,
                    UnitPrice REAL NOT NULL,
                    Profit REAL NOT NULL DEFAULT 0,
                    GovFees TEXT NOT NULL DEFAULT '',
                    AttachmentPath TEXT,
                    FOREIGN KEY (TransactionId) REFERENCES Transactions(Id) ON DELETE CASCADE
                )";

            var createAttachmentsTable = @"
                CREATE TABLE IF NOT EXISTS Attachments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TransactionItemId INTEGER NOT NULL,
                    FileName TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    OriginalFileName TEXT NOT NULL,
                    FileSize INTEGER NOT NULL,
                    FileExtension TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (TransactionItemId) REFERENCES TransactionItems(Id) ON DELETE CASCADE
                )";

            var createCustomersTable = @"
                CREATE TABLE IF NOT EXISTS Customers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerNumber INTEGER NOT NULL DEFAULT 0,
                    Name TEXT NOT NULL,
                    Phone TEXT,
                    Email TEXT,
                    Address TEXT,
                    Notes TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )";

            var createSavedInvoicesTable = @"
                CREATE TABLE IF NOT EXISTS SavedInvoices (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SavedInvoiceNumber TEXT NOT NULL UNIQUE,
                    RootInvoiceNumber TEXT NOT NULL DEFAULT '',
                    SourceInvoiceNumber TEXT NOT NULL DEFAULT '',
                    GroupedSequenceNumber INTEGER NOT NULL DEFAULT 0,
                    SavedKind TEXT NOT NULL DEFAULT 'single',
                    TemplateKey TEXT NOT NULL DEFAULT '',
                    CustomerId INTEGER NOT NULL DEFAULT 0,
                    CustomerName TEXT NOT NULL DEFAULT '',
                    CompanyName TEXT NOT NULL DEFAULT '',
                    InvoiceDate TEXT NOT NULL,
                    TotalAmount REAL NOT NULL DEFAULT 0,
                    Notes TEXT NOT NULL DEFAULT '',
                    PrintHtml TEXT NOT NULL DEFAULT '',
                    PayloadJson TEXT NOT NULL DEFAULT '',
                    SavedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )";

            var createCompanySuggestionsTable = @"
                CREATE TABLE IF NOT EXISTS Suggestions_Companies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Value TEXT NOT NULL,
                    NormalizedValue TEXT NOT NULL UNIQUE,
                    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )";

            var createEmployeeSuggestionsTable = @"
                CREATE TABLE IF NOT EXISTS Suggestions_Employees (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Value TEXT NOT NULL,
                    NormalizedValue TEXT NOT NULL UNIQUE,
                    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )";

            var createItemSuggestionsTable = @"
                CREATE TABLE IF NOT EXISTS Suggestions_Items (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Value TEXT NOT NULL,
                    NormalizedValue TEXT NOT NULL UNIQUE,
                    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )";

            await connection.ExecuteAsync(createTransactionsTable);
            await EnsureTransactionsCustomerIdColumnAsync(connection);
            await EnsureTransactionsStatusColumnAsync(connection);
            await EnsureTransactionsInvoiceTemplateKeyColumnAsync(connection);
            await connection.ExecuteAsync(createItemsTable);
            await EnsureTransactionItemsGovFeesColumnAsync(connection);
            await connection.ExecuteAsync(createAttachmentsTable);
            await connection.ExecuteAsync(createCustomersTable);
            await EnsureCustomersCustomerNumberColumnAsync(connection);
            await connection.ExecuteAsync(createSavedInvoicesTable);
            await connection.ExecuteAsync(createCompanySuggestionsTable);
            await connection.ExecuteAsync(createEmployeeSuggestionsTable);
            await connection.ExecuteAsync(createItemSuggestionsTable);
            await BackfillHazemInvoiceTemplateKeysAsync(connection);
            await BackfillSuggestionTablesAsync(connection);
        }

        private static async Task EnsureTransactionsCustomerIdColumnAsync(SqliteConnection connection)
        {
            var transactionColumns = await connection.QueryAsync("PRAGMA table_info(Transactions)");
            if (!transactionColumns.Any(column => string.Equals((string)column.name, "CustomerId", StringComparison.OrdinalIgnoreCase)))
            {
                await connection.ExecuteAsync("ALTER TABLE Transactions ADD COLUMN CustomerId INTEGER NOT NULL DEFAULT 0");
            }
        }

        private static async Task EnsureTransactionsStatusColumnAsync(SqliteConnection connection)
        {
            var transactionColumns = await connection.QueryAsync("PRAGMA table_info(Transactions)");
            if (!transactionColumns.Any(column => string.Equals((string)column.name, "TransactionStatus", StringComparison.OrdinalIgnoreCase)))
            {
                await connection.ExecuteAsync("ALTER TABLE Transactions ADD COLUMN TransactionStatus TEXT NOT NULL DEFAULT 'معلق'");
            }
        }

        private static async Task EnsureTransactionsInvoiceTemplateKeyColumnAsync(SqliteConnection connection)
        {
            var transactionColumns = await connection.QueryAsync("PRAGMA table_info(Transactions)");
            if (!transactionColumns.Any(column => string.Equals((string)column.name, "InvoiceTemplateKey", StringComparison.OrdinalIgnoreCase)))
            {
                await connection.ExecuteAsync("ALTER TABLE Transactions ADD COLUMN InvoiceTemplateKey TEXT NOT NULL DEFAULT ''");
            }
        }

        private static async Task BackfillHazemInvoiceTemplateKeysAsync(SqliteConnection connection)
        {
            await connection.ExecuteAsync(@"
                UPDATE Transactions
                SET InvoiceTemplateKey = 'hazem'
                WHERE (InvoiceTemplateKey IS NULL OR TRIM(InvoiceTemplateKey) = '')
                  AND CustomerId IN (
                      SELECT Id
                      FROM Customers
                      WHERE Name LIKE '%حازم%'
                         OR LOWER(Name) LIKE '%hazem%'
                  )");
        }

        private static async Task EnsureTransactionItemsGovFeesColumnAsync(SqliteConnection connection)
        {
            var itemColumns = await connection.QueryAsync("PRAGMA table_info(TransactionItems)");
            if (!itemColumns.Any(column => string.Equals((string)column.name, "GovFees", StringComparison.OrdinalIgnoreCase)))
            {
                await connection.ExecuteAsync("ALTER TABLE TransactionItems ADD COLUMN GovFees TEXT NOT NULL DEFAULT ''");

                if (itemColumns.Any(column => string.Equals((string)column.name, "Discount", StringComparison.OrdinalIgnoreCase)))
                {
                    await connection.ExecuteAsync(@"UPDATE TransactionItems
                                                    SET GovFees = CASE
                                                        WHEN Discount IS NULL OR Discount = 0 THEN ''
                                                        ELSE CAST(Discount AS TEXT)
                                                    END
                                                    WHERE GovFees = '' OR GovFees IS NULL");
                }
            }
        }

        private static async Task EnsureCustomersCustomerNumberColumnAsync(SqliteConnection connection)
        {
            var customerColumns = await connection.QueryAsync("PRAGMA table_info(Customers)");
            if (!customerColumns.Any(column => string.Equals((string)column.name, "CustomerNumber", StringComparison.OrdinalIgnoreCase)))
            {
                await connection.ExecuteAsync("ALTER TABLE Customers ADD COLUMN CustomerNumber INTEGER NOT NULL DEFAULT 0");
                await connection.ExecuteAsync("UPDATE Customers SET CustomerNumber = Id WHERE CustomerNumber = 0 OR CustomerNumber IS NULL");
            }
        }

        private static string NormalizeSuggestionValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(" ", value
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim()
                .ToLowerInvariant();
        }

        private static string CleanSuggestionValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(" ", value
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();
        }

        private static async Task BackfillSuggestionTablesAsync(SqliteConnection connection)
        {
            var companyNames = await connection.QueryAsync<string>(@"
                SELECT DISTINCT CompanyName
                FROM Transactions
                WHERE CompanyName IS NOT NULL AND TRIM(CompanyName) <> ''");

            foreach (var companyName in companyNames)
            {
                await InsertSuggestionIfNeededAsync(connection, null, "Suggestions_Companies", companyName);
            }

            var employeeNames = await connection.QueryAsync<string>(@"
                SELECT DISTINCT EmployeeName
                FROM Transactions
                WHERE EmployeeName IS NOT NULL AND TRIM(EmployeeName) <> ''");

            foreach (var employeeName in employeeNames)
            {
                await InsertSuggestionIfNeededAsync(connection, null, "Suggestions_Employees", employeeName);
            }

            var itemDescriptions = await connection.QueryAsync<string>(@"
                SELECT DISTINCT ServiceName
                FROM TransactionItems
                WHERE ServiceName IS NOT NULL AND TRIM(ServiceName) <> ''");

            foreach (var itemDescription in itemDescriptions)
            {
                await InsertSuggestionIfNeededAsync(connection, null, "Suggestions_Items", itemDescription);
            }
        }

        private static async Task InsertSuggestionIfNeededAsync(
            SqliteConnection connection,
            SqliteTransaction? transaction,
            string tableName,
            string? rawValue)
        {
            var cleanedValue = CleanSuggestionValue(rawValue);
            var normalizedValue = NormalizeSuggestionValue(cleanedValue);

            if (string.IsNullOrWhiteSpace(cleanedValue) || string.IsNullOrWhiteSpace(normalizedValue))
            {
                return;
            }

            await connection.ExecuteAsync(
                $@"INSERT OR IGNORE INTO {tableName} (Value, NormalizedValue)
                   VALUES (@Value, @NormalizedValue)",
                new
                {
                    Value = cleanedValue,
                    NormalizedValue = normalizedValue
                },
                transaction);
        }

        private static async Task SaveSuggestionsForTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, Transaction transactionModel)
        {
            await InsertSuggestionIfNeededAsync(connection, transaction, "Suggestions_Companies", transactionModel.CompanyName);
            await InsertSuggestionIfNeededAsync(connection, transaction, "Suggestions_Employees", transactionModel.EmployeeName);

            foreach (var item in transactionModel.Items)
            {
                await InsertSuggestionIfNeededAsync(connection, transaction, "Suggestions_Items", item.ServiceName);
            }
        }

        public async Task<long> SaveTransactionAsync(Transaction transaction)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var dbTransaction = connection.BeginTransaction();
            try
            {
                var existingId = await connection.QueryFirstOrDefaultAsync<long?>(
                    "SELECT Id FROM Transactions WHERE InvoiceNumber = @InvoiceNumber",
                    new { transaction.InvoiceNumber },
                    dbTransaction);

                long transactionId;

                if (existingId.HasValue)
                {
                    await connection.ExecuteAsync(
                        @"UPDATE Transactions 
                          SET CustomerId = @CustomerId,
                              TransactionStatus = @TransactionStatus,
                              InvoiceTemplateKey = @InvoiceTemplateKey,
                              CompanyName = @CompanyName, 
                              EmployeeName = @EmployeeName, 
                              TransactionDate = @TransactionDate, 
                              GrandTotal = @GrandTotal 
                          WHERE Id = @Id",
                        new
                        {
                            Id = existingId.Value,
                            transaction.CustomerId,
                            transaction.TransactionStatus,
                            transaction.InvoiceTemplateKey,
                            transaction.CompanyName,
                            transaction.EmployeeName,
                            TransactionDate = transaction.TransactionDate.ToString("yyyy-MM-dd"),
                            transaction.GrandTotal
                        },
                        dbTransaction);

                    var existingItemIds = await connection.QueryAsync<long>(
                        "SELECT Id FROM TransactionItems WHERE TransactionId = @TransactionId",
                        new { TransactionId = existingId.Value },
                        dbTransaction);

                    var itemIdsToKeep = transaction.Items.Where(i => i.Id > 0).Select(i => i.Id).ToList();
                    var itemIdsToDelete = existingItemIds.Where(id => !itemIdsToKeep.Contains(id)).ToList();

                    foreach (var itemId in itemIdsToDelete)
                    {
                        await connection.ExecuteAsync(
                            "DELETE FROM Attachments WHERE TransactionItemId = @ItemId",
                            new { ItemId = itemId },
                            dbTransaction);
                        
                        await connection.ExecuteAsync(
                            "DELETE FROM TransactionItems WHERE Id = @ItemId",
                            new { ItemId = itemId },
                            dbTransaction);
                    }

                    transactionId = existingId.Value;
                }
                else
                {
                    transactionId = await connection.QuerySingleAsync<long>(
                        @"INSERT INTO Transactions (CustomerId, TransactionStatus, InvoiceNumber, InvoiceTemplateKey, CompanyName, EmployeeName, TransactionDate, GrandTotal) 
                          VALUES (@CustomerId, @TransactionStatus, @InvoiceNumber, @InvoiceTemplateKey, @CompanyName, @EmployeeName, @TransactionDate, @GrandTotal);
                          SELECT last_insert_rowid();",
                        new
                        {
                            transaction.CustomerId,
                            transaction.TransactionStatus,
                            transaction.InvoiceNumber,
                            transaction.InvoiceTemplateKey,
                            transaction.CompanyName,
                            transaction.EmployeeName,
                            TransactionDate = transaction.TransactionDate.ToString("yyyy-MM-dd"),
                            transaction.GrandTotal
                        },
                        dbTransaction);
                }

                foreach (var item in transaction.Items)
                {
                    long itemId;
                    
                    if (item.Id > 0)
                    {
                        await connection.ExecuteAsync(
                            @"UPDATE TransactionItems 
                              SET ServiceName = @ServiceName, 
                                  Quantity = @Quantity, 
                                  UnitPrice = @UnitPrice, 
                                  Profit = @Profit, 
                                  GovFees = @GovFees, 
                                  AttachmentPath = @AttachmentPath 
                              WHERE Id = @Id",
                            new
                            {
                                item.Id,
                                item.ServiceName,
                                item.Quantity,
                                item.UnitPrice,
                                item.Profit,
                                item.GovFees,
                                item.AttachmentPath
                            },
                            dbTransaction);
                        
                        itemId = item.Id;
                    }
                    else
                    {
                        itemId = await connection.QuerySingleAsync<long>(
                            @"INSERT INTO TransactionItems (TransactionId, ServiceName, Quantity, UnitPrice, Profit, GovFees, AttachmentPath) 
                              VALUES (@TransactionId, @ServiceName, @Quantity, @UnitPrice, @Profit, @GovFees, @AttachmentPath);
                              SELECT last_insert_rowid();",
                            new
                            {
                                TransactionId = transactionId,
                                item.ServiceName,
                                item.Quantity,
                                item.UnitPrice,
                                item.Profit,
                                item.GovFees,
                                item.AttachmentPath
                            },
                            dbTransaction);
                        
                        item.Id = itemId;
                    }
                    
                    var newAttachments = item.Attachments.Where(a => a.Id == 0).ToList();
                    if (newAttachments.Count > 0)
                    {
                        foreach (var attachment in newAttachments)
                        {
                            await connection.ExecuteAsync(
                                @"INSERT INTO Attachments (TransactionItemId, FileName, FilePath, OriginalFileName, FileSize, FileExtension) 
                                  VALUES (@TransactionItemId, @FileName, @FilePath, @OriginalFileName, @FileSize, @FileExtension)",
                                new
                                {
                                    TransactionItemId = itemId,
                                    attachment.FileName,
                                    attachment.FilePath,
                                    attachment.OriginalFileName,
                                    attachment.FileSize,
                                    attachment.FileExtension
                                },
                                dbTransaction);
                        }
                    }
                }

                await SaveSuggestionsForTransactionAsync(connection, dbTransaction, transaction);

                dbTransaction.Commit();
                return transactionId;
            }
            catch
            {
                dbTransaction.Rollback();
                throw;
            }
        }

        public async Task<List<Transaction>> GetAllTransactionsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var transactionDict = new Dictionary<long, Transaction>();
            var itemDict = new Dictionary<long, TransactionItemDetail>();

            await connection.QueryAsync<dynamic, dynamic, Transaction>(
                @"SELECT t.*, ti.* 
                  FROM Transactions t 
                  LEFT JOIN TransactionItems ti ON t.Id = ti.TransactionId 
                  ORDER BY t.TransactionDate DESC, t.Id DESC",
                (trans, item) =>
                {
                    if (!transactionDict.TryGetValue((long)trans.Id, out var transaction))
                    {
                        transaction = new Transaction
                        {
                            CustomerId = trans.CustomerId != null ? (long)trans.CustomerId : 0,
                            TransactionStatus = trans.TransactionStatus != null ? (string)trans.TransactionStatus : "معلق",
                            InvoiceNumber = trans.InvoiceNumber,
                            InvoiceTemplateKey = trans.InvoiceTemplateKey != null ? (string)trans.InvoiceTemplateKey : string.Empty,
                            CompanyName = trans.CompanyName,
                            EmployeeName = trans.EmployeeName,
                            TransactionDate = DateTimeOffset.Parse((string)trans.TransactionDate)
                        };
                        transactionDict.Add((long)trans.Id, transaction);
                    }

                    if (item != null && item.Id != null)
                    {
                        var itemId = (long)item.Id;
                        if (!itemDict.ContainsKey(itemId))
                        {
                            var transactionItem = new TransactionItemDetail
                            {
                                Id = itemId,
                                ServiceName = item.ServiceName ?? string.Empty,
                                Quantity = (double)item.Quantity,
                                UnitPrice = (double)item.UnitPrice,
                                Profit = (double)item.Profit,
                                GovFees = item.GovFees ?? string.Empty,
                                AttachmentPath = item.AttachmentPath ?? string.Empty
                            };
                            transaction.Items.Add(transactionItem);
                            itemDict.Add(itemId, transactionItem);
                        }
                    }

                    return transaction;
                },
                splitOn: "Id");

            foreach (var item in itemDict.Values)
            {
                var attachments = await GetAttachmentsAsync(item.Id);
                foreach (var attachment in attachments)
                {
                    item.Attachments.Add(attachment);
                }
            }

            return transactionDict.Values.ToList();
        }

        public async Task<bool> DeleteTransactionAsync(string invoiceNumber)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var rowsAffected = await connection.ExecuteAsync(
                "DELETE FROM Transactions WHERE InvoiceNumber = @InvoiceNumber",
                new { InvoiceNumber = invoiceNumber });

            return rowsAffected > 0;
        }

        public async Task<string> GetNextInvoiceNumberAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var invoiceNumbers = await connection.QueryAsync<string>(
                "SELECT InvoiceNumber FROM Transactions WHERE InvoiceNumber IS NOT NULL AND TRIM(InvoiceNumber) <> ''");

            const int seedInvoiceNumber = 870;

            var maxExistingInvoiceNumber = invoiceNumbers
                .Select(number => int.TryParse(number, out var parsedNumber) ? parsedNumber : 0)
                .DefaultIfEmpty(0)
                .Max();

            var nextInvoiceNumber = Math.Max(seedInvoiceNumber, maxExistingInvoiceNumber + 1);
            return nextInvoiceNumber.ToString("D5");
        }

        public async Task SaveAttachmentsAsync(long transactionItemId, List<Attachment> attachments)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM Attachments WHERE TransactionItemId = @TransactionItemId",
                new { TransactionItemId = transactionItemId });

            foreach (var attachment in attachments)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO Attachments (TransactionItemId, FileName, FilePath, OriginalFileName, FileSize, FileExtension) 
                      VALUES (@TransactionItemId, @FileName, @FilePath, @OriginalFileName, @FileSize, @FileExtension)",
                    new
                    {
                        TransactionItemId = transactionItemId,
                        attachment.FileName,
                        attachment.FilePath,
                        attachment.OriginalFileName,
                        attachment.FileSize,
                        attachment.FileExtension
                    });
            }
        }

        public async Task<List<Attachment>> GetAttachmentsAsync(long transactionItemId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var attachments = await connection.QueryAsync<Attachment>(
                "SELECT * FROM Attachments WHERE TransactionItemId = @TransactionItemId ORDER BY Id",
                new { TransactionItemId = transactionItemId });

            return attachments.ToList();
        }

        public async Task DeleteAttachmentAsync(long attachmentId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM Attachments WHERE Id = @Id",
                new { Id = attachmentId });
        }

        public async Task DeleteAttachmentsByTransactionItemIdsAsync(IEnumerable<long> transactionItemIds)
        {
            var itemIds = transactionItemIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (itemIds.Count == 0)
            {
                return;
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM Attachments WHERE TransactionItemId IN @ItemIds",
                new { ItemIds = itemIds });
        }

        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var customers = await connection.QueryAsync<Customer>(
                "SELECT * FROM Customers ORDER BY CASE WHEN CustomerNumber > 0 THEN CustomerNumber ELSE Id END, Name");

            return customers.ToList();
        }

        public async Task<int> GetSavedInvoicesCountAsync(long? customerId = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var count = await connection.QueryFirstOrDefaultAsync<long>(
                @"SELECT COUNT(1)
                  FROM SavedInvoices
                  WHERE (@CustomerId IS NULL OR CustomerId = @CustomerId)",
                new { CustomerId = customerId });

            return (int)count;
        }

        public async Task<int> GetNextGroupedSavedInvoiceSequenceAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var maxSequence = await connection.QueryFirstOrDefaultAsync<int?>(
                @"SELECT MAX(GroupedSequenceNumber)
                  FROM SavedInvoices
                  WHERE GroupedSequenceNumber > 0");

            const int groupedSeed = 309;
            return maxSequence.HasValue && maxSequence.Value >= groupedSeed
                ? maxSequence.Value + 1
                : groupedSeed;
        }

        public async Task<List<SavedInvoiceRecord>> GetAllSavedInvoicesAsync(long? customerId = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var rows = (await connection.QueryAsync(
                @"SELECT Id,
                         SavedInvoiceNumber,
                         RootInvoiceNumber,
                         SourceInvoiceNumber,
                         GroupedSequenceNumber,
                         SavedKind,
                         TemplateKey,
                         CustomerId,
                         CustomerName,
                         CompanyName,
                         InvoiceDate AS InvoiceDateText,
                         TotalAmount,
                         Notes,
                         PrintHtml,
                         PayloadJson,
                         SavedAt
                  FROM SavedInvoices
                  WHERE (@CustomerId IS NULL OR CustomerId = @CustomerId)
                  ORDER BY datetime(SavedAt) DESC, Id DESC",
                new { CustomerId = customerId })).ToList();

            var records = rows
                .Select(row => new SavedInvoiceRecord
                {
                    Id = row.Id != null ? (long)row.Id : 0,
                    SavedInvoiceNumber = row.SavedInvoiceNumber?.ToString() ?? string.Empty,
                    RootInvoiceNumber = row.RootInvoiceNumber?.ToString() ?? string.Empty,
                    SourceInvoiceNumber = row.SourceInvoiceNumber?.ToString() ?? string.Empty,
                    GroupedSequenceNumber = row.GroupedSequenceNumber != null ? (int)row.GroupedSequenceNumber : 0,
                    SavedKind = row.SavedKind?.ToString() ?? "single",
                    TemplateKey = row.TemplateKey?.ToString() ?? string.Empty,
                    CustomerId = row.CustomerId != null ? (long)row.CustomerId : 0,
                    CustomerName = row.CustomerName?.ToString() ?? string.Empty,
                    CompanyName = row.CompanyName?.ToString() ?? string.Empty,
                    InvoiceDateText = row.InvoiceDateText?.ToString() ?? string.Empty,
                    TotalAmount = row.TotalAmount != null ? (double)row.TotalAmount : 0,
                    Notes = row.Notes?.ToString() ?? string.Empty,
                    PrintHtml = row.PrintHtml?.ToString() ?? string.Empty,
                    PayloadJson = row.PayloadJson?.ToString() ?? string.Empty,
                    SavedAt = row.SavedAt != null
                        ? DateTimeOffset.TryParse(row.SavedAt.ToString(), out DateTimeOffset parsedSavedAt)
                            ? parsedSavedAt
                            : DateTimeOffset.Now
                        : DateTimeOffset.Now
                })
                .ToList();

            for (var index = 0; index < records.Count; index++)
            {
                records[index].SerialNumber = index + 1;
            }

            return records;
        }

        public async Task SaveSavedInvoiceRecordsAsync(IEnumerable<SavedInvoiceRecord> records)
        {
            var recordList = records
                .Where(record => record != null && !string.IsNullOrWhiteSpace(record.SavedInvoiceNumber))
                .ToList();

            if (recordList.Count == 0)
            {
                return;
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var dbTransaction = connection.BeginTransaction();
            try
            {
                foreach (var record in recordList)
                {
                    var existingId = await connection.QueryFirstOrDefaultAsync<long?>(
                        "SELECT Id FROM SavedInvoices WHERE SavedInvoiceNumber = @SavedInvoiceNumber",
                        new { record.SavedInvoiceNumber },
                        dbTransaction);

                    if (existingId.HasValue)
                    {
                        await connection.ExecuteAsync(
                            @"UPDATE SavedInvoices
                              SET RootInvoiceNumber = @RootInvoiceNumber,
                                  SourceInvoiceNumber = @SourceInvoiceNumber,
                                  GroupedSequenceNumber = @GroupedSequenceNumber,
                                  SavedKind = @SavedKind,
                                  TemplateKey = @TemplateKey,
                                  CustomerId = @CustomerId,
                                  CustomerName = @CustomerName,
                                  CompanyName = @CompanyName,
                                  InvoiceDate = @InvoiceDateText,
                                  TotalAmount = @TotalAmount,
                                  Notes = @Notes,
                                  PrintHtml = @PrintHtml,
                                  PayloadJson = @PayloadJson,
                                  SavedAt = @SavedAt
                              WHERE Id = @Id",
                            new
                            {
                                Id = existingId.Value,
                                record.RootInvoiceNumber,
                                record.SourceInvoiceNumber,
                                record.GroupedSequenceNumber,
                                record.SavedKind,
                                record.TemplateKey,
                                record.CustomerId,
                                record.CustomerName,
                                record.CompanyName,
                                record.InvoiceDateText,
                                record.TotalAmount,
                                record.Notes,
                                record.PrintHtml,
                                record.PayloadJson,
                                SavedAt = record.SavedAt.ToString("yyyy-MM-dd HH:mm:ss")
                            },
                            dbTransaction);
                    }
                    else
                    {
                        await connection.ExecuteAsync(
                            @"INSERT INTO SavedInvoices (
                                  SavedInvoiceNumber,
                                  RootInvoiceNumber,
                                  SourceInvoiceNumber,
                                  GroupedSequenceNumber,
                                  SavedKind,
                                  TemplateKey,
                                  CustomerId,
                                  CustomerName,
                                  CompanyName,
                                  InvoiceDate,
                                  TotalAmount,
                                  Notes,
                                  PrintHtml,
                                  PayloadJson,
                                  SavedAt)
                              VALUES (
                                  @SavedInvoiceNumber,
                                  @RootInvoiceNumber,
                                  @SourceInvoiceNumber,
                                  @GroupedSequenceNumber,
                                  @SavedKind,
                                  @TemplateKey,
                                  @CustomerId,
                                  @CustomerName,
                                  @CompanyName,
                                  @InvoiceDateText,
                                  @TotalAmount,
                                  @Notes,
                                  @PrintHtml,
                                  @PayloadJson,
                                  @SavedAt)",
                            new
                            {
                                record.SavedInvoiceNumber,
                                record.RootInvoiceNumber,
                                record.SourceInvoiceNumber,
                                record.GroupedSequenceNumber,
                                record.SavedKind,
                                record.TemplateKey,
                                record.CustomerId,
                                record.CustomerName,
                                record.CompanyName,
                                record.InvoiceDateText,
                                record.TotalAmount,
                                record.Notes,
                                record.PrintHtml,
                                record.PayloadJson,
                                SavedAt = record.SavedAt.ToString("yyyy-MM-dd HH:mm:ss")
                            },
                            dbTransaction);
                    }
                }

                dbTransaction.Commit();
            }
            catch
            {
                dbTransaction.Rollback();
                throw;
            }
        }

        public async Task<long> GetNextCustomerNumberAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var lastCustomerNumber = await connection.QueryFirstOrDefaultAsync<long?>(
                "SELECT MAX(CASE WHEN CustomerNumber > 0 THEN CustomerNumber ELSE Id END) FROM Customers");

            return (lastCustomerNumber ?? 0) + 1;
        }

        private async Task<List<SuggestionEntry>> GetSuggestionsAsync(string tableName, string suggestionType, string? searchText, int limit = 8)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cleanedSearchText = CleanSuggestionValue(searchText);
            if (string.IsNullOrWhiteSpace(cleanedSearchText))
            {
                return new List<SuggestionEntry>();
            }

            var suggestions = await connection.QueryAsync<string>(
                $@"SELECT Value
                   FROM {tableName}
                   WHERE Value LIKE @StartsWith COLLATE NOCASE
                      OR Value LIKE @Contains COLLATE NOCASE
                   ORDER BY CASE WHEN Value LIKE @StartsWith COLLATE NOCASE THEN 0 ELSE 1 END,
                            length(Value),
                            Value
                   LIMIT @Limit",
                new
                {
                    StartsWith = cleanedSearchText + "%",
                    Contains = "%" + cleanedSearchText + "%",
                    Limit = limit
                });

            return suggestions
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(value => new SuggestionEntry
                {
                    Value = value,
                    SuggestionType = suggestionType
                })
                .ToList();
        }

        private async Task DeleteSuggestionAsync(string tableName, string? value)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var normalizedValue = NormalizeSuggestionValue(value);
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return;
            }

            await connection.ExecuteAsync(
                $@"DELETE FROM {tableName}
                   WHERE NormalizedValue = @NormalizedValue",
                new { NormalizedValue = normalizedValue });
        }

        public Task<List<SuggestionEntry>> GetCompanySuggestionsAsync(string? searchText)
        {
            return GetSuggestionsAsync("Suggestions_Companies", "company", searchText);
        }

        public Task<List<SuggestionEntry>> GetEmployeeSuggestionsAsync(string? searchText)
        {
            return GetSuggestionsAsync("Suggestions_Employees", "employee", searchText);
        }

        public Task<List<SuggestionEntry>> GetItemSuggestionsAsync(string? searchText)
        {
            return GetSuggestionsAsync("Suggestions_Items", "item", searchText);
        }

        public Task DeleteCompanySuggestionAsync(string? value)
        {
            return DeleteSuggestionAsync("Suggestions_Companies", value);
        }

        public Task DeleteEmployeeSuggestionAsync(string? value)
        {
            return DeleteSuggestionAsync("Suggestions_Employees", value);
        }

        public Task DeleteItemSuggestionAsync(string? value)
        {
            return DeleteSuggestionAsync("Suggestions_Items", value);
        }

        public async Task<long> SaveCustomerAsync(Customer customer)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            if (customer.Id > 0)
            {
                await connection.ExecuteAsync(
                    @"UPDATE Customers 
                      SET CustomerNumber = @CustomerNumber,
                          Name = @Name, Phone = @Phone, Email = @Email, 
                          Address = @Address, Notes = @Notes 
                      WHERE Id = @Id",
                    customer);
                return customer.Id;
            }
            else
            {
                return await connection.QuerySingleAsync<long>(
                    @"INSERT INTO Customers (CustomerNumber, Name, Phone, Email, Address, Notes) 
                      VALUES (@CustomerNumber, @Name, @Phone, @Email, @Address, @Notes);
                      SELECT last_insert_rowid();",
                    customer);
            }
        }

        public async Task DeleteCustomerAsync(long customerId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM Customers WHERE Id = @Id",
                new { Id = customerId });
        }
    }
}
