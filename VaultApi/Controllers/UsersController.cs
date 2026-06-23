using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VaultShared;
using Microsoft.EntityFrameworkCore;

namespace VaultApi.Controllers;

[ApiController] [Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;

    public UsersController(ApplicationDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // POST: api/users/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] User user)
    {
        if (string.IsNullOrWhiteSpace(user.UserName))
            return BadRequest("Invalid input data.");

        // Check if user exists
        if (_context.Users.Any(u => u.UserName == user.UserName))
            return BadRequest("Username already exists.");

        // 1. HASH PASSWORD
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash); // Assuming PasswordHash property holds raw password temporarily

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "User registered successfully.", UserId = user.UserID });
    }

    // POST: api/users/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);

        if (user == null)
            return Unauthorized("Invalid credentials.");

        // 2. VERIFY PASSWORD
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");

        // 3. GENERATE JWT TOKEN
        var token = GenerateJwtToken(user);
        
        return Ok(new 
        { 
            Token = token, 
            Role = user.Role, 
            UserName = user.UserName 
        });
    }

    // GET: api/users/admin-dashboard (Protected Route)
    [Authorize(Roles = "Admin")] [HttpGet("admin-dashboard")]
    public IActionResult GetAdminDashboard()
    {
        return Ok(new { Message = "Welcome to the Admin Dashboard! You have full access." });
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _config["Jwt:Key"];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[] {
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("UserId", user.UserID.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool IsValidEmail(string email)
    {
        try {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        } catch { return false; }
    }
}

// Helper class for Login Request
public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}