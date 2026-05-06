using Microsoft.EntityFrameworkCore;
using QualityDoc.Pages.Models;

namespace QualityDoc.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Usuario> Users { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<DocumentStatus> DocumentStatus { get; set; }
        public DbSet<Documento> Documents { get; set; }
        public DbSet<ApprovalHistory> ApprovalHistory { get; set; }
        public DbSet<Department> Departments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
            modelBuilder.Entity<Documento>()
                .Property(d => d.Id)
                .HasDefaultValueSql("NEWID()");

            modelBuilder.Entity<Documento>()
                .HasOne(d => d.Author)
                .WithMany()
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Documento>()
                .HasOne(d => d.Status)
                .WithMany(s => s.Documents)
                .HasForeignKey(d => d.StatusId);

            modelBuilder.Entity<Documento>()
                .HasOne(d => d.Company)
                .WithMany()
                .HasForeignKey(d => d.CompanyId);

            modelBuilder.Entity<Documento>()
                .HasOne<Documento>()
                .WithMany()
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Documento>()
                .HasOne(d => d.Department)
                .WithMany(dp => dp.Documents)
                .HasForeignKey(d => d.DepartmentId);

            // Seed Departments
            modelBuilder.Entity<Department>().HasData(
                new Department { Id = 1, Name = "Calidad" },
                new Department { Id = 2, Name = "Producción" },
                new Department { Id = 3, Name = "Recursos Humanos" },
                new Department { Id = 4, Name = "Contabilidad" },
                new Department { Id = 5, Name = "Compras" },
                new Department { Id = 6, Name = "Sistemas" }
            );

            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId);

            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId);

            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Department)
                .WithMany()
                .HasForeignKey(u => u.DepartmentId);

            
            modelBuilder.Entity<ApprovalHistory>()
                .HasOne(h => h.Document)
                .WithMany()
                .HasForeignKey(h => h.DocumentId);

            modelBuilder.Entity<ApprovalHistory>()
                .HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId);
        }
    }
}