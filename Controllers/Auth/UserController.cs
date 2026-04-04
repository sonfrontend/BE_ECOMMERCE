using System.Security.Claims;

using BE_ECOMMERCE.Data;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_ECOMMERCE.Controllers;


[Route("api/[controller]")]
[ApiController]
internal class UserController(ApplicationDbContext context, IConfiguration config) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    // private readonly IConfiguration _config = config;

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Unauthorized();
        }

        Entities.Auth.User? user = await _context.Users.FindAsync(Guid.Parse(userId));
        return user == null
            ? NotFound()
            : Ok(new
            {
                user.UserId,
                user.UserName,
                user.Email,
                user.GoogleId
            });
    }

}