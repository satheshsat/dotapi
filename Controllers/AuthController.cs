using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using dotapi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BCrypt.Net;

namespace dotapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MySqlConnection _connection;
    private readonly IConfiguration _configuration;

    public AuthController(MySqlConnection connection, IConfiguration configuration)
    {
        _connection = connection;
        _configuration = configuration;
    }

    // POST: api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Check if user exists
        await _connection.OpenAsync();
        using var checkCommand = new MySqlCommand("SELECT COUNT(*) FROM users WHERE username = @username OR email = @email", _connection);
        checkCommand.Parameters.AddWithValue("@username", request.Username);
        checkCommand.Parameters.AddWithValue("@email", request.Email);
        var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
        if (count > 0)
        {
            await _connection.CloseAsync();
            return BadRequest(new { Message = "Username or email already exists." });
        }

        // Hash password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Insert user
        using var insertCommand = new MySqlCommand("INSERT INTO users (username, name, email, password) VALUES (@username, @name, @email, @password); SELECT LAST_INSERT_ID();", _connection);
        insertCommand.Parameters.AddWithValue("@username", request.Username);
        insertCommand.Parameters.AddWithValue("@name", request.Name);
        insertCommand.Parameters.AddWithValue("@email", request.Email);
        insertCommand.Parameters.AddWithValue("@password", hashedPassword);
        var id = Convert.ToInt32(await insertCommand.ExecuteScalarAsync());
        await _connection.CloseAsync();

        return Ok(new { Message = "User registered successfully.", UserId = id });
    }

    // POST: api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        await _connection.OpenAsync();
        using var command = new MySqlCommand("SELECT id, username, name, email, password FROM users WHERE username = @username", _connection);
        command.Parameters.AddWithValue("@username", request.Username);
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            await _connection.CloseAsync();
            return Unauthorized("Invalid username or password.");
        }

        var user = new User
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            Name = reader.GetString(2),
            Email = reader.GetString(3),
            Password = reader.GetString(4)
        };
        await _connection.CloseAsync();

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            return Unauthorized("Invalid username or password.");
        }

        // Generate JWT
        var token = GenerateJwtToken(user);
        return Ok(new { Token = token, User = new { user.Id, user.Username, user.Name, user.Email } });
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("id", user.Id.ToString()),
            new Claim("username", user.Username)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}