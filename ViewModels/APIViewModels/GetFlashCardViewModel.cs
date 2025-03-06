namespace FlashcardApp.ViewModels.APIViewModels
{
    public class GetFlashCardViewModel
    {
        public int Id { get; set; }
        public string CategoryName { get; set; } = string.Empty;         
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }
}