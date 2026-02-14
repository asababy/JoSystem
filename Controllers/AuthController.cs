using System;
using System.Linq;
using System.Threading.Tasks;
using JoSystem.Data;
using JoSystem.Models.DTOs;
using JoSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JoSystem.Controllers
{
    [ApiController]
    [Route("api")]
    public class AuthController : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest body)
        {
            if (body == null || string.IsNullOrEmpty(body.Username) || string.IsNullOrEmpty(body.Password))
            {
                return BadRequest(new { success = false, message = "账号或密码不能为空" });
            }

            bool success = await VerifyDatabaseUserAsync(body.Username, body.Password);

            if (success)
            {
                var cookieOptions = new Microsoft.AspNetCore.Http.CookieOptions
                {
                    Path = "/",
                    HttpOnly = false,
                    Expires = DateTimeOffset.Now.AddDays(1),
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax
                };

                Response.Cookies.Append("IsLoggedIn", "true", cookieOptions);
                Response.Cookies.Append("Username", body.Username, cookieOptions);

                LogService.Write("用户登录成功", body.Username);
                return Ok(new { success = true, message = "登录成功" });
            }

            LogService.Write("用户登录失败: 密码错误", body?.Username);
            return Ok(new { success = false, message = "账号或密码错误" });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            string username = Request.Cookies["Username"];
            LogService.Write("用户退出登录", username);

            Response.Cookies.Delete("IsLoggedIn");
            Response.Cookies.Delete("Username");

            return Ok(new { success = true });
        }

        [HttpGet("auth")]
        public IActionResult Auth()
        {
            bool authenticated = Request.Cookies["IsLoggedIn"] == "true";
            string username = Request.Cookies["Username"];
            if (authenticated && string.IsNullOrEmpty(username)) username = "admin";

            return Ok(new
            {
                authenticated,
                username = authenticated ? username : null
            });
        }

        private static async Task<bool> VerifyDatabaseUserAsync(string username, string password)
        {
            try
            {
                await using var db = new AppDbContext();
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username);
                return user != null && DbService.VerifyPassword(password, user.PasswordHash);
            }
            catch (Exception dbEx)
            {
                LogService.WriteError($"DB Login Error: {dbEx.Message}");
                return false;
            }
        }
    }
}
