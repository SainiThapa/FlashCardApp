using FlashcardApp.Data;
using FlashcardApp.Models;
using FlashcardApp.ViewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlashcardApp.Services
{
    public class FlashCardService
    {
        private readonly ApplicationDbContext _context;

        public FlashCardService(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<FlashCard>> GetUserFlashCardsAsync(string userId)
        {
            return await _context.FlashCards
                .Where(f => f.UserId == userId)
                .ToListAsync() ?? new List<FlashCard>(); // CS8603 fix
        }

        public async Task<FlashCard> CreateFlashCardAsync(FlashCard flashCard, string userId)
        {
            if (flashCard == null) throw new ArgumentNullException(nameof(flashCard));
            flashCard.UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            _context.FlashCards.Add(flashCard);
            await _context.SaveChangesAsync();
            return flashCard;
        }

        public async Task<FlashCard> UpdateFlashCardAsync(FlashCard flashCard, string userId)
        {
            if (flashCard == null) throw new ArgumentNullException(nameof(flashCard));
            var existingFlashCard = await _context.FlashCards.FindAsync(flashCard.Id);
            if (existingFlashCard == null || existingFlashCard.UserId != userId)
                return null;

            existingFlashCard.CategoryId = flashCard.CategoryId;
            existingFlashCard.Question = flashCard.Question;
            existingFlashCard.Answer = flashCard.Answer;
            await _context.SaveChangesAsync();
            return existingFlashCard; // CS8603 fix with potential null
        }

        public async Task<bool> DeleteFlashCardAsync(int id, string userId)
        {
            var flashCard = await _context.FlashCards.FindAsync(id);
            if (flashCard == null || flashCard.UserId != userId)
                return false;

            _context.FlashCards.Remove(flashCard);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<FlashCard> GetFlashCardByIdAsync(int id)
        {
            return await _context.FlashCards.FindAsync(id) ?? throw new InvalidOperationException("Flashcard not found");
        }

        public async Task<FlashCardViewModel> GetRandomFlashCardAsync(string userId, int? categoryId = null)
        {
            var flashCards = await GetUserFlashCardsAsync(userId);
            if (flashCards == null || !flashCards.Any())
                return null;

            var filteredFlashCards = categoryId.HasValue
                ? flashCards.Where(f => f.CategoryId == categoryId.Value).ToList()
                : flashCards.ToList();
            if (!filteredFlashCards.Any())
                return null;

            var random = new Random();
            var flashCard = filteredFlashCards[random.Next(filteredFlashCards.Count)];
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == flashCard.CategoryId && c.UserId == userId);
            return new FlashCardViewModel
            {
                Id = flashCard.Id,
                CategoryName = category.Name!= null ? category.Name : "Unknown",
                Question = flashCard.Question,
                Answer = flashCard.Answer
            };
        }
        public async Task<Dictionary<int, string>> GetCategoriesAsync(string userId)
        {
            return await _context.Categories
                .Where(c => c.UserId == userId)
                .ToDictionaryAsync(c => c.Id, c => c.Name) ?? new Dictionary<int, string>();
        }
    }
}