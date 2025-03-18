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

[Authorize(Roles = "Admin")]
[Route("api/adminapi")]
[ApiController]
public class AdminApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
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
        var users = await _userManager.Users.ToListAsync();
        return Ok(users);
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

    // ➤ Update user details
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateProfileViewModel model)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound("User not found.");

        user.UserName=model.Username;
        user.Email = model.Email;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
            return Ok(new { message = "User updated successfully." });

        return BadRequest(result.Errors);
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

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
            await _roleManager.CreateAsync(new IdentityRole(roleName));
    }
}
