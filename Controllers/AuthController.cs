using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BE_TRELLO.Data; // Thay bằng namespace AppDbContext của bạn
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using BE_TRELLO.Entities.Auth;
using System.Text;

namespace BE_TRELLO.Controllers 
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserName == request.UserName && u.PasswordHash == request.Password);
            if (user == null)
            {
                return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng!" });
            }

            // 2. Chế tạo Token
            var token = CreateToken(user);

            // 3. Trả về cho Swagger / React
            return Ok(new { token = token, message = "Đăng nhập thành công!" });

        }

        [HttpGet("my-profile")]
        [Authorize] // Ổ khóa bắt buộc phải có Token mới được gọi API này
        public IActionResult GetMyProfile()
        {
            // Lấy thông tin User đang đăng nhập từ Token
            // (ClaimTypes.NameIdentifier chính là UserId mà ta đã nhét vào thẻ lúc nãy)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name);

            return Ok(new { 
                Message = "Bạn đã lọt qua được trạm kiểm soát bảo mật!",
                UserId = userId,
                UserName = userName
            });
        }

        private string CreateToken(Users user){
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserName),
                new Claim(ClaimTypes.Email, user.PasswordHash)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class LoginRequest
        {
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }
    }

}