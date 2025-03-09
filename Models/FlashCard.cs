    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    namespace FlashcardApp.Models 
    {
        public class FlashCard
        {
            public int Id { get; set; } 
            [Required]
            public int CategoryId { get; set; } 

            [Required]
            [StringLength(500)]
            public string Question { get; set; } = string.Empty;

            [Required]
            [StringLength(500)]
            public string Answer { get; set; } = string.Empty;

            public string UserId { get; set; }= string.Empty;

            [ForeignKey("UserId")]
            public ApplicationUser User { get; set; }= null!;

            [ForeignKey("CategoryId")]
            public Category Category { get; set; }= null!;
        }
    }