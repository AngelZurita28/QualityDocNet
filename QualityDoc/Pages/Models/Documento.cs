namespace QualityDoc.Pages.Models
{
    public class Documento
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }

        public int VersionNumber { get; set; }
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

        public Usuario Author { get; set; }
        public DocumentStatus Status { get; set; }
        public Company Company { get; set; }
        public Department? Department { get; set; }
    }
}
