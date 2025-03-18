using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlashcardApp.Services;
using FlashcardApp.ViewModels;
using System.Linq;
using System.Globalization;
using CsvHelper;
using System.IO;
using System.Threading.Tasks;
using FlashcardApp.Models;
using FlashcardApp.Data;
using Microsoft.EntityFrameworkCore;

namespace FlashcardApp.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly AdminService _adminService;
        private readonly FlashCardService _flashCardService;
        private readonly ILogger<AdminController> _logger;
        private readonly AccountService _accountService;
        private readonly ApplicationDbContext _context;

        public AdminController(AdminService adminService,
            ApplicationDbContext context,
            FlashCardService flashCardService, 
            AccountService accountService, 
            ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _flashCardService = flashCardService;
            _accountService = accountService;
            _logger = logger;
            _context = context;
        }

        //Admin Dashboard
        [HttpGet("dashboard")]
        public IActionResult Dashboard()
        {
            return View();
        }

        //View All Users
        [HttpGet("users")]
        public async Task<IActionResult> UserList()
        {
            var users = await _adminService.GetAllUsersAsync();
            return View(users);
        }

        //View User's Flashcards
        [HttpGet("users/{userId}/flashcards")]
        public async Task<IActionResult> UserFlashCards(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return NotFound("User ID is missing or invalid.");

            var flashCards = await _flashCardService.GetUserFlashCardsAsync(userId);
            if (flashCards == null || !flashCards.Any())
                return NotFound("No flashcards found for this user.");

            ViewBag.UserId = userId;
            ViewBag.CategoryNames = await _adminService.GetAllCategoriesAsync();
            return View(flashCards);
        }

        //Bulk Delete Flashcards for a User
        [HttpPost("users/{userId}/deleteFlashCards")]
        public async Task<IActionResult> DeleteSelectedFlashCards(List<int> flashCardIds, string userId)
        {
            if (flashCardIds != null && flashCardIds.Count > 0)
            {
                await _adminService.DeleteFlashCardsAsync(flashCardIds);
                return RedirectToAction("UserFlashCards", new { userId });
            }
            return RedirectToAction("UserFlashCards", new { userId });
        }

        //Get All Categories (API)
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();
            return Ok(categories);
        }

        //View & Manage Categories
        [HttpGet("manage-categories")]
        public async Task<IActionResult> Categories()
        {
            var categories = await _adminService.GetAllCategoriesAsync();
            var viewModel = new CategoryManagementViewModel
            {
                Categories = categories.Select(c => new CategoryViewModel { Id = c.Id, Name = c.Name }).ToList()
            };
            return View(viewModel);
        }

        //Add New Category
        [HttpPost("add-category")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(CategoryManagementViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid category model received.");
                return View("Categories", model);
            }

            try
            {
                await _adminService.AddCategoryAsync(new Category { Name = model.NewCategory.Name });
                TempData["SuccessMessage"] = "Category added successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add category.");
                TempData["ErrorMessage"] = "Error adding category.";
            }
            return RedirectToAction("Categories");
        }

        //Edit Category
        [HttpGet("edit-category/{id}")]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _adminService.GetCategoryByIdAsync(id);
            if (category == null) return NotFound();

            return View(new CategoryViewModel { Id = category.Id, Name = category.Name });
        }

        [HttpPost("edit-category")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(CategoryViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var category = await _adminService.GetCategoryByIdAsync(model.Id);
            if (category == null) return NotFound();

            category.Name = model.Name;
            await _adminService.UpdateCategoryAsync(category);
            return RedirectToAction("Categories");
        }

        //Delete Category
        [HttpPost("delete-category/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            await _adminService.DeleteCategoryAsync(id);
            return RedirectToAction("Categories");
        }

        //Forgot Password
        [HttpGet("forgot-password")]
        public IActionResult ForgotPassword() => View();

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(UpdatePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var token = await _accountService.GeneratePasswordResetTokenAsync(model.Email);
            if (string.IsNullOrEmpty(token))
            {
                ModelState.AddModelError("", "Invalid email address.");
                return View(model);
            }
            return RedirectToAction("ResetPassword", new { token, email = model.Email });
        }

        //Reset Password
        [HttpGet("reset-password")]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
                return RedirectToAction("ForgotPassword");

            return View(new PasswordResetViewModel { Token = token, Email = email });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(PasswordResetViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _accountService.ResetPasswordAsync(model);
            if (result.Succeeded)
                return RedirectToAction("Dashboard");

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        //Generate CSV Reports
        [HttpGet("download-user-flashcards")]
        public async Task<IActionResult> DownloadUserFlashCardsSummary()
        {
            var usersFlashCards = await _adminService.GetUserFlashCardsSummaryAsync();
            return GenerateCSV(usersFlashCards, "UserFlashCardsSummary.csv");
        }

        [HttpGet("download-all-flashcards")]
        public async Task<IActionResult> DownloadAllFlashCardsWithOwners()
        {
            var flashCardsWithOwners = await _adminService.GetAllFlashCardsWithOwnerAsync();
            return GenerateCSV(flashCardsWithOwners, "AllFlashCardsWithOwners.csv");
        }

        private IActionResult GenerateCSV<T>(IEnumerable<T> records, string fileName)
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
                writer.Flush();
                stream.Position = 0;
                return File(stream, "text/csv", fileName);
            }
        }
    }
}
