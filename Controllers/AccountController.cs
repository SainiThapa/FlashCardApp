using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using FlashcardApp.ViewModels;
using FlashcardApp.Services;
using FlashcardApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using FlashcardApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace FlashcardApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AccountService _accountService;

        public AccountController(AccountService accountService, ILogger<AccountController> logger,
            UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
        {
            _accountService = accountService;
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        //Register Page
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                var result = await _accountService.RegisterUserAsync(model, User);
                if (result.Succeeded)
                {
                    _logger.LogInformation("New user registered: {Email}", model.Email);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during registration.");
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
            }

            return View(model);
        }

        //Login Page
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.Email, user.Email)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                _logger.LogInformation("User logged in: {Email}", model.Email);
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        //Logout
        [Authorize(Policy = "RequireCookie")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Clear all cookies
            if (HttpContext.Request.Cookies != null)
            {
                foreach (var cookie in HttpContext.Request.Cookies.Keys)
                {
                    Response.Cookies.Delete(cookie);
                }
            }

            // Log the event
            _logger.LogInformation("User logged out successfully.");

            return RedirectToAction("Login", "Account");
        }

        //Reset Password - GET
        [HttpGet]
        [Authorize(Policy = "RequireCookie")]
        public async Task<IActionResult> ResetPassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) 
            return NotFound("User not found.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var model = new PasswordResetViewModel { Email = user.Email, Token = token };
            return View(model);
        }

        //Reset Password - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireCookie")]
        public async Task<IActionResult> ResetPassword(PasswordResetViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return NotFound("User not found.");

            var result = await _accountService.ResetPasswordAsync(model);
            if (result.Succeeded)
            {
                user.PlainTextPassword = model.NewPassword;
                await _userManager.UpdateAsync(user);
                return RedirectToAction("ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        [Authorize(Policy = "RequireCookie")]
        public IActionResult ResetPasswordConfirmation() => View();

        //View User Profile
        [Authorize(Policy = "RequireCookie")]
        public async Task<IActionResult> ViewProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            var flashCardCount = await _context.FlashCards.CountAsync(f => f.UserId == user.Id);
            var model = new ProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                TaskCount = flashCardCount
            };

            return View(model);
        }

        [Authorize(Policy ="RequireCookie")]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound("User not found.");

            var model = new ProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email
            };

            return View(model);
        }
         [HttpPost]
        [Authorize(Policy ="RequireCookie")]

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var modelError in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        Console.WriteLine(modelError.ErrorMessage);
                    }
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
                return NotFound("User not found.");

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.Email = model.Email;

        var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
                return RedirectToAction("ViewProfile","Account");

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }
    }
}
