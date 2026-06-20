using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BE_ECOMMERCE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_ECOMMERCE.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AiChatController : ControllerBase
{
    private readonly AiChatService _aiChatService;

    public AiChatController(AiChatService aiChatService)
    {
        _aiChatService = aiChatService;
    }

    public class AiChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? SharedProductId { get; set; }
    }

    [HttpPost("ask")]
    public async Task<IActionResult> AskAi([FromBody] AiChatRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message cannot be empty." });
        }

        try
        {
            var responseText = await _aiChatService.AskAiAsync(userId, request.Message, request.SharedProductId);
            return Ok(new { reply = responseText });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi xử lý AI: " + ex.Message });
        }
    }
}
