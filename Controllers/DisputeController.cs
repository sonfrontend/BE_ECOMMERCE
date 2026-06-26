using BE_ECOMMERCE.Enums;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BE_ECOMMERCE.Constants;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Entities.Order;

using BE_ECOMMERCE.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BE_ECOMMERCE.Services;
using BE_ECOMMERCE.Services.VnPay;
using BE_ECOMMERCE.Services.Notification;

namespace BE_ECOMMERCE.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DisputeController(
    ApplicationDbContext context, 
    IHubContext<ChatHub> hubContext, 
    IVnPayService vnPayService, 
    INotificationService notificationService,
    IDisputeResolutionExecutor executor,
    BE_ECOMMERCE.Services.CloudinaryService cloudinaryService) : ControllerBase
{
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());
    private bool IsAdmin() => User.Claims.Any(c => c.Type == ClaimTypes.Role && (c.Value == "Admin" || c.Value == "SuperAdmin"));

    public class CreateComplaintDto
    {
        public int OrderId { get; set; }
        public int? OrderItemId { get; set; }
        public string Reason { get; set; }
        public string? EvidenceUrl { get; set; }
    }

    public class ResolveComplaintDto
    {
        public int HandlingMethodId { get; set; }
        public decimal? RefundAmount { get; set; }
        public string AdminNote { get; set; }
        public string? AdminEvidenceUrl { get; set; }
        public bool RestoresInventory { get; set; }
        public bool IsFullRefund { get; set; }
        public bool RequiresRefund { get; set; }
        public string PaymentMethod { get; set; }
        public string? FinalOrderStatus { get; set; }
    }

    [HttpGet("reasons")]
    [AllowAnonymous]
    public async Task<IActionResult> GetComplaintReasons()
    {
        var reasons = await context.ComplaintReasons
            .Where(r => r.IsActive)
            .Select(r => new { r.Id, r.Title })
            .ToListAsync();
            
        return Ok(reasons);
    }

    [HttpPost("complaints")]
    public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintDto dto)
    {
        var userId = GetUserId();
        var order = await context.Orders.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.UserId == userId);
        
        if (order == null) return NotFound("Order not found or not yours.");
        if (order.Status != OrderStatus.Delivered) 
            return BadRequest("You can only complain about delivered orders (or specific statuses).");

        var complaint = new Complaint
        {
            OrderId = order.Id,
            UserId = userId,
            Reason = dto.Reason,
            EvidenceUrl = dto.EvidenceUrl,
            Status = "Processing",
            CreatedAt = DateTime.Now
        };

        context.Complaints.Add(complaint);
        order.Status = OrderStatus.Disputed;
        
        try
        {
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(dto.EvidenceUrl))
            {
                await cloudinaryService.DeleteComplaintImageAsync(dto.EvidenceUrl);
            }
            return StatusCode(500, new { message = "Lỗi hệ thống khi tạo khiếu nại." });
        }

        await notificationService.SendNotificationAsync(
            userId,
            "Đã gửi khiếu nại",
            $"Khiếu nại cho đơn hàng #{order.Id} đã được gửi. Admin sẽ xử lý sớm.",
            "OrderStatusChanged",
            order.Id.ToString()
        );

        return Ok(new { message = "Complaint created successfully", complaintId = complaint.Id });
    }

    [HttpGet("order/{orderId}/complaint")]
    public async Task<IActionResult> GetComplaintByOrder(int orderId)
    {
        if (!IsAdmin()) return Forbid("Only admin can view this.");
        var complaint = await context.Complaints
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(c => c.OrderId == orderId);

        if (complaint == null) return NotFound("Complaint not found.");
        return Ok(complaint);
    }

    [HttpGet("admin/complaints")]
    public async Task<IActionResult> GetAdminComplaints()
    {
        if (!IsAdmin()) return Forbid("Only admin can view this.");
        var complaints = await context.Complaints
            .Include(c => c.User)
            .Include(c => c.Order)
            .Include(c => c.ResolutionTemplate)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.OrderId,
                c.UserId,
                UserName = c.User.FullName ?? c.User.UserName,
                c.Reason,
                c.Status,
                c.CreatedAt,
                c.EvidenceUrl,
                OrderTotal = c.Order.TotalAmount,
                c.RefundAmount,
                c.AdminNote,
                c.AdminEvidenceUrl,
                c.ResolvedAt,
                c.HandlingMethodId,
                HandlingMethodName = c.ResolutionTemplate != null ? c.ResolutionTemplate.Title : "Thỏa thuận khác",
                HandlingMethodDescription = c.ResolutionTemplate != null ? c.ResolutionTemplate.Description : null,
                c.RequiresRefund,
                c.PaymentMethod,
                c.RestoresInventory
            })
            .ToListAsync();
        
        return Ok(complaints);
    }

    [HttpPost("complaints/{id}/propose-resolution")]
    public async Task<IActionResult> ProposeResolution(int id, [FromBody] ResolveComplaintDto dto)
    {
        if (!IsAdmin()) return Forbid("Only admin can propose resolution.");

        var complaint = await context.Complaints
            .Include(c => c.Order)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (complaint == null) return NotFound("Complaint not found.");
        if (complaint.Status == "Resolved") return BadRequest("Complaint already resolved.");

        var template = await context.ResolutionTemplates.FindAsync(dto.HandlingMethodId);
        if (template == null) return BadRequest("Invalid handling method.");

        // Admin đề xuất cách giải quyết, lưu vào Complaint và đổi trạng thái Order
        complaint.HandlingMethodId = template.Id;
        complaint.RefundAmount = dto.RefundAmount;
        complaint.AdminNote = dto.AdminNote;
        string? oldAdminEvidenceUrl = complaint.AdminEvidenceUrl;
        bool isAdminEvidenceChanged = oldAdminEvidenceUrl != dto.AdminEvidenceUrl;
        complaint.AdminEvidenceUrl = dto.AdminEvidenceUrl;
        complaint.RestoresInventory = dto.RestoresInventory;
        complaint.IsFullRefund = dto.IsFullRefund;
        complaint.RequiresRefund = dto.RequiresRefund;
        complaint.PaymentMethod = dto.PaymentMethod;
        complaint.FinalOrderStatus = dto.FinalOrderStatus;
        
        complaint.Status = "ResolutionProposed";
        complaint.Order.Status = OrderStatus.PendingResolution;

        try
        {
            await context.SaveChangesAsync();
            
            if (isAdminEvidenceChanged && !string.IsNullOrEmpty(oldAdminEvidenceUrl))
            {
                await cloudinaryService.DeleteComplaintImageAsync(oldAdminEvidenceUrl);
            }
        }
        catch (Exception ex)
        {
            if (isAdminEvidenceChanged && !string.IsNullOrEmpty(dto.AdminEvidenceUrl))
            {
                await cloudinaryService.DeleteComplaintImageAsync(dto.AdminEvidenceUrl);
            }
            return StatusCode(500, new { message = "Lỗi hệ thống khi đề xuất giải quyết." });
        }

        await hubContext.Clients.Group(complaint.UserId.ToString()).SendAsync("ResolutionProposed", new {
            ComplaintId = complaint.Id,
            OrderId = complaint.OrderId,
            Method = template.Code,
            Note = dto.AdminNote
        });

        await notificationService.SendNotificationAsync(
            complaint.UserId,
            "Đề xuất xử lý khiếu nại",
            $"Admin đã đưa ra đề xuất xử lý cho khiếu nại đơn hàng #{complaint.OrderId}. Vui lòng kiểm tra.",
            "OrderStatusChanged",
            complaint.OrderId.ToString()
        );

        return Ok(new { message = "Resolution proposed successfully" });
    }

    [HttpGet("resolution-templates")]
    public async Task<IActionResult> GetResolutionTemplates()
    {
        var templates = await context.ResolutionTemplates.ToListAsync();
        return Ok(templates);
    }

    public class AcceptResolutionDto
    {
        public bool Accept { get; set; }
    }

    [HttpPost("order/{orderId}/reply-resolution")]
    public async Task<IActionResult> AcceptResolution(int orderId, [FromBody] AcceptResolutionDto dto)
    {
        var userId = GetUserId();

        var complaint = await context.Complaints
            .Include(c => c.Order)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductVariant)
                        .ThenInclude(pv => pv.Product)
            .Include(c => c.ResolutionTemplate) // Cần fetch ResolutionTemplate đã được Admin đề xuất
            .FirstOrDefaultAsync(c => c.OrderId == orderId && c.UserId == userId);

        if (complaint == null) return NotFound("Complaint not found or not yours.");
        if (complaint.Order.Status != OrderStatus.PendingResolution)
            return BadRequest("Order is not waiting for your resolution reply.");

        var order = complaint.Order;

        if (!dto.Accept)
        {
            order.Status = OrderStatus.Disputed;
            // Xoá đề xuất để Admin làm lại
            complaint.HandlingMethodId = null;
            complaint.RefundAmount = null;
            complaint.AdminNote = null;
            await context.SaveChangesAsync();

            await notificationService.SendNotificationAsync(
                userId,
                "Từ chối đề xuất",
                $"Bạn đã từ chối đề xuất xử lý cho khiếu nại đơn hàng #{order.Id}.",
                "OrderStatusChanged",
                order.Id.ToString()
            );

            return Ok(new { message = "Resolution rejected", status = order.Status });
        }

        // Thực thi luồng xử lý theo đề xuất chung (thông qua Executor)
        var executorName = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
        var result = await executor.ExecuteResolutionAsync(complaint, executorName);

        if (!result.Success)
        {
            return BadRequest(result.Message);
        }

        await context.SaveChangesAsync();
        
        await hubContext.Clients.Group(order.UserId.ToString()).SendAsync("ComplaintResolved", new {
            ComplaintId = complaint.Id,
            OrderId = order.Id,
            Status = complaint.Status,
            Method = complaint.ResolutionTemplate?.Code ?? "CUSTOM"
        });

        return Ok(new { message = "Complaint resolved successfully", orderStatus = order.Status });
    }
}


