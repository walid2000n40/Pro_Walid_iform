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

            await connection.ExecuteAsync(createTransactionsTable);
            await EnsureTransactionsCustomerIdColumnAsync(connection);
            await EnsureTransactionsStatusColumnAsync(connection);
            await connection.ExecuteAsync(createItemsTable);
            await EnsureTransactionItemsGovFeesColumnAsync(connection);
            await connection.ExecuteAsync(createAttachmentsTable);
            await connection.ExecuteAsync(createCustomersTable);
            await EnsureCustomersCustomerNumberColumnAsync(connection);
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
                        @"INSERT INTO Transactions (CustomerId, TransactionStatus, InvoiceNumber, CompanyName, EmployeeName, TransactionDate, GrandTotal) 
                          VALUES (@CustomerId, @TransactionStatus, @InvoiceNumber, @CompanyName, @EmployeeName, @TransactionDate, @GrandTotal);
                          SELECT last_insert_rowid();",
                        new
                        {
                            transaction.CustomerId,
                            transaction.TransactionStatus,
                            transaction.InvoiceNumber,
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

        public async Task<long> GetNextCustomerNumberAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var lastCustomerNumber = await connection.QueryFirstOrDefaultAsync<long?>(
                "SELECT MAX(CASE WHEN CustomerNumber > 0 THEN CustomerNumber ELSE Id END) FROM Customers");

            return (lastCustomerNumber ?? 0) + 1;
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
