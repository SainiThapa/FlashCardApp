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
            var category = _context.Categories.FirstOrDefault(c => c.Id == f.CategoryId && c.UserId == userId);
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

        ViewBag.TotalFlashCards = viewModels.Count;
        return View(viewModels);
    }

    public async Task<IActionResult> Create()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var model = new FlashCardViewModel
        {
            Categories = await _context.Categories
                .Where(c => c.UserId == userId)
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

        model.Categories = await _context.Categories
            .Where(c => c.UserId == userId)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
        return View(model);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var flashCard = await _flashCardService.GetFlashCardByIdAsync(id);
        if (flashCard == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var viewModel = new FlashCardViewModel
        {
            Id = flashCard.Id,
            CategoryId = flashCard.CategoryId,
            Question = flashCard.Question,
            Answer = flashCard.Answer,
            Categories = await _context.Categories
                .Where(c => c.UserId == userId)
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

        model.Categories = await _context.Categories
            .Where(c => c.UserId == userId)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();
        return View(model);
    }
    public async Task<IActionResult> Details(int id)
    {
      var flashCard = await _flashCardService.GetFlashCardByIdAsync(id);
      if (flashCard == null) return NotFound();

      var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      var category = await _context.Categories
          .FirstOrDefaultAsync(c => c.Id == flashCard.CategoryId && c.UserId == userId);
      var viewModel = new FlashCardViewModel
      {
        Id = flashCard.Id,
        CategoryId = flashCard.CategoryId,
        CategoryName = category?.Name   ?? "Unknown",
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
  }
}