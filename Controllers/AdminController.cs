using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlashcardApp.Services;
using FlashcardApp.ViewModels;
using System.Linq;
using System.Globalization;
using CsvHelper;
using System.IO;

namespace FlashcardApp.Controllers
{
    [Authorize(Roles = "Admin", Policy = "RequireCookie")]
    public class AdminController : Controller
    {
        private readonly AdminService _adminService;
        private readonly FlashCardService _flashCardService; 
        private readonly ILogger<AdminController> _logger;
        private readonly AccountService _accountService;

        public AdminController(AdminService adminService, FlashCardService flashCardService, AccountService accountService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _flashCardService = flashCardService;
            _accountService = accountService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View(); // Updated to use Admin Dashboard view
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
            ViewBag.CategoryNames = await _flashCardService.GetCategoriesAsync(userId); 
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