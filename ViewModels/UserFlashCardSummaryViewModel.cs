namespace FlashcardApp.ViewModels
{
    public class UserFlashCardsSummaryViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<FlashCardViewModel> FlashCards { get; set; } = new List<FlashCardViewModel>();
    }
}