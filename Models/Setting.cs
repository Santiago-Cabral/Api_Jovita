using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForrajeriaJovitaAPI.Models
{
    [Table("Settings")]
    public class Setting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
    }
}