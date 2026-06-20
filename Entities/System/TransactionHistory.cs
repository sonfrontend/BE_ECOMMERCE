using System;

namespace BE_ECOMMERCE.Entities.System
{
    public class TransactionHistory
    {
        public int Id { get; set; }
        
        // Liên kết đến SandboxWallet
        public int WalletId { get; set; }
        public SandboxWallet Wallet { get; set; }

        public int? OrderId { get; set; }
        public decimal AmountChanged { get; set; } // + hoặc -
        public decimal NewBalance { get; set; }
        public string TransactionType { get; set; } = string.Empty; // PAYMENT, REFUND
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
