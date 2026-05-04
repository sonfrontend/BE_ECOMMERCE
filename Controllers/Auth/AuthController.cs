using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography; // Thêm thư viện này lên đầu file
using System.Text;
using System.Text.RegularExpressions;

using BE_ECOMMERCE.Data; // Thay bằng namespace AppDbContext của bạn
using BE_ECOMMERCE.DTOs.Auths;
using BE_ECOMMERCE.Entities.Auth;

using Google.Apis.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        User? user = _context.Users.FirstOrDefault(u => u.UserName == request.UserName);
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
            },
            message = "Đăng nhập thành công với userName, password!"
        });

    }


    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {

        User? user = _context.Users.FirstOrDefault(u => u.UserName == request.UserName);
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
        _ = await _context.SaveChangesAsync();

        return Ok(new { message = "Đăng ký thành công!" });
    }


    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        User? user = _context.Users.FirstOrDefault(u => u.RefreshToken == request.RefreshToken);

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
            // 1. Lấy Google Client ID từ appsettings.json ra để làm mốc đối chiếu
            string? clientId = _config["Google:ClientId"];

            if (string.IsNullOrEmpty(clientId))
            {
                return StatusCode(500, new { message = "Chưa cấu hình Google ClientId trên Server" });
            }

            GoogleJsonWebSignature.ValidationSettings settings = new()
            {
                Audience = [clientId],
            };

            // 2. Ném ID Token của React gửi lên cho Google kiểm tra
            // Nếu Token bị sửa đổi hoặc hết hạn, dòng này sẽ văng lỗi ngay!
            GoogleJsonWebSignature.Payload payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);

            // 3. Nếu hàng chuẩn, móc Email ra và tìm xem người này từng vào hệ thống chưa
            User? user = _context.Users.FirstOrDefault(u => u.Email == payload.Email);

            string newRefreshToken = GenerateRefreshToken();

            // 4. CHƯA CÓ TÀI KHOẢN? -> Tự động đăng ký luôn không cần hỏi!
            if (user == null)
            {
                Guid userId = Guid.NewGuid();
                user = new User // (Tên class model Users của bạn)
                {
                    UserId = userId, // Tự sinh ID ngay ở code
                    FullName = payload.Name, // Lấy luôn tên Google làm tên hiển thị
                    Email = payload.Email,
                    GoogleId = payload.Subject,
                    PasswordHash = "", // Đăng nhập Google thì mật khẩu để trống
                };

                string accessToken = CreateToken(user);
                _ = _context.Users.Add(user);
                user.RefreshToken = newRefreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                _ = await _context.SaveChangesAsync();

                return Ok(new
                {
                    accessToken,
                    refreshToken = newRefreshToken,
                    userInfo = new
                    {
                        id = userId,
                        fullName = payload.Name,
                        email = payload.Email,
                        googleId = payload.Subject,
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
                    },
                    message = "Đăng nhập bằng Google thành công!"
                });
            }
        }
        catch (InvalidJwtException ex)
        {
            // Bắt lỗi nếu React gửi lên Token tào lao
            // return Unauthorized(new { message = "Token Google không hợp lệ hoặc đã hết hạn!" });

            return Unauthorized(new
            {
                message = "Lỗi từ Google: " + ex.Message,
                chi_tiet = "Token bị từ chối tại hàm ValidateAsync"
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
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Email, user.Email),
        ];

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