using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using FlashcardApp.Data;
using FlashcardApp.Models;
using FlashcardApp.ViewModels;
using FlashcardApp.ViewModels.APIViewModels;
using FlashcardApp.DTOs;

namespace FlashcardApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountApiController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration configuration,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        // REGISTER USER
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterAPIViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest(new { message = "A user with this email already exists" });

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                PlainTextPassword = model.Password 
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await EnsureRoleExistsAsync("User");
                await _userManager.AddToRoleAsync(user, "User");
                return Ok(new { message = "Registration successful" });
            }

            return BadRequest(result.Errors);
        }

        // LOGIN USER
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto userLogin)
        {
            Console.WriteLine($"Checking login for: {userLogin.Email}");

            var user = await _userManager.FindByEmailAsync(userLogin.Email);
            if (user == null)
            {
                Console.WriteLine("User not found");
                return Unauthorized("Invalid email or password.");
            }

            if (!await _userManager.CheckPasswordAsync(user, userLogin.Password))
            {
                Console.WriteLine("Incorrect password");
                return Unauthorized("Invalid email or password.");
            }

            Console.WriteLine("User authentication successful");
            var token = GenerateJwtToken(userLogin.Email);
            return Ok(new { Token = token });
        }

        // ADMIN LOGIN
        [HttpPost("admin/login")]
        public async Task<IActionResult> AdminLogin([FromBody] UserLoginDto userLogin)
        {
            Console.WriteLine($"Admin Login Attempt: {userLogin.Email}");

            var user = await _userManager.FindByEmailAsync(userLogin.Email);
            if (user == null)
                return Unauthorized("Invalid credentials.");

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (!isAdmin)
                return Unauthorized("Access denied. Admins only.");

            var token = GenerateJwtToken(userLogin.Email);
            return Ok(new { Token = token });
        }

        // FORGOT PASSWORD
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || user.FirstName != model.FirstName || user.LastName != model.LastName)
                return NotFound("User not found. Please check your details.");

            string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

            if (result.Succeeded)
            {
                user.PlainTextPassword = model.NewPassword;
                await _userManager.UpdateAsync(user);

                await _signInManager.SignOutAsync();
                return Ok(new { message = "Password reset successful." });
            }

            return BadRequest(result.Errors);
        }

        // UPDATE USER PASSWORD
        [Authorize]
        [HttpPut("user/{email}/updatePassword")]
        public async Task<IActionResult> UpdateUserPassword(string email, [FromBody] UpdateUserViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return NotFound("User not found.");

            string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.Password);
            if (result.Succeeded)
            {
                user.PlainTextPassword = model.Password;
                await _userManager.UpdateAsync(user);
                return Ok(new { message = "Password updated successfully" });
            }
            
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return BadRequest(ModelState);
        }

        //  GET USER PROFILE
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userEmail = User.FindFirst("Email")?.Value;
            var user = await _userManager.FindByEmailAsync(userEmail);
            if (user == null) return NotFound("User not found.");

            return Ok(new { user.FirstName, user.LastName, user.Email });
        }

        // DELETE USER
        [Authorize]
        [HttpDelete("user/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded ? Ok("User deleted successfully.") : BadRequest(result.Errors);
        }

        // ADMIN: VIEW ALL USERS
        [Authorize(Roles = "Admin")]
        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetAllUsers()
        {
            return await _userManager.Users.ToListAsync();
        }

        // JWT TOKEN GENERATION
        private string GenerateJwtToken(string email)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? ""));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var user = _userManager.FindByEmailAsync(email).Result;
            var claims = new[]
            {
                new Claim("Email", email),
                new Claim(ClaimTypes.NameIdentifier, user?.Id ?? throw new InvalidOperationException("User not found"))
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(120),
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Ensure role exists before assigning it
        private async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}
