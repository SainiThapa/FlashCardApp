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
    [Authorize(Roles = "Admin", Policy = "RequireCookie")]
    public class AdminController : Controller
    {
        private readonly AdminService _adminService;
        private readonly FlashCardService _flashCardService;
        private readonly ILogger<AdminController> _logger;
        private readonly AccountService _accountService;
        private readonly ApplicationDbContext _context;


        public AdminController(AdminService adminService,
            ApplicationDbContext context,
         FlashCardService flashCardService, AccountService accountService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _flashCardService = flashCardService;
            _accountService = accountService;
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> UserList()
        {
            var users = await _adminService.GetAllUsersAsync();
            return View(users);
        }

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

        [HttpPost]
        public async Task<IActionResult> DeleteSelectedFlashCards(List<int> flashCardIds, string userId)
        {
            if (flashCardIds != null && flashCardIds.Count > 0)
            {
                await _adminService.DeleteFlashCardsAsync(flashCardIds);
                return RedirectToAction("UserFlashCards", new { userId });
            }
            return RedirectToAction("UserFlashCards", new { userId });
        }

        // Category Management Actions
        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _adminService.GetAllCategoriesAsync();
            var viewModel = new CategoryManagementViewModel
            {
                Categories = categories.Select(c => new CategoryViewModel
                {
                    Id = c.Id,
                    Name = c.Name
                }).ToList()
            };
            return View(viewModel);
        }

        [Authorize]
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();
            return Ok(categories);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(CategoryManagementViewModel model)
        {
            _logger.LogInformation($"Incoming Category Name: '{model.NewCategory.Name}'");
            if (!ModelState.IsValid)
            {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            _logger.LogWarning("ModelState invalid: {0}", string.Join(", ", errors));
            var categories = await _adminService.GetAllCategoriesAsync();
            model.Categories = categories.Select(c => new CategoryViewModel
            {
                Id = c.Id,
                Name = c.Name}).ToList();
                return View("Categories", model);
               }
            try
            {
                var category = new Category
                {
                    Name = model.NewCategory.Name
                };
                await _adminService.AddCategoryAsync(category);
                TempData["SuccessMessage"] = "Category added successfully!";
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Failed to add category");
                TempData["ErrorMessage"] = $"Failed to add category: {error.Message}";
            }
            return RedirectToAction("Categories");
        }

        [HttpGet]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _adminService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            var model = new CategoryViewModel
            {
                Id = category.Id,
                Name = category.Name
            };
            return View(model);
        }

        // New EditCategory POST action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(CategoryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var category = await _adminService.GetCategoryByIdAsync(model.Id);
            if (category == null)
            {
                return NotFound();
            }

            category.Name = model.Name;
            await _adminService.UpdateCategoryAsync(category);
            return RedirectToAction("Categories");
        }

        // New DeleteCategory GET action (for confirmation)
        [HttpGet]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _adminService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            var model = new CategoryViewModel
            {
                Id = category.Id,
                Name = category.Name
            };
            return View(model);
        }

        // New DeleteCategory POST action
        [HttpPost, ActionName("DeleteCategory")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategoryConfirmed(int id)
        {
            await _adminService.DeleteCategoryAsync(id);
            return RedirectToAction("Categories");
        }

        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
                return RedirectToAction("ForgotPassword");

            var model = new PasswordResetViewModel { Token = token, Email = email };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(PasswordResetViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(model.Token))
                {
                    ModelState.AddModelError(string.Empty, "Invalid token.");
                    return View(model);
                }

                var result = await _accountService.ResetPasswordAsync(model);
                if (result.Succeeded)
                {
                    ViewData["Message"] = "Password reset successful!";
                    return RedirectToAction("Index", "Home");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var token = await _accountService.GeneratePasswordResetTokenAsync(model.Email);
                if (string.IsNullOrEmpty(token))
                {
                    ModelState.AddModelError(string.Empty, "Invalid email address.");
                    return View(model);
                }
                return RedirectToAction("ResetPassword", new { token, email = model.Email });
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadUserFlashCardsSummary()
        {
            var usersFlashCards = await _adminService.GetUserFlashCardsSummaryAsync();
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

            csvWriter.WriteRecords(usersFlashCards);
            writer.Flush();
            stream.Position = 0;

            return File(stream, "text/csv", "UserFlashCardsSummary.csv");
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAllFlashCardsWithOwners()
        {
            var flashCardsWithOwners = await _adminService.GetAllFlashCardsWithOwnerAsync();
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

            csvWriter.WriteRecords(flashCardsWithOwners);
            writer.Flush();
            stream.Position = 0;

            return File(stream, "text/csv", "AllFlashCardsWithOwners.csv");
        }

        public IActionResult AddUser() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var result = await _accountService.RegisterUserAsync(model, User);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Admin created a new user account with email: {Email}", model.Email);
                    return RedirectToAction("UserList");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while an admin was creating a new user.");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUsers(List<string> userIds)
        {
            if (userIds != null && userIds.Count > 0)
            {
                var result = await _accountService.DeleteUsersAsync(userIds);
                if (result.Succeeded)
                    TempData["SuccessMessage"] = "Users deleted successfully.";
                else
                    TempData["ErrorMessage"] = "Error deleting users: " + result.Errors.FirstOrDefault()?.Description;
            }
            return RedirectToAction("UserList");
        }
    }
}