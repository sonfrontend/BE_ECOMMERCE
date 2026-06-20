using System;

namespace BE_ECOMMERCE.Entities.System
{
    public class SandboxWallet
    {
        public int Id { get; set; }
        public string AccountType { get; set; } = "ADMIN";
        public string AccountName { get; set; } = "PayPal Business (Mô phỏng)";
        public decimal Balance { get; set; } = 0;
    }
}
