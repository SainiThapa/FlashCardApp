using FlashcardApp.Data;
using FlashcardApp.Models;
using FlashcardApp.ViewModels.APIViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using FlashcardApp.Services;

namespace FlashcardApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class FlashCardAPIController : ControllerBase
    {
        private readonly FlashCardService _flashCardService;
        private readonly ApplicationDbContext _context;

        public FlashCardAPIController(FlashCardService flashCardService, ApplicationDbContext context)
        {
            _flashCardService = flashCardService;
            _context = context;
        }

        [HttpGet("List")]
        public async Task<ActionResult<IEnumerable<FlashCard>>> GetFlashCards()
        {
        Console.WriteLine("API CALLED FOR LIST OF FLASHCARDS");
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User ID not found in the token.");

        var flashCards = _context.FlashCards.Where(f=>f.UserId==userId).ToList();
        var flashCardViewModels = flashCards.Select(flashCard =>
        {
            var category = _context.Categories.FirstOrDefault(c => c.Id == flashCard.CategoryId);
            return new GetFlashCardViewModel
            {
                Id = flashCard.Id,
                CategoryName = category?.Name ?? "Unknown",
                Question = flashCard.Question,
                Answer = flashCard.Answer,
                UserId = flashCard.UserId
            };
        }).ToList();
        Console.WriteLine("RETURNING FLASHCARDS LIST");

        return Ok(flashCardViewModels);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GetFlashCardViewModel>> GetFlashCard(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found in the token.");

            var flashCard = await _flashCardService.GetFlashCardByIdAsync(id);
            if (flashCard == null || flashCard.UserId != userId)
                return NotFound();

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == flashCard.CategoryId); // Removed UserId check
            var viewModel = new GetFlashCardViewModel
            {
                Id = flashCard.Id,
                CategoryName = category?.Name ?? "Unknown",
                Question = flashCard.Question,
                Answer = flashCard.Answer,
                UserId = flashCard.UserId
            };
            return Ok(viewModel);
        }

        [HttpPost("create")]
        public async Task<ActionResult<GetFlashCardViewModel>> CreateFlashCard(CreateFlashCardViewModel model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found in the token.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == model.CategoryId); // Removed UserId check
            if (category == null)
                return BadRequest("Invalid category selected.");

            var flashCard = new FlashCard
            {
                CategoryId = model.CategoryId,
                Question = model.Question,
                Answer = model.Answer,
                UserId = userId
            };

            var createdFlashCard = await _flashCardService.CreateFlashCardAsync(flashCard, userId);
            var response = new GetFlashCardViewModel
            {
                Id = createdFlashCard.Id,
                CategoryName = category.Name,
                Question = createdFlashCard.Question,
                Answer = createdFlashCard.Answer,
                UserId = userId
            };
            return CreatedAtAction(nameof(GetFlashCard), new { id = createdFlashCard.Id }, response);
        }

        [HttpPut("update/{id}")]
        public async Task<IActionResult> UpdateFlashCard(int id, UpdateFlashCardViewModel model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found in the token.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == model.CategoryId); // Removed UserId check
            if (category == null)
                return BadRequest("Invalid category selected.");

            var flashCard = new FlashCard
            {
                Id = id,
                CategoryId = model.CategoryId,
                Question = model.Question,
                Answer = model.Answer,
                UserId = userId
            };

            var updatedFlashCard = await _flashCardService.UpdateFlashCardAsync(flashCard, userId);
            if (updatedFlashCard == null)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteFlashCard(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found in the token.");

            var deleted = await _flashCardService.DeleteFlashCardAsync(id, userId);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> BulkDeleteFlashCards([FromBody] List<int> flashcardIds)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                {
                Console.WriteLine("BulkDeleteFlashCards: Unauthorized request.");
                return Unauthorized("User ID not found in the token.");
                }   

            if (flashcardIds == null || !flashcardIds.Any())
                {
                    Console.WriteLine("BulkDeleteFlashCards: No flashcard IDs received.");
                    return BadRequest("No flashcard IDs provided for deletion.");
                }
            Console.WriteLine("BulkDeleteFlashCards: Received IDs - " + string.Join(", ", flashcardIds));

            var deleted = await _flashCardService.BulkDeleteFlashCardsAsync(flashcardIds, userId);
            if (!deleted)
            {
                Console.WriteLine("BulkDeleteFlashCards: Failed to delete flashcards.");
                return NotFound("Failed to delete some or all flashcards.");
            }

            return NoContent();
        }

        
        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            var categories = await _context.Categories.ToListAsync();
            return Ok(categories);
        }

        [HttpGet("quiz/random")]
        public async Task<ActionResult<GetFlashCardViewModel>> GetRandomFlashCard([FromQuery] int? CategoryId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User ID not found in the token.");

            var flashCards = await _flashCardService.GetUserFlashCardsAsync(userId);
            if (flashCards == null || !flashCards.Any())
                return NotFound("No flashcards found.");

            var filteredFlashCards = CategoryId.HasValue
                ? flashCards.Where(f => f.CategoryId == CategoryId.Value).ToList()
                : flashCards.ToList();
            if (!filteredFlashCards.Any())
                return NotFound("No flashcards found for the specified category.");

            var random = new Random();
            var flashCard = filteredFlashCards[random.Next(filteredFlashCards.Count)];
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == flashCard.CategoryId); // Removed UserId check
            var viewModel = new GetFlashCardViewModel
            {
                Id = flashCard.Id,
                CategoryName = category?.Name ?? "Unknown",
                Question = flashCard.Question,
                Answer = flashCard.Answer,
                UserId = flashCard.UserId
            };

            return Ok(viewModel);
        }

        // [HttpGet("All")]
        // public async Task<ActionResult<IEnumerable<GetFlashCardViewModel>>> GetAllFlashCards()
        // {
        //     // Retrieve all flashcards without user filtering
        //     var allFlashCards = await _context.FlashCards
        //         .Include(f => f.Category)
        //         .ToListAsync();

        //     if (allFlashCards == null || !allFlashCards.Any())
        //         return NotFound("No flashcards found in the database.");

        //     var flashCardViewModels = allFlashCards.Select(flashCard => new GetFlashCardViewModel
        //     {
        //         Id = flashCard.Id,
        //         CategoryName = flashCard.Category?.Name ?? "Unknown",
        //         Question = flashCard.Question,
        //         Answer = flashCard.Answer,
        //         UserId = flashCard.UserId
        //     }).ToList();

        //     return Ok(new
        //     {
        //         TotalCount = flashCardViewModels.Count,
        //         Flashcards = flashCardViewModels
        //     });
        // }
    }
}