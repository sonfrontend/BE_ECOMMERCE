using System.Security.Claims;
using System.Text.RegularExpressions;

using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.DTOs.Users;

using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BE_ECOMMERCE.Controllers;

[Route("api/[controller]")] // Bắt buộc phải có
[ApiController]             // Bắt buộc phải có
public class UserController(ApplicationDbContext context, IConfiguration config) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    // private readonly IConfiguration _config = config;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _context.Users
            .Select(u => new {
                u.UserId,
                u.UserName,
                u.FullName
            })
            .ToListAsync();
        return Ok(users);
    }

    [HttpGet("userRole")]
    [Authorize]
    public async Task<IActionResult> GetAllUserRoles()
    {
        var userRoles = await _context.UserRoles
            .Select(ur => new {
                ur.UserRoleId,
                ur.UserId,
                ur.RoleId
            })
            .ToListAsync();
        return Ok(userRoles);
    }

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

    [HttpPut("update-profile")]
    [Authorize]
    [EnableRateLimiting("Giới_Hạn_Spam_Click")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Unauthorized();
        }

        Entities.Auth.User? user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null)
        {
            return NotFound();
        }

        user.UserName = request.UserName;
        user.FullName = request.FullName;
        user.Email = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.AvatarUrl = request.AvatarUrl;

        if (string.IsNullOrEmpty(user.GoogleId) && !string.IsNullOrEmpty(request.Password))
        {
            if (!Regex.IsMatch(request.Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)"))
            {
                return BadRequest(new { message = "Mật khẩu phải bao gồm chữ hoa, chữ thường, số!" });
            }
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            user.UserId,
            user.UserName,
            user.FullName,
            user.Email,
            user.GoogleId,
            user.PhoneNumber,
            user.AvatarUrl
        });
    }


}