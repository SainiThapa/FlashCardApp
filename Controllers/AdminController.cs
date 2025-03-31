using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlashcardApp.Services;
using FlashcardApp.ViewModels;
using System.Linq;
using System.Globalization;
using CsvHelper;
using System.IO;
using FlashcardApp.ViewModels.APIViewModels;
using Microsoft.AspNetCore.Authentication.Cookies;
using FlashcardApp.Data;
using Microsoft.EntityFrameworkCore;
using FlashcardApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace FlashcardApp.Controllers
{
    // [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    // [Authorize]//(Policy = "AdminOnly")]
    [Authorize(Policy = "RequireCookie")]
    public class AdminController : Controller
    {
        private readonly AdminService _adminService;
        private readonly FlashCardService _flashCardService; 
        private readonly ILogger<AdminController> _logger;
        private readonly AccountService _accountService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(AdminService adminService,ApplicationDbContext context, FlashCardService flashCardService, AccountService accountService, ILogger<AdminController> logger, UserManager<ApplicationUser> userManager)
        {
            _adminService = adminService;
            _flashCardService = flashCardService;
            _accountService = accountService;
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }
        
        public IActionResult GetUserClaims()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            return Ok(claims);
        }

        public async Task <IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && user.Email == "admin@abc.com")
            {
                return View();
            }
            return Unauthorized("You do not have access to this page.");

        }

        public async Task<IActionResult> UserList()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null && user.Email == "admin@abc.com")
            {
                var users = await _adminService.GetAllUsersAsync();
                return View(users);
            }
            return Unauthorized("You do not have access to this page.");
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

    [HttpGet]
    public async Task<IActionResult> CreateFlashCard(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return NotFound("User ID is missing or invalid.");

        var categories = await _adminService.GetAllCategoriesAsync();
        var viewModel = new AdminUserFlashCardViewModel
        {
            UserId = userId,
            Categories = categories.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList()
        };
        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> CreateFlashCard(AdminUserFlashCardViewModel model)
    {
        if (ModelState.IsValid)
        {
            var flashCard = new FlashCard
            {
                UserId = model.UserId,
                CategoryId = model.CategoryId,
                Question = model.Question,
                Answer = model.Answer
            };
            await _flashCardService.CreateFlashCardAsync(flashCard);
            return RedirectToAction(nameof(UserFlashCards), new { userId = model.UserId });
        }

        model.Categories = (await _adminService.GetAllCategoriesAsync())
                            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditFlashCard(int id)
    {
        var flashCard = await _flashCardService.GetFlashCardByIdAsync(id);
        if (flashCard == null)
            return NotFound("Flashcard not found.");

        var categories = await _adminService.GetAllCategoriesAsync();
        var viewModel = new AdminUserFlashCardViewModel
        {
            Id = flashCard.Id,
            UserId = flashCard.UserId,
            CategoryId = flashCard.CategoryId,
            Question = flashCard.Question,
            Answer = flashCard.Answer,
            Categories = categories.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList()
        };
        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> EditFlashCard(AdminUserFlashCardViewModel model)
    {
        if (ModelState.IsValid)
        {
            var flashCard = new FlashCard
            {
                Id = model.Id,
                UserId = model.UserId,
                CategoryId = model.CategoryId,
                Question = model.Question,
                Answer = model.Answer
            };
            await _flashCardService.ModifyFlashCardAsync(flashCard);
            return RedirectToAction(nameof(UserFlashCards), new { userId = model.UserId });
        }
        model.Categories = (await _adminService.GetAllCategoriesAsync())
                            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
        return View(model);
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

        [HttpGet]
        public async Task<IActionResult> Categories()
        {
             var user = await _userManager.GetUserAsync(User);
            if (user != null && user.Email == "admin@abc.com")
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
            return Unauthorized("You do not have access to this page.");
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

            var model = new PasswordResetViewModel 
            { 
                Token = token, Email = email 
            };
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
                    return RedirectToAction("Index", "Flashcards");
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
        public async Task<IActionResult> ForgotPassword(UpdatePasswordViewModel model)
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

        public async Task<FileResult> DownloadUserFlashCardsSummary()
        {
            var usersFlashCards = await _adminService.GetUserFlashCardsSummaryAsync();
            var pdfStream = GenerateUserFlashCardsPDF(usersFlashCards);
            return File(pdfStream, "application/pdf", "UserSummaryReport.pdf");        
        }

        public async Task<FileResult> DownloadAllFlashCardsWithOwners()
        {
            var flashCardsWithOwners = await _adminService.GetAllFlashCardsWithOwnerAsync();
            var pdfStream = GenerateAllFlashCardsPDF(flashCardsWithOwners);
            return File(pdfStream, "application/pdf", "FlashCardsReports.pdf");
        }

        private MemoryStream GenerateUserFlashCardsPDF(List<UserFlashCardsSummaryViewModel> usersFlashCards)
        {
            var memoryStream = new MemoryStream();
            var document = new PdfDocument();
            var page = document.AddPage();
            var graphics = XGraphics.FromPdfPage(page);
            var font = new XFont("Verdana", 10, XFontStyleEx.Regular);
            int yPoint = 40;
            
            graphics.DrawString("Users Summary Report", new XFont("Verdana", 14, XFontStyleEx.Bold), XBrushes.Black, new XRect(20, 20, page.Width - 40, page.Height - 40), XStringFormats.TopLeft);
            
            foreach (var user in usersFlashCards)
            {
                int flashCardCount = user.FlashCards.Count;
                graphics.DrawString($"User ID: {user.UserId}", font, XBrushes.Black, new XRect(20, yPoint, page.Width - 40, page.Height - 40), XStringFormats.TopLeft);
                yPoint += 20;
                graphics.DrawString($"Name: {user.FirstName} {user.LastName}", font, XBrushes.Black, new XRect(20, yPoint, page.Width - 40, page.Height - 40), XStringFormats.TopLeft);
                yPoint += 20;
                graphics.DrawString($"Email: {user.Email}", font, XBrushes.Black, new XRect(20, yPoint, page.Width - 40, page.Height - 40), XStringFormats.TopLeft);
                yPoint += 20;
                graphics.DrawString($"Flashcards Count: {flashCardCount}", font, XBrushes.Gray, new XRect(40, yPoint, page.Width - 60, page.Height - 40), XStringFormats.TopLeft);
                yPoint += 30;
            }
            
            document.Save(memoryStream, false);
            memoryStream.Position = 0;
            return memoryStream;
        }


        private MemoryStream GenerateAllFlashCardsPDF(List<FlashCardWithOwnerViewModel> flashCardsWithOwners)
        {
            var memoryStream = new MemoryStream();
            var document = new PdfDocument();
            var page = document.AddPage();
            var graphics = XGraphics.FromPdfPage(page);
            var font = new XFont("Verdana", 10, XFontStyleEx.Regular);
            int yPoint = 40;
            
            graphics.DrawString("User's Flash Cards report", new XFont("Verdana", 14, XFontStyleEx.Bold), XBrushes.Black, new XRect(20, 20, page.Width - 40, page.Height - 40), XStringFormats.TopLeft);
            
            foreach (var flashcard in flashCardsWithOwners)
            {
                graphics.DrawString($"{flashcard.CategoryName}: {flashcard.Question} - {flashcard.Answer}", font, XBrushes.Black, new XRect(20, yPoint, page.Width - 40, page.Height - 40), XStringFormats.TopLeft);
                yPoint += 20;
                graphics.DrawString($"Owner: {flashcard.Owner_FullName} ({flashcard.OwnerEmail})", font, XBrushes.Gray, new XRect(40, yPoint, page.Width - 60, page.Height - 40), XStringFormats.TopLeft);
                yPoint += 30;
            }
            
            document.Save(memoryStream, false);
            memoryStream.Position = 0;
            return memoryStream;
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