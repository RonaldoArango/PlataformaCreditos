using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Hubs;
using PlataformaCreditos.Models;
using PlataformaCreditos.Services;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// DATABASE
// ======================================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ======================================================
// IDENTITY + ROLES
// ======================================================

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ======================================================
// REDIS / CACHE
// ======================================================

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        var redisConnection =
            builder.Configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Redis:ConnectionString no configurado.");

        // Render entrega la URL como:
        // redis://host:6379
        //
        // StackExchange.Redis necesita:
        // host:6379

        if (redisConnection.StartsWith("redis://"))
        {
            redisConnection =
                redisConnection.Substring("redis://".Length);
        }

        options.Configuration = redisConnection;
        options.InstanceName = "PlataformaCreditos:";
    });
}

// ======================================================
// SESSION
// ======================================================

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ======================================================
// MVC
// ======================================================

builder.Services.AddControllersWithViews();

// ======================================================
// RABBITMQ
// ======================================================

// Publisher
builder.Services.AddSingleton<RabbitMqPublisher>();

// Consumer
builder.Services.AddHostedService<RabbitMqConsumer>();

// ======================================================
// SIGNALR
// ======================================================

builder.Services.AddSignalR();

// ======================================================
// PORT
// ======================================================

builder.WebHost.UseUrls(
    $"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "10000"}"
);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// ======================================================
// SEED INICIAL
// ======================================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context =
        services.GetRequiredService<ApplicationDbContext>();

    var userManager =
        services.GetRequiredService<UserManager<IdentityUser>>();

    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    // -------------------------------
    // Crear rol Analista
    // -------------------------------

    if (!await roleManager.RoleExistsAsync("Analista"))
    {
        await roleManager.CreateAsync(
            new IdentityRole("Analista"));
    }

    // -------------------------------
    // Crear usuario Analista
    // -------------------------------

    var analista =
        await userManager.FindByEmailAsync(
            "analista@credito.com");

    if (analista == null)
    {
        analista = new IdentityUser
        {
            UserName = "analista@credito.com",
            Email = "analista@credito.com",
            EmailConfirmed = true
        };

        var resultado =
            await userManager.CreateAsync(
                analista,
                "Analista123!");

        if (resultado.Succeeded)
        {
            await userManager.AddToRoleAsync(
                analista,
                "Analista");
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(
                analista,
                "Analista"))
        {
            await userManager.AddToRoleAsync(
                analista,
                "Analista");
        }
    }

    // -------------------------------
    // Crear Cliente Usuario 1
    // -------------------------------

    var usuario1 =
        await userManager.FindByEmailAsync(
            "cliente1@credito.com");

    if (usuario1 == null)
    {
        usuario1 = new IdentityUser
        {
            UserName = "cliente1@credito.com",
            Email = "cliente1@credito.com",
            EmailConfirmed = true
        };

        await userManager.CreateAsync(
            usuario1,
            "Cliente123!");
    }

    // -------------------------------
    // Crear Cliente Usuario 2
    // -------------------------------

    var usuario2 =
        await userManager.FindByEmailAsync(
            "cliente2@credito.com");

    if (usuario2 == null)
    {
        usuario2 = new IdentityUser
        {
            UserName = "cliente2@credito.com",
            Email = "cliente2@credito.com",
            EmailConfirmed = true
        };

        await userManager.CreateAsync(
            usuario2,
            "Cliente123!");
    }

    // -------------------------------
    // Crear Cliente 1
    // -------------------------------

    var cliente1 =
        await context.Clientes
            .FirstOrDefaultAsync(
                c => c.UsuarioId == usuario1.Id);

    if (cliente1 == null)
    {
        cliente1 = new Cliente
        {
            UsuarioId = usuario1.Id,
            IngresosMensuales = 3000,
            Activo = true
        };

        context.Clientes.Add(cliente1);
    }

    // -------------------------------
    // Crear Cliente 2
    // -------------------------------

    var cliente2 =
        await context.Clientes
            .FirstOrDefaultAsync(
                c => c.UsuarioId == usuario2.Id);

    if (cliente2 == null)
    {
        cliente2 = new Cliente
        {
            UsuarioId = usuario2.Id,
            IngresosMensuales = 5000,
            Activo = true
        };

        context.Clientes.Add(cliente2);
    }

    await context.SaveChangesAsync();

    // -------------------------------
    // Crear solicitudes iniciales
    // -------------------------------

    if (!await context.SolicitudesCredito.AnyAsync())
    {
        context.SolicitudesCredito.Add(
            new SolicitudCredito
            {
                ClienteId = cliente1.Id,
                MontoSolicitado = 5000,
                FechaSolicitud = DateTime.UtcNow,
                Estado = EstadoSolicitud.Pendiente
            });

        context.SolicitudesCredito.Add(
            new SolicitudCredito
            {
                ClienteId = cliente2.Id,
                MontoSolicitado = 10000,
                FechaSolicitud =
                    DateTime.UtcNow.AddDays(-1),
                Estado = EstadoSolicitud.Aprobado
            });

        await context.SaveChangesAsync();
    }
}

// ======================================================
// HTTP PIPELINE
// ======================================================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// En desarrollo HTTP no necesitamos forzar HTTPS.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseWebSockets();

app.UseRouting();

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

// ======================================================
// SIGNALR
// ======================================================

app.MapHub<SolicitudesHub>(
    "/hubs/solicitudes");

// ======================================================
// MVC
// ======================================================

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

// ======================================================
// START
// ======================================================

app.Run();