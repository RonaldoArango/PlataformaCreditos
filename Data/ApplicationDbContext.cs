using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Cliente> Clientes { get; set; }

        public DbSet<SolicitudCredito> SolicitudesCredito { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Cliente>()
                .HasIndex(c => c.UsuarioId)
                .IsUnique();

            builder.Entity<Cliente>()
                .Property(c => c.IngresosMensuales)
                .HasPrecision(18, 2);

            builder.Entity<SolicitudCredito>()
                .Property(s => s.MontoSolicitado)
                .HasPrecision(18, 2);

            builder.Entity<SolicitudCredito>()
                .HasOne(s => s.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);

            // Un cliente no puede tener más de una solicitud Pendiente
            builder.Entity<SolicitudCredito>()
                .HasIndex(s => new { s.ClienteId, s.Estado })
                .IsUnique()
                .HasFilter("[Estado] = 0");
        }
    }
}