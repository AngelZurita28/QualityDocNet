namespace QualityDoc.Pages.Models
{
    public class HistorialAprobacion
    {
        public int Id { get; set; }
        public Guid DocumentoId { get; set; }
        public int UsuarioId { get; set; }
        public string Accion { get; set; }
        public string Comentario { get; set; }
        public DateTime Fecha { get; set; }

        public Documento Documento { get; set; }
        public Usuario Usuario { get; set; }
    }
}
