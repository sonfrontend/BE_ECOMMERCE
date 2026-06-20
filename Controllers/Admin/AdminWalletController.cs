using BE_ECOMMERCE.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_ECOMMERCE.Controllers.Admin
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminWalletController(ApplicationDbContext context) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;

        [HttpGet]
        public async Task<IActionResult> GetWalletBalance()
        {
            var wallet = await _context.SandboxWallets.FirstOrDefaultAsync(w => w.AccountType == "ADMIN");
            if (wallet == null)
            {
                return NotFound("Wallet not found");
            }

            return Ok(new
            {
                balance = wallet.Balance,
                accountName = wallet.AccountName
            });
        }

        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactionHistory()
        {
            var wallet = await _context.SandboxWallets.FirstOrDefaultAsync(w => w.AccountType == "ADMIN");
            if (wallet == null)
            {
                return NotFound("Wallet not found");
            }

            var transactions = await _context.TransactionHistories
                .Where(t => t.WalletId == wallet.Id)
                .OrderByDescending(t => t.TransactionDate)
                .Select(t => new
                {
                    id = t.Id,
                    orderId = t.OrderId,
                    amountChanged = t.AmountChanged,
                    newBalance = t.NewBalance,
                    transactionType = t.TransactionType,
                    transactionDate = t.TransactionDate,
                    description = t.Description
                })
                .ToListAsync();

            return Ok(transactions);
        }
    }
}
