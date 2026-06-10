namespace QualityDoc.Pages.Models
{
    public class Documento
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }

        public decimal? VersionNumber { get; set; }
        public bool IsLatest { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? FilePath { get; set; }

        public int AuthorId { get; set; }
        public int StatusId { get; set; }
        public int? CompanyId { get; set; }
        public int? DepartmentId { get; set; }
        public string? DocumentCode { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool SyncPostgre { get; set; }
        public bool SyncFirebase { get; set; }

        public string? LastErrorLog { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Usuario Author { get; set; } = null!;
        public DocumentStatus Status { get; set; } = null!;
        public Company Company { get; set; } = null!;
        public Department? Department { get; set; }
    }
}
