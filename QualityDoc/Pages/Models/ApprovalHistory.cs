namespace QualityDoc.Pages.Models
{
    public class ApprovalHistory
    {
        public int Id { get; set; }
        public Guid DocumentId { get; set; }
        public int UserId { get; set; }

        public string Action { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; }

        public Documento Document { get; set; } = null!;
        public Usuario User { get; set; } = null!;
    }
}
