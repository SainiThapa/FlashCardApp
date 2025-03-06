namespace FlashcardApp.ViewModels.APIViewModels
{
    public class CreateFlashCardViewModel
    {
        public int CategoryId { get; set; } 
        public string Question { get; set; }
        public string Answer { get; set; }
    }

    public class UpdateFlashCardViewModel
    {
        public int CategoryId { get; set; } 
        public string Question { get; set; }
        public string Answer { get; set; }
    }
}