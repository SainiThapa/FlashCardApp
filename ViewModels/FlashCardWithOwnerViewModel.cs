namespace FlashcardApp.ViewModels
{
    public class FlashCardWithOwnerViewModel
    {
        public int FlashCardId { get; set; }
        public string CategoryName { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public string Owner_FullName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
    }
}