namespace QualityDoc.Pages.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public int RoleId { get; set; }
        public int? CompanyId { get; set; }
        public int? DepartmentId { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public Rol Rol { get; set; } = null!;
        public Company? Company { get; set; } 
        public Department? Department { get; set; }
    }
}
