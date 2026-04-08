using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PhoneDb.Api.Models
{
    [Index(nameof(Number), IsUnique = true)]
    public class PhoneNumberEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(16)]
        public string Number { get; set; } = string.Empty;

        public int CountryCode { get; set; }

        [MaxLength(5)]
        public string? Region { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
