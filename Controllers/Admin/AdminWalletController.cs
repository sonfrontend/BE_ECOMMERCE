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

        [HttpGet("cod-summary")]
        public async Task<IActionResult> GetCodSummary()
        {
            var codOrders = await _context.Orders
                .Where(o => o.PaymentMethod == "COD")
                .ToListAsync();

            decimal totalCodReceived = codOrders
                .Where(o => o.Status == Enums.OrderStatus.Completed || o.Status == Enums.OrderStatus.Resolved)
                .Sum(o => o.TotalAmount);

            decimal totalCodPending = codOrders
                .Where(o => o.Status == Enums.OrderStatus.Pending 
                         || o.Status == Enums.OrderStatus.Processing
                         || o.Status == Enums.OrderStatus.Shipped
                         || o.Status == Enums.OrderStatus.Delivered)
                .Sum(o => o.TotalAmount);

            var manualRefunds = await _context.Complaints
                .Where(c => c.Status == "Resolved" && c.RequiresRefund && c.PaymentMethod == "COD")
                .ToListAsync();

            decimal totalManualRefunded = 0;
            foreach (var complaint in manualRefunds)
            {
                if (complaint.IsFullRefund)
                {
                    var order = await _context.Orders.FindAsync(complaint.OrderId);
                    if (order != null) totalManualRefunded += order.TotalAmount;
                }
                else
                {
                    totalManualRefunded += complaint.RefundAmount ?? 0;
                }
            }

            return Ok(new
            {
                totalCodReceived,
                totalCodPending,
                totalManualRefunded
            });
        }

        [HttpGet("cod-transactions")]
        public async Task<IActionResult> GetCodTransactions()
        {
            var transactions = new List<object>();

            // 1. Nhận thanh toán (Orders Completed/Resolved)
            var codOrders = await _context.Orders
                .Where(o => o.PaymentMethod == "COD" && (o.Status == Enums.OrderStatus.Completed || o.Status == Enums.OrderStatus.Resolved))
                .ToListAsync();

            foreach (var order in codOrders)
            {
                transactions.Add(new
                {
                    id = $"order_{order.Id}",
                    orderId = order.Id,
                    amountChanged = order.TotalAmount,
                    newBalance = 0, // Không cần thiết cho COD
                    transactionType = "PAYMENT",
                    transactionDate = order.UpdatedAt ?? order.OrderDate,
                    description = $"Thu tiền mặt thành công cho đơn hàng #{order.Id}"
                });
            }

            // 2. Hoàn tiền thủ công (Complaints Resolved, RequiresRefund)
            var manualRefunds = await _context.Complaints
                .Where(c => c.Status == "Resolved" && c.RequiresRefund && c.PaymentMethod == "COD")
                .ToListAsync();

            foreach (var complaint in manualRefunds)
            {
                decimal refundAmount = 0;
                if (complaint.IsFullRefund)
                {
                    var order = await _context.Orders.FindAsync(complaint.OrderId);
                    if (order != null) refundAmount = order.TotalAmount;
                }
                else
                {
                    refundAmount = complaint.RefundAmount ?? 0;
                }

                if (refundAmount > 0)
                {
                    transactions.Add(new
                    {
                        id = $"refund_{complaint.Id}",
                        orderId = complaint.OrderId,
                        amountChanged = -refundAmount,
                        newBalance = 0, // Không cần thiết cho COD
                        transactionType = "REFUND",
                        transactionDate = complaint.UpdatedAt ?? complaint.CreatedAt,
                        description = $"Hoàn tiền thủ công (thoả thuận) cho khiếu nại #{complaint.Id} của đơn hàng #{complaint.OrderId}"
                    });
                }
            }

            // Sort by date descending
            var sortedTransactions = transactions
                .OrderByDescending(t => (DateTime)t.GetType().GetProperty("transactionDate").GetValue(t, null))
                .ToList();

            return Ok(sortedTransactions);
        }
    }
}
