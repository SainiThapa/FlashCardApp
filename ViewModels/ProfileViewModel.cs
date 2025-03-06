using System.ComponentModel.DataAnnotations;

namespace FlashcardApp.ViewModels
{
    public class ProfileViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Display(Name = "First Name")]
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Last Name")]
        [Required]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Total Cards")]
        public int TaskCount { get; set; }
    }
}
