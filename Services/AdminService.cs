using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FlashcardApp.Data;
using FlashcardApp.Models;
using FlashcardApp.ViewModels;

namespace FlashcardApp.Services
{
    public class AdminService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminService(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<List<ApplicationUser>> GetAllUsersAsync()
        {
            var users = _userManager.Users.ToList();
            var userList = new List<ApplicationUser>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("User")) // Only add users with the "User" role
                {
                    userList.Add(user);
                }
            }
            return userList;
        }

        public async Task DeleteFlashCardsAsync(List<int> flashCardIds)
        {
            var flashCardsToDelete = await _context.FlashCards
                .Where(f => flashCardIds.Contains(f.Id))
                .ToListAsync();

            _context.FlashCards.RemoveRange(flashCardsToDelete);
            await _context.SaveChangesAsync();
        }

        public async Task<List<UserFlashCardsSummaryViewModel>> GetUserFlashCardsSummaryAsync()
        {
            var usersWithFlashCards = new List<UserFlashCardsSummaryViewModel>();

            var users = _userManager.Users.Include(u => u.FlashCards).ToList();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("User")) // Filter to include only users with "User" role
                {
                    usersWithFlashCards.Add(new UserFlashCardsSummaryViewModel
                    {
                        UserId = user.Id,
                        Email = user.Email,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        UserName = user.UserName,
                        FlashCards = user.FlashCards.Select(f => new FlashCardViewModel
                        {
                            Id = f.Id,
                            CategoryName = f.Category?.Name ?? "Unknown", // Use navigation property
                            Question = f.Question,
                            Answer = f.Answer
                        }).ToList()
                    });
                }
            }
            return usersWithFlashCards;
        }

        public async Task<List<FlashCardViewModel>> GetUserFlashCardsAsync(string userId)
        {
            var flashCards = await _context.FlashCards
                .Where(f => f.UserId == userId)
                .ToListAsync();
            return flashCards.Select(f =>
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
            }).ToList();
        }

        public async Task<List<FlashCardWithOwnerViewModel>> GetAllFlashCardsWithOwnerAsync()
        {
            return await _context.FlashCards
                .Include(f => f.User)  // Include the user to access owner details
                .Include(f => f.Category) // Include Category for name
                .Select(f => new FlashCardWithOwnerViewModel
                {
                    FlashCardId = f.Id,
                    CategoryName = f.Category.Name ?? "Unknown",
                    Question = f.Question,
                    Answer = f.Answer,
                    Owner_FullName = f.User.FirstName + " " + f.User.LastName,
                    OwnerEmail = f.User.Email
                }).ToListAsync();
        }
        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            return await _context.Categories.ToListAsync();
        }

        public async Task AddCategoryAsync(Category category)
        {
        try
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding category: {ex.Message}");
                throw; 
            }        
        }

        public async Task UpdateCategoryAsync(Category category)
        {
            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(int categoryId)
        {
            var category = await _context.Categories.FindAsync(categoryId);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Category> GetCategoryByIdAsync(int id)
        {
            return await _context.Categories.FindAsync(id);
        }
        public async Task<bool> DeleteUsersAsync(List<string> userIds)
        {
            if (userIds == null || !userIds.Any())
            {
                return false;
            }

            var usersToDelete = await _context.Users
                .Where(user => userIds.Contains(user.Id))
                .ToListAsync();

            if (!usersToDelete.Any())
            {
                return false;
            }

            _context.Users.RemoveRange(usersToDelete);

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting users: {ex.Message}");
                return false;
            }
        }
    }
}