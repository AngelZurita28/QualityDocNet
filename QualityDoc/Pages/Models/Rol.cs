namespace QualityDoc.Pages.Models
{
    public class Rol
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<Usuario> Users { get; set; } = new List<Usuario>();
    }
}
