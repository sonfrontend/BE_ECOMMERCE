using System;
using System.ComponentModel.DataAnnotations;
using BE_ECOMMERCE.Entities.Auth;

namespace BE_ECOMMERCE.Entities.Chat;

public class ChatMessage : BaseEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid UserId { get; set; }
    public virtual User User { get; set; }

    [Required]
    public Guid SenderId { get; set; } // Người gửi tin nhắn này

    [Required]
    public string Message { get; set; }

    public bool IsAdmin { get; set; } // True nếu người gửi là Admin

    public bool IsRead { get; set; } = false; // Admin/User đã đọc tin nhắn này chưa

    public string? ImageName { get; set; } // Tên ảnh lưu trên Cloudinary

    public int? ReplyToId { get; set; } // ID tin nhắn gốc được Reply

    public virtual ChatMessage? ReplyTo { get; set; } // Liên kết đến tin nhắn gốc
}
