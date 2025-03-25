using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace FlashcardApp.ViewModels
{
    public class AdminUserFlashCardViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int CategoryId { get; set; }        
        public string CategoryName { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public List<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
    }
}