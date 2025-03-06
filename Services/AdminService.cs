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
                            CategoryName = f.Category.Name,
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
                var category = _context.Categories.FirstOrDefault(c => c.Id == f.CategoryId && c.UserId == userId);
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
                .Select(f => new FlashCardWithOwnerViewModel
                {
                    FlashCardId = f.Id,
                    CategoryName = f.Category.Name,
                    Question = f.Question,
                    Answer = f.Answer,
                    Owner_FullName = f.User.FirstName + " " + f.User.LastName,
                    OwnerEmail = f.User.Email
                }).ToListAsync();
        }
    }
}