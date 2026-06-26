using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Entities.Chat;
using BE_ECOMMERCE.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BE_ECOMMERCE.Controllers;

public class SendChatDto
{
    public string? Message { get; set; }
    public Guid? TargetUserId { get; set; } // Admin dùng để gửi cho user cụ thể
    public string? ImageName { get; set; } // Ảnh đính kèm (nếu có)
    public int? ReplyToId { get; set; } // ID tin nhắn gốc được Reply
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; }
    public string? Message { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string? ImageName { get; set; }
    public int? ReplyToId { get; set; }
    public string? ReplyToMessage { get; set; }
    public string? ReplyToSenderName { get; set; }
}

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ChatController(ApplicationDbContext context, IHubContext<ChatHub> hubContext, BE_ECOMMERCE.Services.CloudinaryService cloudinaryService) : ControllerBase
{
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());
    private bool IsAdmin() => User.Claims.Any(c => c.Type == ClaimTypes.Role && (c.Value == "Admin" || c.Value == "SuperAdmin"));

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] Guid? targetUserId)
    {
        var currentUserId = GetUserId();
        var isAdmin = IsAdmin();

        Guid chatUserId = isAdmin && targetUserId.HasValue ? targetUserId.Value : currentUserId;

        var messages = await context.ChatMessages
            .Include(m => m.User)
            .Include(m => m.ReplyTo)
            .Where(m => m.UserId == chatUserId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                UserId = m.UserId,
                SenderId = m.SenderId,
                SenderName = context.Users.Where(u => u.UserId == m.SenderId).Select(u => u.FullName ?? u.UserName).FirstOrDefault(),
                Message = m.Message,
                IsAdmin = m.IsAdmin,
                CreatedAt = m.CreatedAt,
                IsRead = m.IsRead,
                ImageName = m.ImageName,
                ReplyToId = m.ReplyToId,
                ReplyToMessage = m.ReplyTo != null ? m.ReplyTo.Message : null,
                ReplyToSenderName = m.ReplyTo != null ? context.Users.Where(u => u.UserId == m.ReplyTo.SenderId).Select(u => u.FullName ?? u.UserName).FirstOrDefault() : null
            })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendChatDto dto)
    {
        Console.WriteLine($"[DEBUG] SendMessage called. Message: {dto.Message}, ImageName: {dto.ImageName}");
        var currentUserId = GetUserId();
        var isAdmin = IsAdmin();

        Guid chatUserId = isAdmin && dto.TargetUserId.HasValue ? dto.TargetUserId.Value : currentUserId;

        var message = new ChatMessage
        {
            UserId = chatUserId,
            SenderId = currentUserId,
            Message = dto.Message ?? "", // Nếu chỉ gửi ảnh thì Message có thể rỗng
            IsAdmin = isAdmin,
            CreatedAt = DateTime.Now,
            IsRead = false,
            ImageName = dto.ImageName,
            ReplyToId = dto.ReplyToId
        };

        context.ChatMessages.Add(message);
        await context.SaveChangesAsync();

        var senderName = await context.Users.Where(u => u.UserId == currentUserId).Select(u => u.FullName ?? u.UserName).FirstOrDefaultAsync();

        string? replyToMessageText = null;
        string? replyToSenderName = null;
        if (message.ReplyToId.HasValue)
        {
            var repliedMsg = await context.ChatMessages.FindAsync(message.ReplyToId.Value);
            if (repliedMsg != null)
            {
                replyToMessageText = repliedMsg.Message;
                replyToSenderName = await context.Users.Where(u => u.UserId == repliedMsg.SenderId).Select(u => u.FullName ?? u.UserName).FirstOrDefaultAsync();
            }
        }

        var messageDto = new ChatMessageDto
        {
            Id = message.Id,
            UserId = message.UserId,
            SenderId = message.SenderId,
            SenderName = senderName,
            Message = message.Message,
            IsAdmin = message.IsAdmin,
            CreatedAt = message.CreatedAt,
            IsRead = message.IsRead,
            ImageName = message.ImageName,
            ReplyToId = message.ReplyToId,
            ReplyToMessage = replyToMessageText,
            ReplyToSenderName = replyToSenderName
        };

        // Gửi qua SignalR cho cả Admin và User
        await hubContext.Clients.Group(chatUserId.ToString().ToLower()).SendAsync("ReceiveMessage", messageDto);
        // Ngoài ra, Admin cũng lắng nghe ở kênh 'admin-chat' để nhận tất cả tin nhắn mới nếu họ đang ở màn hình chat chung
        await hubContext.Clients.Group("AdminGroup").SendAsync("ReceiveMessage", messageDto);

        return Ok(messageDto);
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage([FromForm] Microsoft.AspNetCore.Http.IFormFile file, [FromForm] string? oldImageUrl, [FromForm] string folder = "images/messages")
    {
        try
        {
            if (!string.IsNullOrEmpty(oldImageUrl))
            {
                await cloudinaryService.DeleteImageAsync(oldImageUrl, folder);
            }
            
            var imageName = await cloudinaryService.UploadImageAsync(file, folder);
            if (string.IsNullOrEmpty(imageName))
                return BadRequest(new { message = "Không tìm thấy file hợp lệ." });

            return Ok(new { imageName });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi upload ảnh: " + ex.Message });
        }
    }

    [HttpPost("upload-complaint-image")]
    public async Task<IActionResult> UploadComplaintImage([FromForm] Microsoft.AspNetCore.Http.IFormFile file, [FromForm] string? oldImageUrl)
    {
        try
        {
            if (!string.IsNullOrEmpty(oldImageUrl))
            {
                await cloudinaryService.DeleteComplaintImageAsync(oldImageUrl);
            }
            
            var imageName = await cloudinaryService.UploadComplaintImageAsync(file);
            if (string.IsNullOrEmpty(imageName))
                return BadRequest(new { message = "Không tìm thấy file hợp lệ." });

            return Ok(new { imageName });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi upload ảnh: " + ex.Message });
        }
    }

    [HttpPost("upload-banner-image")]
    public async Task<IActionResult> UploadBannerImage([FromForm] Microsoft.AspNetCore.Http.IFormFile file, [FromForm] string? oldImageUrl)
    {
        try
        {
            if (!string.IsNullOrEmpty(oldImageUrl))
            {
                await cloudinaryService.DeleteBannerImageAsync(oldImageUrl);
            }
            
            var imageName = await cloudinaryService.UploadBannerImageAsync(file);
            if (string.IsNullOrEmpty(imageName))
                return BadRequest(new { message = "Không tìm thấy file hợp lệ." });

            return Ok(new { url = imageName }); // Return 'url' because frontend CustomImageUpload expects res.data.url
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi upload ảnh: " + ex.Message });
        }
    }

    [HttpGet("conversations")]
    [Authorize] // Chỉ admin mới cần gọi API này, nhưng check bên trong
    public async Task<IActionResult> GetConversations()
    {
        if (!IsAdmin()) return Forbid();

        var conversations = await context.ChatMessages
            .Include(m => m.User)
            .GroupBy(m => m.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                UserName = g.FirstOrDefault().User.FullName ?? g.FirstOrDefault().User.UserName,
                AvatarUrl = g.FirstOrDefault().User.AvatarUrl,
                LastMessage = g.OrderByDescending(m => m.CreatedAt).FirstOrDefault().Message,
                LastMessageAt = g.OrderByDescending(m => m.CreatedAt).FirstOrDefault().CreatedAt,
                UnreadCount = g.Count(m => !m.IsRead && !m.IsAdmin) // Tin nhắn chưa đọc từ User
            })
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        return Ok(conversations);
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromBody] Guid targetUserId)
    {
        if (!IsAdmin()) return Forbid();

        var unreadMessages = await context.ChatMessages
            .Where(m => m.UserId == targetUserId && !m.IsRead && !m.IsAdmin)
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
        }

        await context.SaveChangesAsync();
        return Ok();
    }
}
