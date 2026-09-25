# PlataformaCreditos

Aplicación web de gestión de solicitudes de crédito desarrollada con ASP.NET Core MVC .NET 10.

## Tecnologías

- ASP.NET Core MVC .NET 10
- ASP.NET Core Identity
- Entity Framework Core
- SQLite
- Redis
- SignalR / WebSocket
- RabbitMQ / CloudAMQP
- Docker
- Render

## Funcionalidades

- Registro y consulta de solicitudes de crédito.
- Validaciones de solicitudes.
- Panel de Analista para aprobar o rechazar solicitudes.
- Sesión y caché utilizando Redis.
- Notificaciones en tiempo real mediante SignalR/WebSocket.
- Mensajería asíncrona mediante RabbitMQ y CloudAMQP.
- Notificaciones persistidas en SQLite.
- Control de mensajes duplicados mediante MessageId.

## WebSocket

El Hub de SignalR se encuentra en:

`/hubs/solicitudes`

Cuando una solicitud cambia de estado, el propietario recibe la actualización sin recargar la página.

## RabbitMQ / CloudAMQP

La cola utilizada es:

`solicitudes.notificaciones`

El consumidor se ejecuta mediante un `BackgroundService` y utiliza ACK manual después de guardar correctamente la notificación.

Se utiliza `MessageId` único para evitar notificaciones duplicadas.

## Redis

Redis se utiliza para:

- Guardar la última solicitud visitada.
- Almacenar temporalmente la lista de solicitudes.
- Invalidar la caché cuando corresponde.

## Despliegue

La aplicación está desplegada en Render como Web Service mediante Docker.

Variables principales:

- `ASPNETCORE_ENVIRONMENT`
- `ASPNETCORE_URLS`
- `ConnectionStrings__DefaultConnection`
- `Redis__ConnectionString`
- `RabbitMq__ConnectionString`
- `RabbitMq__QueueName`
- `RabbitMq__ConsumerEnabled`

Las credenciales y valores sensibles se mantienen como variables de entorno.

## Pruebas de Cloud MQ

1. Desactivar temporalmente `RabbitMq__ConsumerEnabled`.
2. Registrar una solicitud pendiente.
3. Comprobar que el mensaje queda pendiente en CloudAMQP.
4. Reactivar el consumidor.
5. Verificar que la cola se vacía.
6. Comprobar que aparece una sola notificación.
7. Reenviar el mismo `MessageId` y verificar que no se genera un duplicado.

## Persistencia

La aplicación utiliza SQLite mediante:

`Data Source=app.db`

Las migraciones de Entity Framework se aplican al iniciar la aplicación.

## Usuarios de prueba

### Cliente

`cliente1@credito.com`

### Analista

`analista@credito.com`