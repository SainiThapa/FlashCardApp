using FlashcardApp.Data;
using FlashcardApp.Models;
using FlashcardApp.ViewModels;
using FlashcardApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FlashcardApp.Controllers
{
    [Authorize(Policy = "RequireCookie")]
    public class FlashCardsController : Controller
    {
        private readonly FlashCardService _flashCardService;
        private readonly ApplicationDbContext _context;

        public FlashCardsController(FlashCardService flashCardService, ApplicationDbContext context)
        {
            _flashCardService = flashCardService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found.");

            var flashCards = await _flashCardService.GetUserFlashCardsAsync(userId);
            var viewModels = flashCards.Select(f =>
            {
                var category = _context.Categories.FirstOrDefault(c => c.Id == f.CategoryId);
                string categoryName = category != null ? category.Name : "Unknown";
                return new FlashCardViewModel
                {
                    Id = f.Id,
                    CategoryId = f.CategoryId,
                    CategoryName = categoryName,
                    Question = f.Question,
                    Answer = f.Answer
                };
            }).ToList();

            var categories = flashCards
                .Select(f => new CategoryViewModel
                {
                    Id = f.CategoryId,
                    Name = _context.Categories.FirstOrDefault(c => c.Id == f.CategoryId)?.Name ?? "Unknown"
                })
                .DistinctBy(c => c.Id)
                .ToList();

            ViewBag.TotalFlashCards = viewModels.Count;
            ViewBag.Categories = categories;

            return View(viewModels);
        }

        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found.");

            var model = new FlashCardViewModel
            {
                Categories = await _context.Categories // Global categories, no UserId filter
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                    .ToListAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FlashCardViewModel model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found.");

            if (ModelState.IsValid)
            {
                var flashCard = new FlashCard
                {
                    CategoryId = model.CategoryId,
                    Question = model.Question,
                    Answer = model.Answer,
                    UserId = userId
                };

                await _flashCardService.CreateFlashCardAsync(flashCard, userId);
                return RedirectToAction(nameof(Index));
            }

            model.Categories = await _context.Categories // Global categories
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var flashCard = await _flashCardService.GetFlashCardByIdAsync(id);
            if (flashCard == null) return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found.");

            var viewModel = new FlashCardViewModel
            {
                Id = flashCard.Id,
                CategoryId = flashCard.CategoryId,
                Question = flashCard.Question,
                Answer = flashCard.Answer,
                Categories = await _context.Categories // Global categories
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                    .ToListAsync()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FlashCardViewModel model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found.");

            if (ModelState.IsValid)
            {
                var flashCard = new FlashCard
                {
                    Id = id,
                    CategoryId = model.CategoryId,
                    Question = model.Question,
                    Answer = model.Answer,
                    UserId = userId
                };
                await _flashCardService.UpdateFlashCardAsync(flashCard, userId);
                return RedirectToAction(nameof(Index));
            }

            model.Categories = await _context.Categories // Global categories
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                .ToListAsync();
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var flashCard = await _flashCardService.GetFlashCardByIdAsync(id);
            if (flashCard == null) return NotFound();

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found.");

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == flashCard.CategoryId);             
                var viewModel = new FlashCardViewModel
            {
                Id = flashCard.Id,
                CategoryId = flashCard.CategoryId,
                CategoryName = category?.Name ?? "Unknown",
                Question = flashCard.Question,
                Answer = flashCard.Answer
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found.");

            var deleted = await _flashCardService.DeleteFlashCardAsync(id, userId);
            if (!deleted)
                return NotFound();

            return RedirectToAction(nameof(Index));
        }


        public async Task<IActionResult> RandomFlashCard()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found.");

            var flashCards = await _flashCardService.GetUserFlashCardsAsync(userId);
            if (flashCards == null || !flashCards.Any())
                return NotFound("No flashcards found for the user.");

            var random = new Random();
            var randomFlashCard = flashCards.OrderBy(x => random.Next()).FirstOrDefault();

            if (randomFlashCard == null)
                return NotFound("Unable to retrieve a random flashcard.");

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == randomFlashCard.CategoryId);

            var viewModel = new FlashCardViewModel
            {
                Id = randomFlashCard.Id,
                CategoryId = randomFlashCard.CategoryId,
                CategoryName = category?.Name ?? "Unknown",
                Question = randomFlashCard.Question,
                Answer = randomFlashCard.Answer
            };

            return View(viewModel);
        }
    }
}