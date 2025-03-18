using Microsoft.AspNetCore.Identity;

namespace FlashcardApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PlainTextPassword { get; set; } = string.Empty;

        public ICollection<FlashCard> FlashCards { get; set; } = new List<FlashCard>();

    }
}
