using System.ComponentModel.DataAnnotations;

namespace BE_ECOMMERCE.DTOs.Orders;

public class SendMessageDto
{
    [Required]
    public string Message { get; set; }
}

public class ProposeResolutionDto
{
    [Required]
    public int TemplateId { get; set; }
    
    public string Note { get; set; }
}

public class ReplyResolutionDto
{
    [Required]
    public bool Accept { get; set; }
}

public class DisputeMessageDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; }
    public string Message { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ResolutionTemplateDto
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string? FinalOrderStatus { get; set; }
}
