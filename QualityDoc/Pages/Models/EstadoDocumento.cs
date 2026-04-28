namespace QualityDoc.Pages.Models
{
    public class EstadoDocumento
    {
        public int Id { get; set; }
        public string Nombre { get; set; }

        public ICollection<Documento> Documentos { get; set; }
    }
}
