using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProWalid.Services
{
    public class SyncRequest
    {
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = string.Empty;
        [JsonPropertyName("last_sync")]
        public string LastSync { get; set; } = "2000-01-01 00:00:00";
        [JsonPropertyName("clients")]
        public List<SyncClientPush> Clients { get; set; } = new();
        [JsonPropertyName("transactions")]
        public List<SyncTransactionPush> Transactions { get; set; } = new();
        [JsonPropertyName("line_items")]
        public List<SyncLineItemPush> LineItems { get; set; } = new();
    }

    public class SyncClientPush
    {
        [JsonPropertyName("desktop_id")] public long DesktopId { get; set; }
        [JsonPropertyName("sync_uuid")] public string SyncUuid { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("phone")] public string Phone { get; set; } = "";
        [JsonPropertyName("notes")] public string Notes { get; set; } = "";
        [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
        [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
    }

    public class SyncTransactionPush
    {
        [JsonPropertyName("desktop_id")] public long DesktopId { get; set; }
        [JsonPropertyName("desktop_client_id")] public long DesktopClientId { get; set; }
        [JsonPropertyName("sync_uuid")] public string SyncUuid { get; set; } = "";
        [JsonPropertyName("invoice_number")] public string InvoiceNumber { get; set; } = "";
        [JsonPropertyName("company_name")] public string CompanyName { get; set; } = "";
        [JsonPropertyName("employee_name")] public string EmployeeName { get; set; } = "";
        [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
        [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
    }

    public class SyncLineItemPush
    {
        [JsonPropertyName("desktop_transaction_id")] public long DesktopTransactionId { get; set; }
        [JsonPropertyName("sync_uuid")] public string SyncUuid { get; set; } = "";
        [JsonPropertyName("service_name")] public string ServiceName { get; set; } = "";
        [JsonPropertyName("quantity")] public double Quantity { get; set; }
        [JsonPropertyName("unit_price")] public double UnitPrice { get; set; }
        [JsonPropertyName("total")] public double Total { get; set; }
        [JsonPropertyName("company_name")] public string CompanyName { get; set; } = "";
        [JsonPropertyName("employee_name")] public string EmployeeName { get; set; } = "";
        [JsonPropertyName("gov_fees")] public string GovFees { get; set; } = "";
        [JsonPropertyName("item_date")] public string ItemDate { get; set; } = "";
        [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
    }

    public class SyncResponse
    {
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("server_time")] public string ServerTime { get; set; } = "";
        [JsonPropertyName("stats")] public SyncStats? Stats { get; set; }
        [JsonPropertyName("clients")] public List<SyncClientPull>? Clients { get; set; }
        [JsonPropertyName("transactions")] public List<SyncTransactionPull>? Transactions { get; set; }
        [JsonPropertyName("line_items")] public List<SyncLineItemPull>? LineItems { get; set; }
        [JsonPropertyName("client_id_map")] public Dictionary<string, long>? ClientIdMap { get; set; }
        [JsonPropertyName("transaction_id_map")] public Dictionary<string, long>? TransactionIdMap { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
    }

    public class SyncStats
    {
        [JsonPropertyName("clients_pushed")] public int ClientsPushed { get; set; }
        [JsonPropertyName("clients_pulled")] public int ClientsPulled { get; set; }
        [JsonPropertyName("tx_pushed")] public int TxPushed { get; set; }
        [JsonPropertyName("tx_pulled")] public int TxPulled { get; set; }
        [JsonPropertyName("items_pushed")] public int ItemsPushed { get; set; }
        [JsonPropertyName("items_pulled")] public int ItemsPulled { get; set; }
    }

    public class SyncClientPull
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("phone")] public string? Phone { get; set; }
        [JsonPropertyName("notes")] public string? Notes { get; set; }
        [JsonPropertyName("balance")] public double Balance { get; set; }
        [JsonPropertyName("interest_total")] public double InterestTotal { get; set; }
        [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
        [JsonPropertyName("sync_uuid")] public string SyncUuid { get; set; } = "";
        [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
    }

    public class SyncTransactionPull
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("client_id")] public long ClientId { get; set; }
        [JsonPropertyName("transaction_type")] public string TransactionType { get; set; } = "";
        [JsonPropertyName("invoice_number")] public string InvoiceNumber { get; set; } = "";
        [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
        [JsonPropertyName("sync_uuid")] public string SyncUuid { get; set; } = "";
        [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
        [JsonPropertyName("client_sync_uuid")] public string? ClientSyncUuid { get; set; }
    }

    public class SyncLineItemPull
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("transaction_id")] public long TransactionId { get; set; }
        [JsonPropertyName("transaction_type")] public string? ServiceName { get; set; }
        [JsonPropertyName("number")] public string? Quantity { get; set; }
        [JsonPropertyName("unit_price")] public double UnitPrice { get; set; }
        [JsonPropertyName("total")] public double Total { get; set; }
        [JsonPropertyName("company_name")] public string? CompanyName { get; set; }
        [JsonPropertyName("employee_name")] public string? EmployeeName { get; set; }
        [JsonPropertyName("discount")] public string? GovFees { get; set; }
        [JsonPropertyName("item_date")] public string? ItemDate { get; set; }
        [JsonPropertyName("sync_uuid")] public string SyncUuid { get; set; } = "";
        [JsonPropertyName("updated_at")] public string? UpdatedAt { get; set; }
        [JsonPropertyName("transaction_sync_uuid")] public string? TransactionSyncUuid { get; set; }
    }
}
