using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace PlataformaCreditos.Services
{
    public class RabbitMqPublisher
    {
        private readonly IConfiguration _configuration;

        public RabbitMqPublisher(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task PublicarSolicitudRegistradaAsync(
            int solicitudId,
            string usuarioId,
            DateTime fechaEventoUtc)
        {
            var connectionString =
                _configuration["RabbitMq:ConnectionString"];

            var queueName =
                _configuration["RabbitMq:QueueName"]
                ?? "solicitudes.notificaciones";

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "RabbitMq:ConnectionString no está configurado.");
            }

            var messageId = Guid.NewGuid().ToString();

            var mensaje = new
            {
                MessageId = messageId,
                SolicitudId = solicitudId,
                UsuarioId = usuarioId,
                FechaEventoUtc = fechaEventoUtc
            };

            var json = JsonSerializer.Serialize(mensaje);

            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString)
            };

            await using var connection =
                await factory.CreateConnectionAsync();

            await using var channel =
                await connection.CreateChannelAsync(
                    new CreateChannelOptions(
                        publisherConfirmationsEnabled: true,
                        publisherConfirmationTrackingEnabled: true));

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = messageId,
                ContentType = "application/json"
            };

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body);

            Console.WriteLine(
                $"[RabbitMQ] Solicitud publicada. " +
                $"MessageId: {messageId}, " +
                $"SolicitudId: {solicitudId}");
        }
    }
}