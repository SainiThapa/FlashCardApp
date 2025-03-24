using System.ComponentModel.DataAnnotations;

namespace FlashcardApp.ViewModels.APIViewModels
{
    public class CreateFlashCardViewModel
    {
        [Required]
        public string Question { get; set; }

        [Required]
        public string Answer { get; set; }

        [Required]
        public int CategoryId { get; set; }
    }

    public class UpdateFlashCardViewModel
    {
        public int CategoryId { get; set; } 
        public string Question { get; set; }
        public string Answer { get; set; }
    }
}