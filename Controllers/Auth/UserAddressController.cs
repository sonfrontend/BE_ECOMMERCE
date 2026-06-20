using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Entities.Auth;

namespace BE_ECOMMERCE.Controllers.Auth;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UserAddressController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UserAddressController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyAddresses()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var addresses = await _context.UserAddresses
            .Where(a => a.UserId == userId && a.IsActived)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(addresses);
    }

    [HttpPost]
    public async Task<IActionResult> AddAddress([FromBody] UserAddress request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        // Check if it's the first address
        var hasAddresses = await _context.UserAddresses.AnyAsync(a => a.UserId == userId && a.IsActived);
        
        if (!hasAddresses || request.IsDefault)
        {
            // Set all other addresses to not default
            var existingDefaults = await _context.UserAddresses.Where(a => a.UserId == userId && a.IsDefault).ToListAsync();
            foreach (var addr in existingDefaults)
            {
                addr.IsDefault = false;
            }
        }

        var address = new UserAddress
        {
            UserId = userId,
            RecipientName = request.RecipientName,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            Email = request.Email,
            IsDefault = !hasAddresses || request.IsDefault,
            CreatedAt = DateTime.UtcNow,
            IsActived = true
        };

        _context.UserAddresses.Add(address);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Thêm địa chỉ thành công", address });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAddress(int id, [FromBody] UserAddress request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var address = await _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && a.IsActived);
        if (address == null) return NotFound("Không tìm thấy địa chỉ");

        address.RecipientName = request.RecipientName;
        address.PhoneNumber = request.PhoneNumber;
        address.Address = request.Address;
        address.Email = request.Email;
        address.UpdatedAt = DateTime.UtcNow;

        if (request.IsDefault && !address.IsDefault)
        {
            var existingDefaults = await _context.UserAddresses.Where(a => a.UserId == userId && a.IsDefault).ToListAsync();
            foreach (var addr in existingDefaults)
            {
                addr.IsDefault = false;
            }
            address.IsDefault = true;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Cập nhật địa chỉ thành công", address });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var address = await _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && a.IsActived);
        if (address == null) return NotFound("Không tìm thấy địa chỉ");

        address.IsActived = false;

        // If we deleted the default, set another one to default if exists
        if (address.IsDefault)
        {
            address.IsDefault = false;
            var nextAddress = await _context.UserAddresses.FirstOrDefaultAsync(a => a.UserId == userId && a.IsActived && a.Id != id);
            if (nextAddress != null)
            {
                nextAddress.IsDefault = true;
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Xóa địa chỉ thành công" });
    }

    [HttpPut("{id}/set-default")]
    public async Task<IActionResult> SetDefaultAddress(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var address = await _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && a.IsActived);
        if (address == null) return NotFound("Không tìm thấy địa chỉ");

        var existingDefaults = await _context.UserAddresses.Where(a => a.UserId == userId && a.IsDefault).ToListAsync();
        foreach (var addr in existingDefaults)
        {
            addr.IsDefault = false;
        }

        address.IsDefault = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật địa chỉ mặc định thành công" });
    }
}
