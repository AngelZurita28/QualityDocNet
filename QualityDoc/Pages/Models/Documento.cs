namespace QualityDoc.Pages.Models
{
    public class Documento
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string RutaArchivo { get; set; }

        public int AutorId { get; set; }
        public int EstadoId { get; set; }
        public DateTime FechaCreacion { get; set; }

        public Usuario Autor { get; set; }
        public EstadoDocumento Estado { get; set; }

        public ICollection<HistorialAprobacion> Historial { get; set; }
    }
}
