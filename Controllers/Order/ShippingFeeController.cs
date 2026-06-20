using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Entities.Order;

namespace BE_ECOMMERCE.Controllers.Order;

[Route("api/[controller]")]
[ApiController]
public class ShippingFeeController(ApplicationDbContext context) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAllShippingFees()
    {
        var fees = await _context.ShippingFees.ToListAsync();
        return Ok(fees);
    }

    [HttpPost]
    public async Task<IActionResult> CreateShippingFee([FromBody] ShippingFee request)
    {
        if (request.Fee < 0 || request.Fee > 50000)
        {
            return BadRequest("Phí vận chuyển phải từ 0 đến 50.000 VNĐ.");
        }

        var existing = await _context.ShippingFees.FirstOrDefaultAsync(s => s.ProvinceName == request.ProvinceName);
        if (existing != null)
        {
            return BadRequest("Tỉnh thành này đã được cấu hình phí vận chuyển.");
        }

        _context.ShippingFees.Add(request);
        await _context.SaveChangesAsync();

        return Ok(request);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateShippingFee(int id, [FromBody] ShippingFee request)
    {
        if (request.Fee < 0 || request.Fee > 50000)
        {
            return BadRequest("Phí vận chuyển phải từ 0 đến 50.000 VNĐ.");
        }

        var fee = await _context.ShippingFees.FindAsync(id);
        if (fee == null) return NotFound("Không tìm thấy cấu hình phí.");

        // Check duplicate name
        var duplicate = await _context.ShippingFees.FirstOrDefaultAsync(s => s.ProvinceName == request.ProvinceName && s.Id != id);
        if (duplicate != null)
        {
            return BadRequest("Tỉnh thành này đã được cấu hình phí vận chuyển.");
        }

        fee.ProvinceName = request.ProvinceName;
        fee.Fee = request.Fee;

        await _context.SaveChangesAsync();
        return Ok(fee);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteShippingFee(int id)
    {
        var fee = await _context.ShippingFees.FindAsync(id);
        if (fee == null) return NotFound();

        _context.ShippingFees.Remove(fee);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Xóa thành công" });
    }
}
