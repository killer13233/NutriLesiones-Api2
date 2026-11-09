using NutriLesiones.Api.Models;
using NutriLesiones.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// CORS: por defecto abierto (demo). En producción define CORS_ORIGINS="https://tu-front.vercel.app"
var origenes = builder.Configuration["CORS_ORIGINS"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (origenes is { Length: > 0 }) p.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod();
    else p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddSingleton<CatalogoService>();
builder.Services.AddSingleton<MenuService>();

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles(); // "/" abre wwwroot/index.html (página de prueba)
app.UseStaticFiles();  // sirve las fotos: /imagenes/archivo.jpg

app.MapGet("/api", () => Results.Ok(new { api = "NutriLesiones", estado = "ok", docs = "POST /api/plan, GET /api/catalogos, GET /api/alimentos" }));
app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok" }));

app.MapGet("/api/catalogos", () => Results.Ok(new
{
    fases = new[]
    {
        new { id = "inflamatoria", nombre = "Inflamatoria" },
        new { id = "proliferativa", nombre = "Proliferativa" },
        new { id = "remodelacion", nombre = "Remodelación" }
    },
    lecciones = new[]
    {
        new { id = "contracturas", nombre = "Contracturas musculares" },
        new { id = "desgarros", nombre = "Desgarros musculares" },
        new { id = "tendinosas", nombre = "Lesiones tendinosas y ligamentos" }
    },
    comidas = CatalogoService.ComidaIds.Select((id, i) => new
    {
        id, nombre = CatalogoService.ComidaNombres[i], horario = CatalogoService.ComidaHorarios[i]
    })
}));

app.MapGet("/api/alimentos", (string? grupo, string? comida, CatalogoService c) =>
{
    var q = c.Alimentos.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(grupo)) q = q.Where(a => Texto.Norm(a.Grupo).Contains(Texto.Norm(grupo)));
    if (!string.IsNullOrWhiteSpace(comida)) q = q.Where(a => a.Comidas.Contains(comida, StringComparer.OrdinalIgnoreCase));
    return Results.Ok(q);
});

// Calcula requerimientos, porciones por grupo, reparto en las 5 comidas y el menú con alimentos y fotos.
// "semilla" es opcional: la misma semilla da el mismo menú; sin semilla cada llamada genera un menú distinto.
app.MapPost("/api/plan", (PlanRequest req, MenuService menu) =>
{
    if (req.Peso < 25 || req.Peso > 150)
        return Results.BadRequest(new { error = "El peso debe estar entre 25 y 150 kg." });
    if (req.Edad is { } edad && (edad < 12 || edad > 19))
        return Results.BadRequest(new { error = "La edad debe estar entre 12 y 19 años." });
    var fase = RequerimientosService.ParsearFase(req.Fase);
    if (fase == null)
        return Results.BadRequest(new { error = "Fase no válida. Usa: inflamatoria, proliferativa o remodelacion." });

    var semilla = req.Semilla ?? Random.Shared.Next(1, int.MaxValue);
    return Results.Ok(menu.CrearPlan(req.Peso, fase, req.Leccion, semilla));
});

app.Run();
