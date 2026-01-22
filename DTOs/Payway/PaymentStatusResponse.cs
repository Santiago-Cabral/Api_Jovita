// File: DTOs/Payway/PaymentStatusResponse.cs
using System;

namespace ForrajeriaJovitaAPI.DTOs.Payway
{
    public class PaymentStatusResponse
    {
        public string? TransactionId { get; set; }
        public string? Status { get; set; } // e.g., "pending", "paid", "canceled"
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? RawResponse { get; set; } // opcional para debugging
    }
}
