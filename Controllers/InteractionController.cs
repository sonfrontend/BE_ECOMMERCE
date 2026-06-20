using BE_ECOMMERCE.Entities;
using BE_ECOMMERCE.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BE_ECOMMERCE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InteractionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InteractionController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("log")]
        [Authorize]
        public async Task<IActionResult> LogInteraction([FromBody] LogInteractionRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            // Check if this specific interaction (e.g., View > 5s) already exists today to prevent spam
            var exists = _context.UserInteractions.Any(u => u.UserId == userId && u.ProductId == request.ProductId && u.InteractionType == request.InteractionType && u.CreatedAt.Date == DateTime.UtcNow.Date);
            if (exists)
            {
                return Ok(new { success = true, message = "Already logged today" });
            }

            var interaction = new UserInteraction
            {
                UserId = userId,
                ProductId = request.ProductId,
                InteractionType = request.InteractionType,
                Score = request.Score
            };

            _context.UserInteractions.Add(interaction);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }

    public class LogInteractionRequest
    {
        public string ProductId { get; set; }
        public string InteractionType { get; set; } // "VIEW", "CART", "PURCHASE", "FAVORITE"
        public int Score { get; set; }
    }
}
