namespace PlataformaCreditos.Models
{
    public class Notificacion
    {
        public int Id { get; set; }

        public string MessageId { get; set; } = "";

        public int SolicitudId { get; set; }

        public string UsuarioId { get; set; } = "";

        public string Tipo { get; set; } = "";

        public DateTime FechaEventoUtc { get; set; }

        public DateTime FechaCreacionUtc { get; set; }
            = DateTime.UtcNow;

        public string? Contenido { get; set; }
    }
}