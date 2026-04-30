namespace QualityDoc.Pages.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<Usuario> Users { get; set; }
    }
}
