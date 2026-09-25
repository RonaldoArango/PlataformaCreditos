using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Services;

namespace PlataformaCreditos.Controllers
{
    [Authorize]
    public class NotificacionesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificacionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var usuarioId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            var notificaciones = await _context.Notificaciones
                .Where(n => n.UsuarioId == usuarioId)
                .OrderByDescending(n => n.FechaCreacionUtc)
                .ToListAsync();

            return View(notificaciones);
        }
    }
}