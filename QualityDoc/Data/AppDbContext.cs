using Microsoft.EntityFrameworkCore;
using QualityDoc.Pages.Models;

namespace QualityDoc.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Rol> Roles { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<EstadoDocumento> EstadosDocumento { get; set; }
        public DbSet<Documento> Documentos { get; set; }
        public DbSet<HistorialAprobacion> HistorialAprobaciones { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // UUID
            modelBuilder.Entity<Documento>()
                .Property(d => d.Id)
                .HasDefaultValueSql("NEWID()");

            // Relaciones
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.RolId);

            modelBuilder.Entity<Documento>()
                .HasOne(d => d.Autor)
                .WithMany()
                .HasForeignKey(d => d.AutorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Documento>()
                .HasOne(d => d.Estado)
                .WithMany(e => e.Documentos)
                .HasForeignKey(d => d.EstadoId);

            modelBuilder.Entity<HistorialAprobacion>()
                .HasOne(h => h.Documento)
                .WithMany(d => d.Historial)
                .HasForeignKey(h => h.DocumentoId);

            modelBuilder.Entity<HistorialAprobacion>()
                .HasOne(h => h.Usuario)
                .WithMany()
                .HasForeignKey(h => h.UsuarioId);
        }
    }
}
