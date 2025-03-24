using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using FlashcardApp.Models;
using FlashcardApp.ViewModels;
using System.Threading.Tasks;
using System.Collections.Generic;
using FlashcardApp.Data;
using Microsoft.AspNetCore.Identity;
using FlashcardApp.ViewModels.APIViewModels;
using FlashcardApp.Services;
using CsvHelper;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

[Authorize]//(Roles = "Admin")
[Route("api/adminapi")]
[ApiController]
public class AdminApiController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly FlashCardService _flashcardService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminApiController(ApplicationDbContext context, AdminService adminService, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, FlashCardService flashcardService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _adminService = adminService;
        _flashcardService = flashcardService;
    }

    // ➤ Get all categories
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.Categories.ToListAsync();
        return Ok(categories);
    }

    // ➤ Get category by ID
    [HttpGet("categories/{id}")]
    public async Task<IActionResult> GetCategory(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound("Category not found.");
        return Ok(category);
    }

    // ➤ Create a new category
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] Category model)
    {
        Console.WriteLine("Category Name: " + model.Name);
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        _context.Categories.Add(model);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategory), new { id = model.Id }, model);
    }

    // ➤ Update category
    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] Category model)
    {
        Console.WriteLine("Update category ID: " + id);

        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound("Category not found.");

        category.Name = model.Name;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Category updated successfully." });
    }

    // ➤ Delete category
    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        Console.WriteLine("Delete category ID: " + id);
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound("Category not found.");

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Category deleted successfully." });
    }

    // ================================
    // ✅ USER MANAGEMENT (CRUD)
    // ================================

    // ➤ Get all users
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
            var users = _userManager.Users.ToList();
            var userList = new List<ApplicationUser>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("User"))
                {
                    userList.Add(user);
                }
            }
            return Ok(userList);
    }


    // ➤ Get user by ID
    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound("User not found.");
        return Ok(user);
    }

    // ➤ Delete user
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound("User not found.");

        var result = await _userManager.DeleteAsync(user);
        if (result.Succeeded)
            return Ok(new { message = "User deleted successfully." });

        return BadRequest(result.Errors);
    }

    // [HttpPost("bulk-delete-users")]
    // public async Task<IActionResult> BulkDeleteUsers([FromBody] List<string> userIds)
    // {
    //     Console.WriteLine("Bulk delete users: " + userIds.Count);
    //     if (userIds == null || !userIds.Any())
    //     {
    //         return BadRequest("No users selected for deletion.");
    //     }

    //     var result = await _adminService.DeleteUsersAsync(userIds);

    //     if (result)
    //     {
    //         return Ok("Users deleted successfully.");
    //     }

    //     return StatusCode(500, "Failed to delete users.");
    // }


    [Authorize]
    [HttpGet("Users/{userId}/details")]
    public async Task<IActionResult> GetUserDetails(string userId)
    {
        Console.WriteLine("User ID: " + userId);

        var user = await _userManager.Users
            .Include(u => u.FlashCards)
            .ThenInclude(fc => fc.Category)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        var userDetails = new
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Flashcards = user.FlashCards.Select(t => new
            {   
                t.Id,
                t.Question,
                t.Answer,
                CategoryName=t.Category?.Name,
            }).ToList()
        };

        return Ok(userDetails);
    }

        [HttpPut("user/{email}/updatePassword")]
        public async Task<IActionResult> UpdateUserPassword(string email, [FromBody] UpdateUserViewModel model)
        {
            Console.WriteLine("Updating password for: " + email);

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


    [HttpGet("users/{userId}/flashcards")]
    public async Task<IActionResult> GetUserFlashcards(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        var flashcards = await _context.FlashCards
            .Where(f => f.UserId == userId)
            .Include(f => f.Category)
            .ToListAsync();

        return Ok(flashcards);
    }

    [HttpPost("users/{userId}/flashcards")]
    public async Task<IActionResult> CreateFlashcard(string userId, [FromBody] CreateFlashCardViewModel model)
    {
        Console.WriteLine($"Create flashcard for user: {userId}");

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == model.CategoryId);
        if (category == null)
            return BadRequest("Invalid category.");

        var flashcard = new FlashCard
        {
            Question = model.Question,
            Answer = model.Answer,
            CategoryId = model.CategoryId,
            UserId = userId
        };

        _context.FlashCards.Add(flashcard);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUserFlashcards), new { userId }, flashcard);
    }

    [HttpPut("users/{userId}/flashcards/{flashcardId}")]
    public async Task<IActionResult> UpdateFlashcard(string userId, int flashcardId, [FromBody] UpdateFlashCardViewModel model)
    {

        
        var flashcard = await _context.FlashCards.FindAsync(flashcardId);
        if (flashcard == null || flashcard.UserId != userId)
            return NotFound("Flashcard not found.");

        flashcard.Question = model.Question;
        flashcard.Answer = model.Answer;
        flashcard.CategoryId = model.CategoryId;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Flashcard updated successfully." });
    }

    [HttpDelete("users/{userId}/flashcards/{flashcardId}")]
    public async Task<IActionResult> DeleteFlashcard(string userId, int flashcardId)
    {
        var flashcard = await _context.FlashCards.FindAsync(flashcardId);
        if (flashcard == null || flashcard.UserId != userId)
            return NotFound("Flashcard not found.");

        _context.FlashCards.Remove(flashcard);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Flashcard deleted successfully." });
    }

        [HttpPost("bulk-delete-users")]
        public async Task<IActionResult> BulkDeleteUsers([FromBody] BulkDeleteUsersRequest request)
        {
            Console.WriteLine("Bulk delete users: " + (request?.UserIds?.Count ?? 0));
            if (request == null || request.UserIds == null || !request.UserIds.Any())
            {
                return BadRequest("No users selected for deletion.");
            }

            var result = await _adminService.DeleteUsersAsync(request.UserIds);

            if (result)
            {
                return Ok("Users deleted successfully.");
            }

            return StatusCode(500, "Failed to delete users.");
        }

        [HttpGet("users/flashcards/{flashcardId}")]
        public async Task<IActionResult> GetUserFlashcard(int flashcardId)
        {
            // Fetch the flashcard by ID from the database
            var flashcard = await _flashcardService.GetFlashCardByIdAsync(flashcardId);

            // If the flashcard is not found
            if (flashcard == null)
            {
                return NotFound(new { message = $"Flashcard with ID {flashcardId} not found." });
            }

            // Return the found flashcard
            return Ok(flashcard);
        }

        //Generate CSV Reports
        [HttpGet("download-user-flashcards")]
        public async Task<IActionResult> DownloadUserFlashCardsSummary()
        {
            var usersFlashCards = await _adminService.GetUserFlashCardsSummaryAsync();

            var csvStream = GenerateCSVStream(usersFlashCards);
            return File(csvStream, "text/csv", "UserFlashCardsSummary.csv");        }

        [HttpGet("download-all-flashcards")]
        public async Task<IActionResult> DownloadAllFlashCardsWithOwners()
        {
            var flashCardsWithOwners = await _adminService.GetAllFlashCardsWithOwnerAsync();
            var csvStream = GenerateCSVStream(flashCardsWithOwners);
            return File(csvStream, "text/csv", "AllFlashCardsWithOwners.csv");
        }
        private MemoryStream GenerateCSVStream<T>(List<T> data)
        {
            var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
            {
                using (var csvWriter = new CsvHelper.CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture))
                {
                    csvWriter.WriteRecords(data);
                }
            }
            memoryStream.Position = 0; // Reset stream position for download
            return memoryStream;
        }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
            await _roleManager.CreateAsync(new IdentityRole(roleName));
    }
}
public class BulkDeleteUsersRequest
{
    public List<string> UserIds { get; set; }
}