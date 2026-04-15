using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using dotnetapi.Models;

namespace dotnetapi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly MySqlConnection _connection;

    public UsersController(MySqlConnection connection)
    {
        _connection = connection;
    }

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        var users = new List<User>();
        await _connection.OpenAsync();
        using var command = new MySqlCommand("SELECT id, username, name, email FROM users", _connection);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Name = reader.GetString(2),
                Email = reader.GetString(3)
            });
        }
        await _connection.CloseAsync();
        return users;
    }

    // GET: api/users/5
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        User? user = null;
        await _connection.OpenAsync();
        using var command = new MySqlCommand("SELECT id, username, name, email FROM users WHERE id = @id", _connection);
        command.Parameters.AddWithValue("@id", id);
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            user = new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Name = reader.GetString(2),
                Email = reader.GetString(3)
            };
        }
        await _connection.CloseAsync();
        if (user == null)
        {
            return NotFound();
        }
        return user;
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<User>> PostUser(User user)
    {
        try
        {
            await _connection.OpenAsync();
            using var command = new MySqlCommand("INSERT INTO users (username, name, email, password) VALUES (@username, @name, @email, @password); SELECT LAST_INSERT_ID();", _connection);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@name", user.Name);
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@password", BCrypt.Net.BCrypt.HashPassword(user.Password));
            var id = Convert.ToInt32(await command.ExecuteScalarAsync());
            user.Id = id;
            await _connection.CloseAsync();
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            return BadRequest(new { Message = "Username or email already exists." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while creating user.", Error = ex.Message });
        }
    }

    // PUT: api/users/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutUser(int id, User user)
    {
        if (id != user.Id)
        {
            return BadRequest();
        }

        try
        {
            await _connection.OpenAsync();
            using var command = new MySqlCommand("UPDATE users SET username = @username, name = @name, email = @email, password = @password WHERE id = @id", _connection);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@name", user.Name);
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@password", BCrypt.Net.BCrypt.HashPassword(user.Password));
            command.Parameters.AddWithValue("@id", id);
            var rowsAffected = await command.ExecuteNonQueryAsync();
            await _connection.CloseAsync();

            if (rowsAffected == 0)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            return BadRequest(new { Message = "Username or email already exists." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while updating user.", Error = ex.Message });
        }
    }

    // DELETE: api/users/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            await _connection.OpenAsync();
            using var command = new MySqlCommand("DELETE FROM users WHERE id = @id", _connection);
            command.Parameters.AddWithValue("@id", id);
            var rowsAffected = await command.ExecuteNonQueryAsync();
            await _connection.CloseAsync();

            if (rowsAffected == 0)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while deleting user.", Error = ex.Message });
        }
    }
}