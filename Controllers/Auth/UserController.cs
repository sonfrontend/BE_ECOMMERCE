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
    private readonly IConfiguration _config = config;

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _context.Users
            .Select(u => new
            {
                u.UserId,
                u.UserName,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                Role = _context.UserRoles
                        .Where(ur => ur.UserId == u.UserId)
                        .Join(_context.Roles, ur => ur.RoleId, r => r.RoleId, (ur, r) => r.RoleName)
                        .FirstOrDefault() ?? "Người dùng"
            })
            .ToListAsync();
        return Ok(users);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "Không tìm thấy người dùng!" });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã xóa người dùng thành công!" });
    }

    [HttpGet("userRole")]
    [Authorize]
    public async Task<IActionResult> GetAllUserRoles()
    {
        var userRoles = await _context.UserRoles
            .Select(ur => new
            {
                ur.UserRoleId,
                ur.UserId,
                ur.RoleId
            })
            .ToListAsync();
        return Ok(userRoles);
    }

    [HttpPost("userRole")]
    [Authorize]
    public async Task<IActionResult> CreateUserRole([FromBody] Entities.Auth.UserRole request)
    {
        var exists = await _context.UserRoles.AnyAsync(ur => ur.UserId == request.UserId && ur.RoleId == request.RoleId);
        if (exists)
            return BadRequest(new { message = "Dữ liệu này đã tồn tại!" });

        request.UserRoleId = Guid.NewGuid();
        _context.UserRoles.Add(request);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Thêm dữ liệu thành công!", userRole = request });
    }

    [HttpDelete("userRole/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteUserRole(Guid id)
    {
        var userRole = await _context.UserRoles.FindAsync(id);
        if (userRole == null)
            return NotFound(new { message = "Không tìm thấy dữ liệu!" });

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Xóa dữ liệu thành công!" });
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Unauthorized();
        }

        Entities.Auth.User user = await _context.Users.FindAsync(Guid.Parse(userId));
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
        string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Unauthorized();
        }

        Entities.Auth.User user = await _context.Users.FindAsync(Guid.Parse(userId));
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
            if (string.IsNullOrEmpty(request.OldPassword))
            {
                return BadRequest(new { message = "Vui lòng nhập mật khẩu cũ!" });
            }
            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Mật khẩu cũ không chính xác!" });
            }
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