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

        // Relación con Documentos
        public ICollection<Documento> Documents { get; set; } = new List<Documento>();
    }
}
