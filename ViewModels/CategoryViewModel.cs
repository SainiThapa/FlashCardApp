using System.ComponentModel.DataAnnotations;

namespace FlashcardApp.ViewModels
{
    public class CategoryViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;
    }

    public class CategoryManagementViewModel
    {
        public List<CategoryViewModel> Categories { get; set; } = new List<CategoryViewModel>();
        public CategoryViewModel NewCategory { get; set; } = new CategoryViewModel();
    }
}