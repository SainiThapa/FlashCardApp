using Microsoft.AspNetCore.Mvc.Rendering;
namespace FlashcardApp.ViewModels
{
    public class FlashCardViewModel
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }        
        public string CategoryName { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public List<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }
}