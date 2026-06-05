namespace QualityDoc.Pages.Models
{
    public class DocumentStatus
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<Documento> Documents { get; set; } = new List<Documento>();
    }
}
