using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Entities.Order;
using BE_ECOMMERCE.Enums;
using BE_ECOMMERCE.Services.VnPay;
using Microsoft.EntityFrameworkCore;

namespace BE_ECOMMERCE.Services;

public interface IDisputeResolutionExecutor
{
    Task<(bool Success, string Message)> ExecuteResolutionAsync(Complaint complaint, string executorName);
}

public class DisputeResolutionExecutor(ApplicationDbContext context, IVnPayService vnPayService) : IDisputeResolutionExecutor
{
    public async Task<(bool Success, string Message)> ExecuteResolutionAsync(Complaint complaint, string executorName)
    {
        var order = complaint.Order;
        if (order == null)
            return (false, "Order không tồn tại.");

        // 1. Kiểm tra RequiresRefund
        if (complaint.RequiresRefund)
        {
            decimal refundAmt = complaint.IsFullRefund ? order.TotalAmount : (complaint.RefundAmount ?? 0);
            
            if (refundAmt > 0 && refundAmt <= order.TotalAmount)
            {
                if (complaint.PaymentMethod == "VNPAY")
                {
                    string refundType = complaint.IsFullRefund ? "02" : "03";
                    bool refundSuccess = await vnPayService.RefundAsync(order, refundAmt, refundType, executorName);
                    if (!refundSuccess)
                        return (false, "Lỗi khi gọi API hoàn tiền qua VNPay.");

                    // Cập nhật ví giả lập Admin chỉ khi hoàn qua VNPAY
                    var adminWallet = await context.SandboxWallets.FirstOrDefaultAsync(w => w.AccountType == "ADMIN");
                    if (adminWallet != null)
                    {
                        adminWallet.Balance -= refundAmt;
                        context.TransactionHistories.Add(new Entities.System.TransactionHistory
                        {
                            WalletId = adminWallet.Id,
                            OrderId = order.Id,
                            AmountChanged = -refundAmt,
                            NewBalance = adminWallet.Balance,
                            TransactionType = "REFUND",
                            TransactionDate = DateTime.Now,
                            Description = $"Hoàn tiền tranh chấp ({(complaint.IsFullRefund ? "toàn bộ" : "một phần")}) cho đơn hàng #{order.Id}"
                        });
                    }
                }
            }
        }

        // 2. Kiểm tra RestoresInventory
        if (complaint.RestoresInventory)
        {
            if (order.OrderItems == null || !order.OrderItems.Any())
            {
                // Load order items if not already loaded
                await context.Entry(order).Collection(o => o.OrderItems).Query().Include(oi => oi.ProductVariant).ThenInclude(pv => pv.Product).LoadAsync();
            }

            foreach (var item in order.OrderItems)
            {
                if (item.ProductVariant == null)
                {
                    await context.Entry(item).Reference(i => i.ProductVariant).Query().Include(pv => pv.Product).LoadAsync();
                }

                var pv = item.ProductVariant;
                if (pv != null)
                {
                    pv.StockQuantity += item.Quantity;
                    pv.SoldQuantity -= item.Quantity;
                    
                    if (pv.Product != null)
                    {
                        pv.Product.SoldQuantity -= item.Quantity;
                    }
                }
            }
        }

        // 3. Cập nhật trạng thái
        complaint.Status = "Resolved";
        complaint.ResolvedAt = DateTime.Now;

        // FinalOrderStatus logic
        string? finalStatusString = complaint.FinalOrderStatus;
        if (string.IsNullOrEmpty(finalStatusString) && complaint.HandlingMethodId != null)
        {
            var template = complaint.ResolutionTemplate ?? await context.ResolutionTemplates.FindAsync(complaint.HandlingMethodId);
            if (template != null)
            {
                finalStatusString = template.FinalOrderStatus;
            }
        }
        
        OrderStatus finalStatus = OrderStatus.Cancelled; // Default
        if (!string.IsNullOrEmpty(finalStatusString) && Enum.TryParse<OrderStatus>(finalStatusString, out var parsedStatus))
        {
            finalStatus = parsedStatus;
        }
        
        order.Status = finalStatus;

        return (true, "Thực thi thành công.");
    }
}
