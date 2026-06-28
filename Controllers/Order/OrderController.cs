using BE_ECOMMERCE.Enums;
using System.Security.Claims;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.DTOs.Orders;
using BE_ECOMMERCE.Entities.Order;
using BE_ECOMMERCE.Entities.Transaction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_ECOMMERCE.Controllers.Order;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OrderController(ApplicationDbContext context, BE_ECOMMERCE.Services.Email.IEmailService emailService, IConfiguration configuration, BE_ECOMMERCE.Services.Notification.INotificationService notificationService, BE_ECOMMERCE.Services.VnPay.IVnPayService vnPayService) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly BE_ECOMMERCE.Services.Email.IEmailService _emailService = emailService;
    private readonly IConfiguration _configuration = configuration;
    private readonly BE_ECOMMERCE.Services.Notification.INotificationService _notificationService = notificationService;
    private readonly BE_ECOMMERCE.Services.VnPay.IVnPayService _vnPayService = vnPayService;

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        if (request.SelectedCartItemIds == null || request.SelectedCartItemIds.Count == 0)
            return BadRequest("Không có sản phẩm nào được chọn");

        var cartItems = await _context.CartItems
            .Include(c => c.ProductVariant)
            .ThenInclude(v => v.Product)
            .Where(c => c.UserId == userId && request.SelectedCartItemIds.Contains(c.Id))
            .ToListAsync();

        if (cartItems.Count == 0)
            return BadRequest("Sản phẩm trong giỏ không hợp lệ");

        decimal totalAmount = 0; // Thay vì gán phí ship vào luôn, ta chỉ tính giá sản phẩm trước để áp mã giảm giá

        var orderItems = new List<OrderItem>();
        var now = DateTime.Now;

        foreach (var item in cartItems)
        {
            if (item.Quantity > item.ProductVariant.StockQuantity)
            {
                return BadRequest($"Sản phẩm {item.ProductVariant.Product.ProductName} (Size {item.ProductVariant.Size}, Màu {item.ProductVariant.Color}) chỉ còn {item.ProductVariant.StockQuantity} cái trong kho.");
            }

            // Trừ tồn kho và cộng đã bán
            item.ProductVariant.StockQuantity -= item.Quantity;
            item.ProductVariant.SoldQuantity += item.Quantity;
            if (item.ProductVariant.Product != null)
            {
                item.ProductVariant.Product.SoldQuantity += item.Quantity;
            }

            bool isDiscountActive = item.ProductVariant.Product.DiscountPercentage > 0 &&
                                    (item.ProductVariant.Product.DiscountEndDate == null || item.ProductVariant.Product.DiscountEndDate >= DateTime.Now);

            var unitPrice = isDiscountActive
                ? (item.ProductVariant.CurrentPrice > 0 ? item.ProductVariant.CurrentPrice : item.ProductVariant.OriginalPrice)
                : item.ProductVariant.OriginalPrice;

            totalAmount += unitPrice * item.Quantity;

            orderItems.Add(new OrderItem
            {
                VariantId = item.VariantId,
                Quantity = item.Quantity,
                UnitPrice = unitPrice
            });

            // Ghi nhận tương tác Purchase cho bảng UserInteractions
            _context.UserInteractions.Add(new BE_ECOMMERCE.Entities.UserInteraction
            {
                UserId = userId,
                ProductId = item.ProductVariant.ProductId,
                InteractionType = "PURCHASE",
                Score = 5,
                CreatedAt = DateTime.UtcNow
            });
        }

        decimal discountAmount = 0;
        Entities.Promotion.UserVoucher? appliedVoucher = null;
        if (!string.IsNullOrEmpty(request.VoucherCode))
        {
            var userVoucher = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .FirstOrDefaultAsync(uv => uv.Voucher.Code == request.VoucherCode && uv.UserId == userId);

            if (userVoucher != null && userVoucher.Voucher.IsActived && !userVoucher.IsUsed && totalAmount >= userVoucher.Voucher.MinOrderValue)
            {
                // Kiểm tra hạn sử dụng
                if (userVoucher.Voucher.StartDate > now || userVoucher.Voucher.EndDate < now)
                {
                    return BadRequest("Mã giảm giá này đã hết hạn hoặc chưa đến thời gian sử dụng.");
                }

                discountAmount = userVoucher.Voucher.DiscountValue;
                if (discountAmount > totalAmount) discountAmount = totalAmount;

                // Đánh dấu là đã sử dụng
                userVoucher.IsUsed = true;
                appliedVoucher = userVoucher;
            }
            else
            {
                return BadRequest("Mã giảm giá không hợp lệ, chưa được lưu, hoặc không đủ điều kiện áp dụng.");
            }
        }
        Entities.Promotion.Promotion? appliedPromotion = null;
        if (appliedVoucher == null)
        {
            // Mỗi user chỉ được sử dụng mỗi promotion 1 lần
            var activePromotion = await _context.Promotions
                .Where(p => p.IsActived && p.StartDate <= now && p.EndDate >= now)
                .Where(p => !_context.UserPromotions.Any(up => up.PromotionId == p.Id && up.UserId == userId && up.IsUsed))
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            if (activePromotion != null)
            {
                discountAmount = totalAmount * (activePromotion.DiscountPercentage / 100m);
                appliedPromotion = activePromotion;
            }
        }

        totalAmount -= discountAmount;
        totalAmount += request.ShippingFee; // Shipping fee is added after discount

        var order = new Entities.Order.Order
        {
            UserId = userId,
            OrderDate = now,
            Status = (request.PaymentMethod == "VNPAY")
                ? OrderStatus.PendingPayment
                : OrderStatus.Pending,
            TotalAmount = totalAmount,
            ShippingFee = request.ShippingFee,
            PaymentMethod = request.PaymentMethod,
            VoucherCode = appliedVoucher != null ? appliedVoucher.Voucher.Code : (appliedPromotion != null ? appliedPromotion.Title : null),
            DiscountAmount = discountAmount,
            RecipientName = request.RecipientName,
            PhoneNumber = request.PhoneNumber,
            ShippingAddress = request.ShippingAddress,
            Email = request.Email,
            OrderItems = orderItems
        };

        _context.Orders.Add(order);
        _context.CartItems.RemoveRange(cartItems);

        await _context.SaveChangesAsync();

        // Gán OrderId cho Voucher nếu có dùng
        if (appliedVoucher != null)
        {
            appliedVoucher.OrderId = order.Id;
        }

        // Tạo UserPromotion nếu có dùng Promotion
        if (appliedPromotion != null)
        {
            _context.UserPromotions.Add(new Entities.Promotion.UserPromotion
            {
                UserId = userId,
                PromotionId = appliedPromotion.Id,
                OrderId = order.Id,
                IsUsed = true
            });
        }

        if (appliedVoucher != null || appliedPromotion != null)
        {
            await _context.SaveChangesAsync();
        }

        string emailStatus = "Not attempted";
        // Send Email if it's COD
        if (order.PaymentMethod == Constants.PaymentMethodConstant.COD)
        {
            var orderWithItems = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.ProductVariant)
                .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            var user = await _context.Users.FindAsync(userId);
            if (orderWithItems != null && user != null)
            {
                emailStatus = await _emailService.SendOrderConfirmationEmailAsync(orderWithItems, user);
            }
        }

        // Gửi thông báo Notification
        await _notificationService.SendNotificationAsync(
            userId,
            "Đặt hàng thành công",
            $"Đơn hàng #{order.Id} đã được tạo thành công.",
            "OrderCreated",
            order.Id.ToString()
        );

        // Gửi thông báo cho Admin
        var adminRoleIds = await _context.Roles
            .Where(r => r.RoleName == "Admin" || r.RoleName == "SuperAdmin")
            .Select(r => r.RoleId)
            .ToListAsync();
            
        if (adminRoleIds.Any())
        {
            var adminIds = await _context.UserRoles
                .Where(ur => adminRoleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();
            
            foreach (var adminId in adminIds)
            {
                await _notificationService.SendNotificationAsync(
                    adminId, 
                    "Đơn hàng mới", 
                    $"Có đơn hàng mới #{order.Id} trị giá {order.TotalAmount:N0}đ", 
                    "System", 
                    order.Id.ToString()
                );
            }
        }

        return Ok(new
        {
            orderId = order.Id,
            orderDate = order.OrderDate,
            totalAmount = order.TotalAmount,
            status = order.Status,
            paymentMethod = order.PaymentMethod,
            emailStatus = emailStatus
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetOrderHistory()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.ProductVariant)
            .ThenInclude(v => v.Product)
            .Include(o => o.Complaints)
            .ThenInclude(c => c.ResolutionTemplate)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                id = o.Id,
                orderDate = o.OrderDate,
                status = o.Status,
                totalAmount = o.TotalAmount,
                resolutionTemplateTitle = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().ResolutionTemplate != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().ResolutionTemplate.Title : null,
                resolutionTemplateDescription = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().ResolutionTemplate != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().ResolutionTemplate.Description : null,
                complaintReason = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().Reason : null,
                complaintEvidenceUrl = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().EvidenceUrl : null,
                resolutionNote = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().AdminNote : null,
                adminEvidenceUrl = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().AdminEvidenceUrl : null,
                requiresRefund = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().RequiresRefund,
                isFullRefund = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().IsFullRefund,
                refundAmount = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().RefundAmount : null,
                resolutionPaymentMethod = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().PaymentMethod : null,
                restoresInventory = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().RestoresInventory,
                shippingFee = o.ShippingFee,
                paymentMethod = o.PaymentMethod,
                voucherCode = o.VoucherCode,
                discountAmount = o.DiscountAmount,
                isPaid = o.IsPaid,
                recipientName = o.RecipientName,
                phoneNumber = o.PhoneNumber,
                shippingAddress = o.ShippingAddress,
                orderItems = o.OrderItems.Select(oi => new
                {
                    id = oi.Id,
                    articleId = oi.ProductVariant.ProductId,
                    productName = oi.ProductVariant.Product.ProductName,
                    imageUrl = oi.ProductVariant.ImageUrl,
                    color = oi.ProductVariant.Color,
                    size = oi.ProductVariant.Size,
                    quantity = oi.Quantity,
                    unitPrice = oi.UnitPrice,
                    isReviewed = _context.Reviews.Any(r => r.OrderItemId == oi.Id)
                })
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.ProductVariant)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (order == null)
            return NotFound("Order not found");

        if (order.Status != OrderStatus.PendingPayment && order.Status != OrderStatus.Pending)
        {
            return BadRequest("Chỉ có thể hủy đơn hàng đang chờ thanh toán hoặc chờ xác nhận");
        }

        // Hoàn lại tồn kho
        foreach (var item in order.OrderItems)
        {
            item.ProductVariant.StockQuantity += item.Quantity;
            item.ProductVariant.SoldQuantity -= item.Quantity;
            if (item.ProductVariant.Product != null)
            {
                item.ProductVariant.Product.SoldQuantity -= item.Quantity;
            }
        }

        // Hoàn lại Voucher nếu có
        var userVoucher = await _context.UserVouchers.FirstOrDefaultAsync(uv => uv.OrderId == order.Id);
        if (userVoucher != null)
        {
            userVoucher.IsUsed = false;
            userVoucher.OrderId = null;
        }

        // Hoàn lại Promotion nếu có
        var userPromotion = await _context.UserPromotions.FirstOrDefaultAsync(up => up.OrderId == order.Id);
        if (userPromotion != null)
        {
            userPromotion.IsUsed = false;
        }

        order.VoucherCode = null;

        // Xử lý hoàn tiền
        if (order.IsPaid && order.PaymentMethod.Equals("VNPAY", StringComparison.OrdinalIgnoreCase))
        {
            // Cập nhật trạng thái
            order.Status = OrderStatus.Refunded;
            order.IsPaid = false;

            // Gọi VNPay Refund API
            bool refundSuccess = await _vnPayService.RefundAsync(order, order.TotalAmount, "02", "System");
            if (!refundSuccess)
            {
                return BadRequest(new { message = "Hủy đơn thành công nhưng gặp lỗi khi hoàn tiền qua VNPay. Vui lòng liên hệ Admin." });
            }

            // Cập nhật ví giả lập Admin
            var adminWallet = await _context.SandboxWallets.FirstOrDefaultAsync(w => w.AccountType == "ADMIN");
            if (adminWallet != null)
            {
                adminWallet.Balance -= order.TotalAmount;

                var transaction = new BE_ECOMMERCE.Entities.System.TransactionHistory
                {
                    WalletId = adminWallet.Id,
                    OrderId = order.Id,
                    AmountChanged = -order.TotalAmount,
                    NewBalance = adminWallet.Balance,
                    TransactionType = "REFUND",
                    TransactionDate = DateTime.Now,
                    Description = $"Hoàn tiền VNPay cho đơn hàng #{order.Id}"
                };
                _context.TransactionHistories.Add(transaction);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Hủy đơn thành công. Tiền đã được yêu cầu hoàn lại qua VNPay." });
        }

        order.Status = OrderStatus.Cancelled;
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            order.UserId,
            "Đơn hàng đã bị hủy",
            $"Đơn hàng #{order.Id} đã được hủy thành công.",
            "OrderStatusChanged",
            order.Id.ToString()
        );

        return Ok(new { message = "Cancel order successfully" });
    }

    [HttpGet("admin")]
    // [Authorize(Roles = "Admin")] // Uncomment if admin role is configured
    public async Task<IActionResult> GetAllOrdersForAdmin()
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.ProductVariant)
            .ThenInclude(v => v.Product)
            .Include(o => o.Complaints)
            .ThenInclude(c => c.ResolutionTemplate)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                id = o.Id,
                orderDate = o.OrderDate,
                status = o.Status,
                totalAmount = o.TotalAmount,
                resolutionTemplateTitle = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().ResolutionTemplate != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().ResolutionTemplate.Title : null,
                resolutionTemplateDescription = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().ResolutionTemplate != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().ResolutionTemplate.Description : null,
                complaintReason = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().Reason : null,
                complaintEvidenceUrl = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().EvidenceUrl : null,
                resolutionNote = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().AdminNote : null,
                adminEvidenceUrl = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().AdminEvidenceUrl : null,
                requiresRefund = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().RequiresRefund,
                isFullRefund = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().IsFullRefund,
                refundAmount = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().RefundAmount : null,
                resolutionPaymentMethod = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().PaymentMethod : null,
                restoresInventory = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null && o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().RestoresInventory,
                finalOrderStatus = o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault() != null ? o.Complaints.OrderByDescending(c => c.CreatedAt).FirstOrDefault().FinalOrderStatus : null,
                shippingFee = o.ShippingFee,
                paymentMethod = o.PaymentMethod,
                voucherCode = o.VoucherCode,
                discountAmount = o.DiscountAmount,
                isPaid = o.IsPaid,
                recipientName = o.RecipientName,
                phoneNumber = o.PhoneNumber,
                shippingAddress = o.ShippingAddress,
                orderItems = o.OrderItems.Select(oi => new
                {
                    articleId = oi.ProductVariant.ProductId,
                    productName = oi.ProductVariant.Product.ProductName,
                    imageUrl = oi.ProductVariant.ImageUrl,
                    color = oi.ProductVariant.Color,
                    size = oi.ProductVariant.Size,
                    quantity = oi.Quantity,
                    unitPrice = oi.UnitPrice
                })
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpPut("admin/{id}/status")]
    // [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.ProductVariant)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
            return NotFound("Order not found");

        // Hoàn lại tồn kho nếu admin hủy đơn
        if (request.Status == OrderStatus.Cancelled && order.Status != OrderStatus.Cancelled)
        {
            foreach (var item in order.OrderItems)
            {
                item.ProductVariant.StockQuantity += item.Quantity;
                item.ProductVariant.SoldQuantity -= item.Quantity;
                if (item.ProductVariant.Product != null)
                {
                    item.ProductVariant.Product.SoldQuantity -= item.Quantity;
                }
            }

            // Hoàn lại Voucher và Promotion nếu đơn hàng đang ở trạng thái chưa duyệt
            if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.PendingPayment)
            {
                var userVoucher = await _context.UserVouchers.FirstOrDefaultAsync(uv => uv.OrderId == order.Id);
                if (userVoucher != null)
                {
                    userVoucher.IsUsed = false;
                    userVoucher.OrderId = null;
                }

                var userPromotion = await _context.UserPromotions.FirstOrDefaultAsync(up => up.OrderId == order.Id);
                if (userPromotion != null)
                {
                    userPromotion.IsUsed = false;
                }

                order.VoucherCode = null;
            }
        }

        order.Status = request.Status;
        if (request.Status == OrderStatus.Completed)
        {
            order.IsPaid = true;
        }

        if (request.Status == OrderStatus.Delivered)
        {
            order.DeliveredDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        string statusMessage = request.Status switch
        {
            OrderStatus.Pending => "đã được xác nhận và đang chờ xử lý",
            OrderStatus.Processing => "đang được chuẩn bị",
            OrderStatus.Shipped => "đã được giao cho đơn vị vận chuyển",
            OrderStatus.Delivered => "đã được giao thành công",
            OrderStatus.Completed => "đã hoàn thành",
            OrderStatus.Cancelled => "đã bị huỷ",
            OrderStatus.Disputed => "đang được xử lý khiếu nại",
            OrderStatus.PendingPayment => "đang chờ thanh toán",
            _ => $"đã được cập nhật sang trạng thái {request.Status}"
        };

        await _notificationService.SendNotificationAsync(
            order.UserId,
            "Trạng thái đơn hàng thay đổi",
            $"Đơn hàng #{order.Id} {statusMessage}.",
            "OrderStatusChanged",
            order.Id.ToString()
        );

        return Ok(new { message = "Order status updated successfully", order });
    }

    [HttpPut("admin/{id}/approve-payment")]
    // [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApprovePayment(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
            return NotFound("Order not found");

        if (order.Status != OrderStatus.PendingPayment)
        {
            return BadRequest("Đơn hàng không ở trạng thái chờ thanh toán");
        }

        order.IsPaid = true;
        order.Status = OrderStatus.Pending; // Chuyển sang chờ giao hàng

        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            order.UserId,
            "Thanh toán được xác nhận",
            $"Đơn hàng #{order.Id} đã được xác nhận thanh toán.",
            "OrderStatusChanged",
            order.Id.ToString()
        );

        return Ok(new { message = "Xác nhận thanh toán thành công" });
    }

    [HttpPut("{id}/confirm-received")]
    public async Task<IActionResult> ConfirmReceived(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
        if (order == null) return NotFound("Order not found");

        if (order.Status != OrderStatus.Delivered)
            return BadRequest("Đơn hàng chưa ở trạng thái Đã giao hàng");

        order.Status = OrderStatus.Completed;
        order.IsPaid = true;
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            order.UserId,
            "Đơn hàng hoàn thành",
            $"Bạn đã xác nhận nhận hàng cho đơn #{order.Id}. Cảm ơn bạn đã mua sắm!",
            "OrderStatusChanged",
            order.Id.ToString()
        );

        return Ok(new { message = "Xác nhận đã nhận hàng thành công!" });
    }

    [HttpPut("{id}/dispute")]
    public async Task<IActionResult> DisputeOrder(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);
        if (order == null) return NotFound("Order not found");

        if (order.Status != OrderStatus.Delivered)
            return BadRequest("Chỉ có thể khiếu nại đơn hàng đang ở trạng thái Đã giao hàng");

        order.Status = OrderStatus.Disputed;
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            order.UserId,
            "Đã gửi khiếu nại",
            $"Khiếu nại cho đơn hàng #{order.Id} đã được gửi. Admin sẽ xử lý sớm.",
            "OrderStatusChanged",
            order.Id.ToString()
        );

        return Ok(new { message = "Yêu cầu khiếu nại thành công, Admin sẽ xử lý sớm." });
    }

    [HttpGet("test-email")]
    [AllowAnonymous]
    public async Task<IActionResult> TestEmail([FromQuery] string toEmail)
    {
        try
        {
            var smtpConfig = _configuration.GetSection("SmtpConfig");
            var host = smtpConfig["Host"];
            var portStr = smtpConfig["Port"];
            var enableSslStr = smtpConfig["EnableSsl"];
            var username = smtpConfig["Username"];
            var password = smtpConfig["Password"];

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username) || username == "your_email@gmail.com")
            {
                return BadRequest("Cấu hình SMTP chưa được thiết lập. Vui lòng kiểm tra appsettings.json.");
            }

            int.TryParse(portStr, out int port);
            bool.TryParse(enableSslStr, out bool enableSsl);

            using (var client = new System.Net.Mail.SmtpClient(host, port))
            {
                client.EnableSsl = enableSsl;
                client.Credentials = new System.Net.NetworkCredential(username, password);

                var mailMessage = new System.Net.Mail.MailMessage
                {
                    From = new System.Net.Mail.MailAddress(username, "Test ECommerce"),
                    Subject = "Test Email Configuration",
                    IsBodyHtml = true,
                    Body = "<h3>Cấu hình Email thành công!</h3>"
                };

                mailMessage.To.Add(toEmail ?? username);
                await client.SendMailAsync(mailMessage);
            }

            return Ok(new { success = true, message = "Email đã được gửi thành công!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Gửi email thất bại: " + ex.Message, errorDetails = ex.ToString() });
        }
    }

    [HttpPost("admin/{id}/create-compensation")]
    public async Task<IActionResult> CreateCompensationOrder(int id)
    {
        if (!User.Claims.Any(c => c.Type == ClaimTypes.Role && (c.Value == "Admin" || c.Value == "SuperAdmin")))
            return Forbid("Only admin can create compensation orders.");

        var originalOrder = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (originalOrder == null)
            return NotFound("Không tìm thấy đơn hàng gốc.");

        var newOrder = new Entities.Order.Order
        {
            UserId = originalOrder.UserId,
            OrderDate = DateTime.Now,
            Status = OrderStatus.Processing, // Đơn bù tự động duyệt
            RecipientName = originalOrder.RecipientName + $" (Đơn đổi trả cho #{originalOrder.Id})",
            PhoneNumber = originalOrder.PhoneNumber,
            ShippingAddress = originalOrder.ShippingAddress,
            Email = originalOrder.Email,
            TotalAmount = 0,
            ShippingFee = 0,
            PaymentMethod = originalOrder.PaymentMethod,
            IsPaid = true,
            CreatedAt = DateTime.UtcNow,
            IsActived = true,
            OrderItems = originalOrder.OrderItems.Select(oi => new OrderItem
            {
                VariantId = oi.VariantId,
                Quantity = oi.Quantity,
                UnitPrice = 0, // Đơn bù giá 0đ
                CreatedAt = DateTime.UtcNow,
                IsActived = true
            }).ToList()
        };

        _context.Orders.Add(newOrder);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Tạo đơn hàng 0đ thành công", newOrderId = newOrder.Id });
    }
}

public class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; set; }
}

