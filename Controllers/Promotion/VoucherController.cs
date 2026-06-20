using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Entities.Promotion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;

namespace BE_ECOMMERCE.Controllers.Promotion
{
    [Route("api/[controller]")]
    [ApiController]
    public class VoucherController(ApplicationDbContext context) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetActiveVouchers()
        {
            var query = _context.Vouchers.Where(v => v.IsActived && v.Quantity > 0 && v.EndDate >= DateTime.Now).AsQueryable();

            Guid? userId = null;
            var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            
            if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out var parsedId))
            {
                userId = parsedId;
            }
            else
            {
                // Thử lấy từ header nếu middleware không tự động parse (vì thiếu [Authorize])
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader != null && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwtToken = handler.ReadJwtToken(token);
                        var idClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier || c.Type == "sub");
                        if (idClaim != null && Guid.TryParse(idClaim.Value, out var extractedId))
                        {
                            userId = extractedId;
                        }
                    }
                }
            }

            if (userId.HasValue)
            {
                var savedVoucherIds = await _context.UserVouchers
                    .Where(uv => uv.UserId == userId.Value)
                    .Select(uv => uv.VoucherId)
                    .ToListAsync();

                if (savedVoucherIds.Any())
                {
                    query = query.Where(v => !savedVoucherIds.Contains(v.Id));
                }
            }

            var vouchers = await query.ToListAsync();
            return Ok(vouchers);
        }

        [HttpGet("my-vouchers")]
        [Authorize]
        public async Task<IActionResult> GetMyVouchers()
        {
            var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var userVouchers = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == userId && !uv.IsUsed && uv.Voucher.IsActived)
                .Select(uv => uv.Voucher) // Trả về thông tin Voucher gốc để frontend tiện hiển thị
                .ToListAsync();

            return Ok(userVouchers);
        }

        [HttpGet("my-all-vouchers")]
        [Authorize]
        public async Task<IActionResult> GetMyAllVouchers()
        {
            var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var userVouchers = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .Where(uv => uv.UserId == userId)
                .OrderByDescending(uv => uv.Id)
                .Select(uv => new {
                    uv.Id,
                    uv.IsUsed,
                    uv.OrderId,
                    Voucher = uv.Voucher
                })
                .ToListAsync();

            return Ok(userVouchers);
        }

        [HttpPost("save/{templateId}")]
        [Authorize]
        public async Task<IActionResult> SaveVoucher(int templateId)
        {
            var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var template = await _context.Vouchers.FindAsync(templateId);
            if (template == null || !template.IsActived)
            {
                return BadRequest("Mã voucher không hợp lệ hoặc đã bị khóa.");
            }

            if (template.EndDate < DateTime.Now)
            {
                return BadRequest("Mã giảm giá này đã hết hạn.");
            }

            // Kiểm tra xem user đã lưu mã này chưa
            var existingUserVoucher = await _context.UserVouchers
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.VoucherId == templateId);

            if (existingUserVoucher != null)
            {
                return BadRequest("Bạn đã lưu mã giảm giá này rồi.");
            }

            if (template.Quantity <= 0)
            {
                return BadRequest("Mã giảm giá này đã được phát hết.");
            }

            template.Quantity -= 1;

            var userVoucher = new UserVoucher
            {
                UserId = userId,
                VoucherId = templateId,
                IsUsed = false
            };

            _context.UserVouchers.Add(userVoucher);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Lưu mã giảm giá thành công!", voucher = template });
        }

        // API cho Khách hàng: Kiểm tra tính hợp lệ của Voucher và trả về số tiền được giảm
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateVoucher([FromBody] ValidateVoucherRequest request)
        {
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == request.Code);

            if (voucher == null)
            {
                return BadRequest("Mã giảm giá không tồn tại.");
            }

            if (!voucher.IsActived)
            {
                return BadRequest("Mã giảm giá đã bị khóa.");
            }

            if (voucher.EndDate < DateTime.Now)
            {
                return BadRequest("Mã giảm giá đã hết hạn.");
            }

            if (voucher.StartDate > DateTime.Now)
            {
                return BadRequest("Mã giảm giá chưa đến thời gian áp dụng.");
            }

            var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var currentUserId))
            {
                return Unauthorized("Vui lòng đăng nhập để sử dụng mã giảm giá.");
            }

            var userVoucher = await _context.UserVouchers
                .FirstOrDefaultAsync(uv => uv.UserId == currentUserId && uv.VoucherId == voucher.Id);

            if (userVoucher == null)
            {
                return BadRequest("Bạn chưa lưu mã giảm giá này trong ví.");
            }

            if (userVoucher.IsUsed)
            {
                return BadRequest("Mã giảm giá này đã được sử dụng.");
            }

            if (request.OrderTotal < voucher.MinOrderValue)
            {
                return BadRequest($"Đơn hàng tối thiểu để áp dụng mã này là {voucher.MinOrderValue:N0}đ.");
            }

            // Tính số tiền được giảm
            decimal discountAmount = voucher.DiscountValue;

            // Số tiền giảm không được vượt quá tổng tiền
            if (discountAmount > request.OrderTotal)
            {
                discountAmount = request.OrderTotal;
            }

            return Ok(new
            {
                discountAmount = discountAmount,
                message = "Áp dụng mã giảm giá thành công!"
            });
        }

        // --- CÁC API DÀNH CHO ADMIN ---

        [HttpGet("admin")]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllVouchersForAdmin()
        {
            var vouchers = await _context.Vouchers.OrderByDescending(v => v.CreatedAt).ToListAsync();
            
            var voucherIds = vouchers.Select(v => v.Id).ToList();
            var assignedCounts = await _context.UserVouchers
                .Where(uv => voucherIds.Contains(uv.VoucherId))
                .GroupBy(uv => uv.VoucherId)
                .Select(g => new { VoucherId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.VoucherId, g => g.Count);

            var result = vouchers.Select(v => new {
                v.Id,
                v.Code,
                v.DiscountValue,
                v.MinOrderValue,
                v.StartDate,
                v.EndDate,
                v.IsActived,
                v.CreatedAt,
                v.UpdatedAt,
                Quantity = v.Quantity, // Keep it for compatibility (remaining quantity)
                RemainingQuantity = v.Quantity,
                GivenQuantity = assignedCounts.ContainsKey(v.Id) ? assignedCounts[v.Id] : 0,
                TotalQuantity = v.Quantity + (assignedCounts.ContainsKey(v.Id) ? assignedCounts[v.Id] : 0)
            });

            return Ok(result);
        }

        [HttpPost("admin")]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateVoucher([FromBody] Voucher model)
        {
            // Kiểm tra trùng lặp
            if (await _context.Vouchers.AnyAsync(v => v.Code == model.Code))
            {
                return BadRequest("Mã Code này đã tồn tại!");
            }

            model.Code = model.Code.ToUpper();
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;

            _context.Vouchers.Add(model);
            await _context.SaveChangesAsync();

            return Ok(model);
        }

        [HttpPut("admin/{id}")]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateVoucher(int id, [FromBody] Voucher model)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null) return NotFound("Không tìm thấy Voucher");

            // Nếu sửa Code, kiểm tra trùng lặp (trừ chính nó)
            if (await _context.Vouchers.AnyAsync(v => v.Code == model.Code && v.Id != id))
            {
                return BadRequest("Mã Code này đã được sử dụng cho Voucher khác!");
            }

            voucher.Code = model.Code.ToUpper();
            voucher.DiscountValue = model.DiscountValue;
            voucher.MinOrderValue = model.MinOrderValue;
            voucher.Quantity = model.Quantity;
            voucher.StartDate = model.StartDate;
            voucher.EndDate = model.EndDate;
            voucher.IsActived = model.IsActived;
            voucher.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(voucher);
        }

        [HttpDelete("admin/{id}")]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteVoucher(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null) return NotFound("Không tìm thấy Voucher");

            _context.Vouchers.Remove(voucher);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa Voucher thành công" });
        }

        [HttpGet("admin/{id}/users")]
        public async Task<IActionResult> GetVoucherUsers(int id)
        {
            var userVouchers = await _context.UserVouchers
                .Include(uv => uv.User)
                .Where(uv => uv.VoucherId == id)
                .Select(uv => new {
                    uv.Id,
                    uv.UserId,
                    uv.IsUsed,
                    UserName = uv.User.FullName,
                    Email = uv.User.Email
                })
                .ToListAsync();

            return Ok(userVouchers);
        }

        [HttpPost("admin/{id}/assign/{userId}")]
        public async Task<IActionResult> AssignVoucherToUser(int id, string userId)
        {
            if (!Guid.TryParse(userId, out var parsedUserId)) return BadRequest("User ID không hợp lệ");

            var template = await _context.Vouchers.FindAsync(id);
            if (template == null) return NotFound("Voucher không tồn tại");

            var existingUserVoucher = await _context.UserVouchers
                .FirstOrDefaultAsync(uv => uv.UserId == parsedUserId && uv.VoucherId == id);

            if (existingUserVoucher != null) return BadRequest("Người dùng này đã sở hữu Voucher này.");

            if (template.Quantity <= 0) return BadRequest("Voucher này đã được phát hết.");

            template.Quantity -= 1;

            var userVoucher = new UserVoucher
            {
                UserId = parsedUserId,
                VoucherId = id,
                IsUsed = false
            };

            _context.UserVouchers.Add(userVoucher);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cấp phát Voucher thành công!" });
        }

        [HttpDelete("admin/{id}/revoke/{userId}")]
        public async Task<IActionResult> RevokeVoucherFromUser(int id, string userId)
        {
            if (!Guid.TryParse(userId, out var parsedUserId)) return BadRequest("User ID không hợp lệ");

            var userVoucher = await _context.UserVouchers
                .Include(uv => uv.Voucher)
                .FirstOrDefaultAsync(uv => uv.UserId == parsedUserId && uv.VoucherId == id);

            if (userVoucher == null) return NotFound("Người dùng này không có Voucher này.");

            if (userVoucher.IsUsed) return BadRequest("Không thể thu hồi vì Voucher đã được người dùng sử dụng.");

            if (userVoucher.Voucher != null)
            {
                userVoucher.Voucher.Quantity += 1;
            }

            _context.UserVouchers.Remove(userVoucher);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thu hồi Voucher thành công!" });
        }
    }

    public class ValidateVoucherRequest
    {
        public string Code { get; set; }
        public decimal OrderTotal { get; set; }
    }
}
