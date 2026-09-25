using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PlataformaCreditos.Hubs
{
    [Authorize]
    public class SolicitudesHub : Hub
    {
    }
}