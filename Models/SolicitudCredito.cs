using System.ComponentModel.DataAnnotations;

namespace PlataformaCreditos.Models
{
    public enum EstadoSolicitud
    {
        Pendiente,
        Aprobado,
        Rechazado
    }

    public class SolicitudCredito
    {
        public int Id { get; set; }

        public int ClienteId { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal MontoSolicitado { get; set; }

        public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

        public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

        public string? MotivoRechazo { get; set; }

        public Cliente? Cliente { get; set; }
    }
}