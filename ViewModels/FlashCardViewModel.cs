using Microsoft.AspNetCore.Mvc.Rendering;
namespace FlashcardApp.ViewModels
{
    public class FlashCardViewModel
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }        
        public string CategoryName { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }
        public List<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }
}