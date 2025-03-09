using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using FlashcardApp.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using FlashcardApp.Services;
using FlashcardApp.Data;
using FlashcardApp.Models;
using FlashcardApp.ViewModels.APIViewModels;
using FlashcardApp.ViewModels;

namespace FlashcardApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly FlashCardService _flashCardService;

        public AccountApiController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            IConfiguration configuration,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            FlashCardService flashCardService)
        {
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _flashCardService = flashCardService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterAPIViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest(new { message = "A user with this email already exists" });

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await EnsureRoleExistsAsync("User");
                await _userManager.AddToRoleAsync(user, "User");
                if (Request.Headers["X-Source"].FirstOrDefault() == "MobileApp")
                {
                    Console.WriteLine($"Registration successful: User {model.Email} registered from MobileApp at {DateTime.Now}");
                }
                return Ok(new { message = "Registration successful" });
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return BadRequest(ModelState);
        }

        private async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto userLogin)
        {
            Console.WriteLine(userLogin.Email);
            var validation = await IsValidUser(userLogin);
            if (validation)
            {
                var token = GenerateJwtToken(userLogin.Email);
                if (Request.Headers["X-Source"].FirstOrDefault() == "MobileApp")
                {
                    Console.WriteLine($"Login successful: User {userLogin.Email} logged in from MobileApp at {DateTime.Now}");
                }
                return Ok(new { Token = token });
            }
            return Unauthorized();
        }

        [HttpPost("admin/login")]
        public async Task<IActionResult> AdminLogin([FromBody] UserLoginDto userLogin)
        {
            var validation = await IsValidUser(userLogin);
            if (validation)
            {
                var user = await _userManager.FindByEmailAsync(userLogin.Email);
                if (user == null || !await _userManager.IsInRoleAsync(user, "Admin"))
                    return Unauthorized("Access denied. Admins only.");

                var token = GenerateJwtToken(userLogin.Email);
                if (Request.Headers["X-Source"].FirstOrDefault() == "MobileApp")
                {
                    Console.WriteLine($"Admin Login successful: User {userLogin.Email} logged in from MobileApp at {DateTime.Now}");
                }
                return Ok(new { Token = token });
            }
            return Unauthorized("Invalid login credentials.");
        }

        private async Task<bool> IsValidUser(UserLoginDto userLogin)
        {
            var result = await _signInManager.PasswordSignInAsync(
                userLogin.Email,
                userLogin.Password,
                false,
                lockoutOnFailure: false);
            return result.Succeeded;
        }

        private string GenerateJwtToken(string email)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? ""));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var user = _userManager.FindByEmailAsync(email).Result;
            var claims = new[]
            {
                new Claim("Email", email ?? throw new ArgumentNullException(nameof(email))),
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

        // [Authorize]
        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<ApplicationUser>>> GetAllUsers()
        {
            return await _userManager.Users.ToListAsync();
        }

        [Authorize]
        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var userViewModel = new UserViewModel { Email = user.Email, FirstName = user.FirstName, LastName = user.LastName };
            return Ok(userViewModel);
        }

        [Authorize]
        [HttpGet("users/{userId}/details")]
        public async Task<IActionResult> GetUserDetails(string userId)
        {
            var user = await _userManager.Users
                .Include(u => u.FlashCards)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound();

            var userDetails = new
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FlashCards = user.FlashCards.Select(f =>
                {
                    var category = _context.Categories.FirstOrDefault(c => c.Id == f.CategoryId); // Removed UserId check
                    string categoryName = category != null ? category.Name : "Unknown";
                    return new FlashCardViewModel
                    {
                        Id = f.Id,
                        CategoryName = categoryName,
                        Question = f.Question,
                        Answer = f.Answer
                    };
                }).ToList()
            };

            return Ok(userDetails);
        }

        [Authorize]
        [HttpPost("users/{userId}/deleteFlashCards")]
        public async Task<IActionResult> DeleteFlashCards(string userId, [FromBody] List<int> flashCardIds)
        {
            var flashCardsToDelete = await _context.FlashCards
                .Where(f => flashCardIds.Contains(f.Id) && f.UserId == userId)
                .ToListAsync();
            if (flashCardsToDelete.Count == 0)
                return NotFound("No flashcards found to delete.");

            _context.FlashCards.RemoveRange(flashCardsToDelete);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"{flashCardsToDelete.Count} flashcards deleted successfully." });
        }

        [Authorize]
        [HttpPut("user/{email}/updatePassword")]
        public async Task<IActionResult> UpdateUserPassword(string email, [FromBody] UpdateUserViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(email ?? throw new ArgumentNullException(nameof(email)));
            if (user == null)
                return NotFound();

            string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.Password);

            if (result.Succeeded)
                return Ok(new { message = "Password updated successfully" });

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return BadRequest(ModelState);
        }

        [Authorize]
        [HttpDelete("user/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
                return Ok(new { message = "User deleted successfully" });

            return BadRequest(result.Errors);
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userEmail = User.FindFirst("Email")?.Value;
            var user = await _userManager.FindByEmailAsync(userEmail ?? throw new InvalidOperationException("Email claim not found"));

            if (user == null)
                return NotFound("User not found");

            var flashCardCount = await _context.FlashCards.CountAsync(f => f.UserId == user.Id);
            var profile = new UserProfileViewModel
            {
                Username = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                TaskCount = flashCardCount
            };

            return Ok(profile);
        }

        [Authorize]
        [HttpPost("users/{userId}/createFlashCard")]
        public async Task<IActionResult> CreateFlashCard(string userId, [FromBody] CreateFlashCardViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == model.CategoryId); // Removed UserId check
            if (category == null)
                return BadRequest("Invalid category selected.");

            var flashCard = new FlashCard
            {
                CategoryId = model.CategoryId,
                Question = model.Question,
                Answer = model.Answer,
                UserId = userId
            };

            var createdFlashCard = await _flashCardService.CreateFlashCardAsync(flashCard, userId);
            var response = new GetFlashCardViewModel
            {
                Id = createdFlashCard.Id,
                CategoryName = category.Name,
                Question = createdFlashCard.Question,
                Answer = createdFlashCard.Answer,
                UserId = userId
            };

            return CreatedAtAction(null, new { id = createdFlashCard.Id }, response);
        }

        [Authorize]
        [HttpPut("users/{userId}/updateFlashCard/{id}")]
        public async Task<IActionResult> UpdateFlashCard(string userId, int id, [FromBody] UpdateFlashCardViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == model.CategoryId); // Removed UserId check
            if (category == null)
                return BadRequest("Invalid category selected.");

            var flashCard = new FlashCard
            {
                Id = id,
                CategoryId = model.CategoryId,
                Question = model.Question,
                Answer = model.Answer,
                UserId = userId
            };

            var updatedFlashCard = await _flashCardService.UpdateFlashCardAsync(flashCard, userId);
            if (updatedFlashCard == null)
                return NotFound();

            return NoContent();
        }
    }
}