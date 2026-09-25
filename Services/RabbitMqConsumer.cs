using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PlataformaCreditos.Services
{
    public class RabbitMqConsumer : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RabbitMqConsumer> _logger;

        public RabbitMqConsumer(
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            ILogger<RabbitMqConsumer> logger)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("RABBITMQ CONSUMER: INICIANDO...");
            Console.WriteLine("========================================");

            var consumerEnabled =
                _configuration.GetValue<bool>(
                    "RabbitMq:ConsumerEnabled");

            Console.WriteLine(
                $"ConsumerEnabled: {consumerEnabled}");

            if (!consumerEnabled)
            {
                _logger.LogInformation(
                    "RabbitMQ Consumer deshabilitado.");

                return;
            }

            var connectionString =
                _configuration["RabbitMq:ConnectionString"];

            var queueName =
                _configuration["RabbitMq:QueueName"]
                ?? "solicitudes.notificaciones";

            Console.WriteLine(
                $"Cola configurada: {queueName}");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError(
                    "RabbitMq:ConnectionString no está configurado.");

                return;
            }

            try
            {
                Console.WriteLine(
                    "Conectando con CloudAMQP...");

                var factory = new ConnectionFactory
                {
                    Uri = new Uri(connectionString)
                };

                await using var connection =
                    await factory.CreateConnectionAsync(
                        cancellationToken: stoppingToken);

                Console.WriteLine(
                    "CONEXIÓN CON CLOUDAMQP EXITOSA.");

                await using var channel =
                    await connection.CreateChannelAsync(
                        cancellationToken: stoppingToken);

                Console.WriteLine(
                    "CANAL RABBITMQ CREADO.");

                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                Console.WriteLine(
                    $"COLA VERIFICADA: {queueName}");

                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: 1,
                    global: false,
                    cancellationToken: stoppingToken);

                var consumer =
                    new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (sender, ea) =>
                {
                    await ProcesarMensajeAsync(
                        channel,
                        ea,
                        stoppingToken);
                };

                await channel.BasicConsumeAsync(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                Console.WriteLine(
                    "========================================");
                Console.WriteLine(
                    "RABBITMQ CONSUMER INICIADO CORRECTAMENTE");
                Console.WriteLine(
                    $"ESCUCHANDO: {queueName}");
                Console.WriteLine(
                    "========================================");

                _logger.LogInformation(
                    "RabbitMQ Consumer iniciado. Cola: {QueueName}",
                    queueName);

                await Task.Delay(
                    Timeout.Infinite,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "RabbitMQ Consumer detenido.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "========================================");
                Console.WriteLine(
                    "ERROR DEL RABBITMQ CONSUMER");
                Console.WriteLine(
                    ex.Message);
                Console.WriteLine(
                    "========================================");

                _logger.LogError(
                    ex,
                    "Error iniciando RabbitMQ Consumer.");
            }
        }

        private async Task ProcesarMensajeAsync(
            IChannel channel,
            BasicDeliverEventArgs ea,
            CancellationToken stoppingToken)
        {
            try
            {
                var json = Encoding.UTF8.GetString(
                    ea.Body.ToArray());

                Console.WriteLine(
                    $"Mensaje recibido: {json}");

                var mensaje =
                    JsonSerializer.Deserialize<
                        SolicitudRegistradaMensaje>(
                            json);

                // Validación del mensaje
                if (mensaje == null ||
                    string.IsNullOrWhiteSpace(
                        mensaje.MessageId) ||
                    mensaje.SolicitudId <= 0 ||
                    string.IsNullOrWhiteSpace(
                        mensaje.UsuarioId))
                {
                    _logger.LogWarning(
                        "Mensaje inválido. Se rechaza sin requeue.");

                    await channel.BasicRejectAsync(
                        ea.DeliveryTag,
                        requeue: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                using var scope =
                    _scopeFactory.CreateScope();

                var context =
                    scope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();

                // Verificar duplicado por MessageId
                var existe =
                    await context.Notificaciones
                        .AnyAsync(
                            n => n.MessageId ==
                                mensaje.MessageId,
                            stoppingToken);

                if (existe)
                {
                    _logger.LogInformation(
                        "Mensaje duplicado detectado. " +
                        "MessageId: {MessageId}",
                        mensaje.MessageId);

                    // Ya fue procesado anteriormente.
                    // ACK para eliminar la copia duplicada.
                    await channel.BasicAckAsync(
                        ea.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    return;
                }

                // Crear notificación
                var notificacion = new Notificacion
                {
                    MessageId = mensaje.MessageId,
                    SolicitudId = mensaje.SolicitudId,
                    UsuarioId = mensaje.UsuarioId,
                    Tipo = "SolicitudRegistrada",
                    FechaEventoUtc = mensaje.FechaEventoUtc,
                    FechaCreacionUtc = DateTime.UtcNow,
                    Contenido = json
                };

                context.Notificaciones.Add(
                    notificacion);

                // Guardar primero
                await context.SaveChangesAsync(
                    stoppingToken);

                Console.WriteLine(
                    $"Notificación guardada. " +
                    $"MessageId: {mensaje.MessageId}");

                // ACK SOLO después de guardar
                await channel.BasicAckAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                Console.WriteLine(
                    $"ACK enviado. " +
                    $"MessageId: {mensaje.MessageId}");

                _logger.LogInformation(
                    "Notificación procesada correctamente. " +
                    "MessageId: {MessageId}, SolicitudId: {SolicitudId}",
                    mensaje.MessageId,
                    mensaje.SolicitudId);
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "JSON inválido. " +
                    "Se rechaza el mensaje sin requeue.");

                await RechazarMensajeAsync(
                    channel,
                    ea.DeliveryTag,
                    stoppingToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Error guardando la notificación. " +
                    "Se rechaza sin requeue.");

                await RechazarMensajeAsync(
                    channel,
                    ea.DeliveryTag,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error procesando mensaje. " +
                    "Se rechaza sin requeue para evitar " +
                    "reintentos infinitos.");

                await RechazarMensajeAsync(
                    channel,
                    ea.DeliveryTag,
                    stoppingToken);
            }
        }

        private async Task RechazarMensajeAsync(
            IChannel channel,
            ulong deliveryTag,
            CancellationToken stoppingToken)
        {
            try
            {
                await channel.BasicRejectAsync(
                    deliveryTag,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "No se pudo rechazar el mensaje.");
            }
        }

        private class SolicitudRegistradaMensaje
        {
            public string MessageId { get; set; } = "";

            public int SolicitudId { get; set; }

            public string UsuarioId { get; set; } = "";

            public DateTime FechaEventoUtc { get; set; }
        }
    }
}