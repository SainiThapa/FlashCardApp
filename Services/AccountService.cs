using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using FlashcardApp.Models; // Replace with the namespace containing your ApplicationUser model
using FlashcardApp.ViewModels; // Replace with the namespace containing your view models

namespace FlashcardApp.Services
{
    public class AccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;

        }
        public async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Register a new user
        public async Task<IdentityResult> RegisterUserAsync(RegisterViewModel model, ClaimsPrincipal currentUser)
        {
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return IdentityResult.Failed(new IdentityError { Description = "A user with this email already exists." });
            }

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
                if (!currentUser.Identity.IsAuthenticated)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                }
            }

            return result;
        }

        // Login user
        public async Task<SignInResult> LoginUserAsync(LoginViewModel model)
        {
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);
            return result;
        }

        // Logout user
        public async Task LogoutUserAsync()
        {
            await _signInManager.SignOutAsync();
        }

        // Reset user password
        public async Task<IdentityResult> ResetPasswordAsync(PasswordResetViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return IdentityResult.Failed(new IdentityError { Description = "User not found" });

            }
            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (result.Succeeded)
            {
                user.PlainTextPassword = model.NewPassword;
                await _userManager.UpdateAsync(user);
            }
            return result;
        }

        // Send a password reset token to be used in a link sent via email
        public async Task<string> GeneratePasswordResetTokenAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return null;
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            return token;
        }

        public async Task<IdentityResult> DeleteUsersAsync(List<string> userIds)
        {
            IdentityResult result = IdentityResult.Success;
            foreach (var userId in userIds)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    var deletionResult = await _userManager.DeleteAsync(user);
                    if (!deletionResult.Succeeded)
                    {
                        return deletionResult;
                    }
                }
            }
            return result;
        }
    }
}
