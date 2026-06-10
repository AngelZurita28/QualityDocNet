using System.ComponentModel.DataAnnotations;

namespace QualityDoc.Pages.Models
{
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        public int CompanyId { get; set; }

        public Company Company { get; set; } = null!;
        public ICollection<Documento> Documents { get; set; } = new List<Documento>();
    }
}
