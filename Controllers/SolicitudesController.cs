using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Controllers
{
    [Authorize]
    public class SolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;

        public SolicitudesController(
            ApplicationDbContext context,
            IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // MIS SOLICITUDES + CACHE REDIS 60 SEGUNDOS
        public async Task<IActionResult> Index()
        {
            var usuarioId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

            if (cliente == null)
                return NotFound();

            string cacheKey =
                $"solicitudes_usuario_{usuarioId}";

            var cacheData =
                await _cache.GetStringAsync(cacheKey);

            List<SolicitudCredito> solicitudes;

            if (cacheData != null)
            {
                solicitudes =
                    JsonSerializer.Deserialize<List<SolicitudCredito>>(
                        cacheData
                    ) ?? new List<SolicitudCredito>();
            }
            else
            {
                solicitudes = await _context.SolicitudesCredito
                    .Where(s => s.ClienteId == cliente.Id)
                    .OrderByDescending(s => s.FechaSolicitud)
                    .ToListAsync();

                var opciones = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromSeconds(60)
                };

                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(solicitudes),
                    opciones);
            }

            return View(solicitudes);
        }

        // DETALLE + ÚLTIMA SOLICITUD EN SESSION
        public async Task<IActionResult> Details(int id)
        {
            var usuarioId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            var solicitud = await _context.SolicitudesCredito
                .Include(s => s.Cliente)
                .FirstOrDefaultAsync(s =>
                    s.Id == id &&
                    s.Cliente!.UsuarioId == usuarioId);

            if (solicitud == null)
                return NotFound();

            HttpContext.Session.SetInt32(
                "UltimaSolicitudId",
                solicitud.Id);

            HttpContext.Session.SetString(
                "UltimaSolicitudMonto",
                solicitud.MontoSolicitado.ToString("F2"));

            return View(solicitud);
        }

        // FORMULARIO
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // REGISTRAR SOLICITUD
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            decimal montoSolicitado)
        {
            var usuarioId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

            if (cliente == null)
            {
                ViewBag.Error =
                    "No existe un cliente asociado.";

                return View();
            }

            if (!cliente.Activo)
            {
                ViewBag.Error =
                    "El cliente está inactivo.";

                return View();
            }

            if (montoSolicitado <= 0)
            {
                ViewBag.Error =
                    "El monto debe ser mayor que 0.";

                return View();
            }

            var pendiente = await _context.SolicitudesCredito
                .AnyAsync(s =>
                    s.ClienteId == cliente.Id &&
                    s.Estado == EstadoSolicitud.Pendiente);

            if (pendiente)
            {
                ViewBag.Error =
                    "Ya tienes una solicitud pendiente.";

                return View();
            }

            if (montoSolicitado >
                cliente.IngresosMensuales * 10)
            {
                ViewBag.Error =
                    "El monto no puede superar 10 veces tus ingresos.";

                return View();
            }

            var solicitud = new SolicitudCredito
            {
                ClienteId = cliente.Id,
                MontoSolicitado = montoSolicitado,
                FechaSolicitud = DateTime.UtcNow,
                Estado = EstadoSolicitud.Pendiente
            };

            _context.SolicitudesCredito.Add(solicitud);

            await _context.SaveChangesAsync();

            // INVALIDAR CACHE
            await _cache.RemoveAsync(
                $"solicitudes_usuario_{usuarioId}");

            TempData["Mensaje"] =
                "Solicitud registrada correctamente.";

            return RedirectToAction(nameof(Index));
        }
    }
}