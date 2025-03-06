using System.ComponentModel.DataAnnotations;
namespace FlashcardApp.ViewModels
{

    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

}