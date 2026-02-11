using System.ComponentModel.DataAnnotations;

namespace MiniServiceDesk.Api.Dtos
{
    public class CreateTicketRequest
    {
        [Required]
        [MinLength(4)]
        [MaxLength(120)]
        [RegularExpression(@".*\S.*")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MinLength(10)]
        [MaxLength(4000)]
        [RegularExpression(@".*\S.*")]
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "IT";
        public int Priority { get; set; } = 1;
        public DateTime? DueAt { get; set; }
    }
}
