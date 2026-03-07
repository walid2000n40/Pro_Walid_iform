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
                    Discount REAL NOT NULL DEFAULT 0,
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

            await connection.ExecuteAsync(createTransactionsTable);
            await connection.ExecuteAsync(createItemsTable);
            await connection.ExecuteAsync(createAttachmentsTable);
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
                          SET CompanyName = @CompanyName, 
                              EmployeeName = @EmployeeName, 
                              TransactionDate = @TransactionDate, 
                              GrandTotal = @GrandTotal 
                          WHERE Id = @Id",
                        new
                        {
                            Id = existingId.Value,
                            transaction.CompanyName,
                            transaction.EmployeeName,
                            TransactionDate = transaction.TransactionDate.ToString("yyyy-MM-dd"),
                            transaction.GrandTotal
                        },
                        dbTransaction);

                    await connection.ExecuteAsync(
                        "DELETE FROM TransactionItems WHERE TransactionId = @TransactionId",
                        new { TransactionId = existingId.Value },
                        dbTransaction);

                    transactionId = existingId.Value;
                }
                else
                {
                    transactionId = await connection.QuerySingleAsync<long>(
                        @"INSERT INTO Transactions (InvoiceNumber, CompanyName, EmployeeName, TransactionDate, GrandTotal) 
                          VALUES (@InvoiceNumber, @CompanyName, @EmployeeName, @TransactionDate, @GrandTotal);
                          SELECT last_insert_rowid();",
                        new
                        {
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
                    var itemId = await connection.QuerySingleAsync<long>(
                        @"INSERT INTO TransactionItems (TransactionId, ServiceName, Quantity, UnitPrice, Profit, Discount, AttachmentPath) 
                          VALUES (@TransactionId, @ServiceName, @Quantity, @UnitPrice, @Profit, @Discount, @AttachmentPath);
                          SELECT last_insert_rowid();",
                        new
                        {
                            TransactionId = transactionId,
                            item.ServiceName,
                            item.Quantity,
                            item.UnitPrice,
                            item.Profit,
                            item.Discount,
                            item.AttachmentPath
                        },
                        dbTransaction);
                    
                    item.Id = itemId;
                    
                    if (item.Attachments.Count > 0)
                    {
                        foreach (var attachment in item.Attachments)
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
                  ORDER BY t.Id DESC",
                (trans, item) =>
                {
                    if (!transactionDict.TryGetValue((long)trans.Id, out var transaction))
                    {
                        transaction = new Transaction
                        {
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
                                Discount = (double)item.Discount,
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

        public async Task<int> GetNextInvoiceNumberAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var lastInvoice = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT InvoiceNumber FROM Transactions ORDER BY Id DESC LIMIT 1");

            if (string.IsNullOrEmpty(lastInvoice))
                return 811;

            if (int.TryParse(lastInvoice, out int lastNumber))
                return lastNumber + 1;

            return 811;
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
    }
}
