using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography; // Thêm thư viện này lên đầu file
using System.Text;
using System.Text.RegularExpressions;

using BE_ECOMMERCE.Data; // Thay bằng namespace AppDbContext của bạn
using BE_ECOMMERCE.DTOs.Auths;
using BE_ECOMMERCE.Entities.Auth;

using FirebaseAdmin.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;


namespace BE_ECOMMERCE.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(ApplicationDbContext context, IConfiguration config) : ControllerBase
{
    private readonly ApplicationDbContext _context = context;
    private readonly IConfiguration _config = config;

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {


        bool isPasswordValid = false;
        User user = _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefault(u => u.UserName == request.UserName);
        if (user == null)
        {
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng!" });
        }
        else
        {
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                return Unauthorized(new { message = "Tài khoản này được đăng ký bằng Google, vui lòng đăng nhập bằng Google!" });
            }
            isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }

        if (!isPasswordValid)
        {
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng!" });
        }

        // 2. Chế tạo Token
        string accessToken = CreateToken(user);
        string refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        _ = _context.SaveChanges();

        // 3. Trả về cho Swagger / React
        return Ok(new
        {
            accessToken,
            refreshToken,
            userInfo = new
            {
                id = user.UserId,
                userName = user.UserName,
                email = user.Email,
                fullName = user.FullName,
                avatarUrl = user.AvatarUrl,
                googleId = user.GoogleId,
                phoneNumber = user.PhoneNumber,
                roles = user.UserRoles?.Select(ur => ur.Role?.RoleName).Where(r => r != null).ToList() ?? new List<string>()
            },
            message = "Đăng nhập thành công với userName, password!"
        });

    }


    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {

        User user = _context.Users.FirstOrDefault(u => u.UserName == request.UserName);
        if (user != null)
        {
            return BadRequest(new { message = "Tên đăng nhập đã tồn tại!" });
        }

        // Kiểm tra xem Email đã tồn tại chưa
        bool isEmailExist = _context.Users.Any(u => u.Email == request.Email);
        if (isEmailExist)
        {
            return BadRequest(new { message = "Email này đã được sử dụng!" });
        }

        // Check password
        if (!Regex.IsMatch(request.Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)"))
        {
            return BadRequest(new { message = "Mật khẩu phải bao gồm chữ hoa, chữ thường, số!" });
        }

        // 🎯 ĐÂY LÀ PHÉP THUẬT CỦA BCRYPT: Băm mật khẩu
        // Ví dụ user gõ "123456", biến này sẽ biến thành chuỗi: "$2a$11$Kk3/..."
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        User newUser = new()
        {
            UserId = Guid.NewGuid(), // Tự sinh ID ngay ở code
            UserName = request.UserName,
            Email = request.Email,
            PasswordHash = hashedPassword, // Lưu chuỗi loằng ngoằng này vào Database!
            GoogleId = null // Đăng ký tay thì không có GoogleId
        };

        _ = _context.Users.Add(newUser);

        // Tạo 2 Voucher chào mừng cho User mới
        var welcomeVoucher1 = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == "WELCOME10K");
        var welcomeVoucher2 = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == "SUMMER20K");

        if (welcomeVoucher1 != null)
        {
            _context.UserVouchers.Add(new BE_ECOMMERCE.Entities.Promotion.UserVoucher
            {
                UserId = newUser.UserId,
                VoucherId = welcomeVoucher1.Id,
                IsUsed = false
            });
        }
        if (welcomeVoucher2 != null)
        {
            _context.UserVouchers.Add(new BE_ECOMMERCE.Entities.Promotion.UserVoucher
            {
                UserId = newUser.UserId,
                VoucherId = welcomeVoucher2.Id,
                IsUsed = false
            });
        }

        _ = await _context.SaveChangesAsync();

        return Ok(new { message = "Đăng ký thành công!" });
    }


    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        User user = _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefault(u => u.RefreshToken == request.RefreshToken);

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Refresh Token không hợp lệ hoặc đã hết hạn!" });
        }

        // Tạo Token mới
        string newAccessToken = CreateToken(user);
        string newRefreshToken = GenerateRefreshToken();

        // Cập nhật vào DB
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        _ = await _context.SaveChangesAsync();

        return Ok(new
        {
            accessToken = newAccessToken,
            refreshToken = newRefreshToken,
            message = "Token đã được làm mới thành công!"
        });
    }


    [AllowAnonymous]
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            // Xác thực Firebase ID Token thay vì dùng Google API gốc
            FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);

            string email = decodedToken.Claims.ContainsKey("email") ? decodedToken.Claims["email"].ToString() : "";
            string name = decodedToken.Claims.ContainsKey("name") ? decodedToken.Claims["name"].ToString() : "";
            string googleId = decodedToken.Uid;

            // 3. Nếu hàng chuẩn, móc Email ra và tìm xem người này từng vào hệ thống chưa
            User user = _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefault(u => u.Email == email);

            string newRefreshToken = GenerateRefreshToken();

            // 4. CHƯA CÓ TÀI KHOẢN? -> Tự động đăng ký luôn không cần hỏi!
            if (user == null)
            {
                Guid userId = Guid.NewGuid();
                string baseUserName = email.Split('@')[0];
                string uniqueUserName = $"{baseUserName}_{Guid.NewGuid().ToString("N").Substring(0, 4)}";

                user = new User // (Tên class model Users của bạn)
                {
                    UserId = userId, // Tự sinh ID ngay ở code
                    UserName = uniqueUserName, // Cần có UserName vì required
                    FullName = name, // Lấy luôn tên Google làm tên hiển thị
                    Email = email,
                    GoogleId = googleId,
                    PasswordHash = "", // Đăng nhập Google thì mật khẩu để trống
                };

                string accessToken = CreateToken(user);
                _ = _context.Users.Add(user);
                user.RefreshToken = newRefreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

                // Tạo 2 Voucher chào mừng cho User mới
                var welcomeVoucher1 = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == "WELCOME10K");
                var welcomeVoucher2 = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == "SUMMER20K");

                if (welcomeVoucher1 != null)
                {
                    _context.UserVouchers.Add(new BE_ECOMMERCE.Entities.Promotion.UserVoucher
                    {
                        UserId = user.UserId,
                        VoucherId = welcomeVoucher1.Id,
                        IsUsed = false
                    });
                }
                if (welcomeVoucher2 != null)
                {
                    _context.UserVouchers.Add(new BE_ECOMMERCE.Entities.Promotion.UserVoucher
                    {
                        UserId = user.UserId,
                        VoucherId = welcomeVoucher2.Id,
                        IsUsed = false
                    });
                }

                _ = await _context.SaveChangesAsync();

                return Ok(new
                {
                    accessToken,
                    refreshToken = newRefreshToken,
                    userInfo = new
                    {
                        id = userId,
                        userName = uniqueUserName,
                        fullName = name,
                        email = email,
                        googleId = googleId,
                        roles = user.UserRoles?.Select(ur => ur.Role?.RoleName).Where(r => r != null).ToList() ?? new List<string>()
                    },
                    message = "Đăng nhập bằng Google thành công!"
                });
            }
            else
            {

                string accessToken = CreateToken(user);
                user.RefreshToken = newRefreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                _ = await _context.SaveChangesAsync();

                return Ok(new
                {
                    accessToken,
                    refreshToken = newRefreshToken,
                    userInfo = new
                    {
                        id = user.UserId,
                        fullName = user.FullName,
                        userName = user.UserName,
                        email = user.Email,
                        googleId = user.GoogleId,
                        phoneNumber = user.PhoneNumber,
                        roles = user.UserRoles?.Select(ur => ur.Role?.RoleName).Where(r => r != null).ToList() ?? new List<string>()
                    },
                    message = "Đăng nhập bằng Google thành công!"
                });
            }
        }
        catch (FirebaseAuthException ex)
        {
            // Bắt lỗi nếu React gửi lên Token tào lao
            // return Unauthorized(new { message = "Token Google không hợp lệ hoặc đã hết hạn!" });

            return Unauthorized(new
            {
                message = "Lỗi từ Firebase: " + ex.Message,
                chi_tiet = "Token bị từ chối tại hàm VerifyIdTokenAsync"
            });
        }
        catch (Exception ex)
        {
            // Lỗi hệ thống (database sập, lỗi code...)
            return StatusCode(500, new { message = "Lỗi Server: " + ex.Message });
        }
    }

    private string CreateToken(User user)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        ];

        // Gắn tất cả các Role của User vào Token
        if (user.UserRoles != null)
        {
            foreach (var ur in user.UserRoles)
            {
                if (ur.Role != null)
                {
                    claims.Add(new Claim(ClaimTypes.Role, ur.Role.RoleName));
                }
            }
        }

        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken accessToken = new(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60), // AccessToken thường chỉ nên để 30 - 60 phút
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(accessToken);
    }


    // Class phụ để hứng dữ liệu từ React (bạn viết nó nằm ngoài AuthController, hoặc ở cuối file)




    private string GenerateRefreshToken()
    {
        byte[] randomNumber = new byte[32];
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}