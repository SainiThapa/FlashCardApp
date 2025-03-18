using System.ComponentModel.DataAnnotations;
namespace FlashcardApp.ViewModels
{

    public class UpdatePasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

}