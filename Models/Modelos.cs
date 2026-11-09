namespace NutriLesiones.Api.Models;

/// <summary>Alimento del catálogo (viene de Data/alimentos.json).</summary>
public sealed class Alimento
{
    public string Id { get; set; } = "";
    public string Grupo { get; set; } = "";
    public string Subgrupo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public string PesoG { get; set; } = "";
    public string MedidaCasera { get; set; } = "";
    public List<string> Comidas { get; set; } = new();
    public string? Foto { get; set; }
    public string? FotoReferencia { get; set; }   // texto si la foto es de un alimento parecido, no del mismo
    public string? Nota { get; set; }
}

public sealed record Requerimientos(
    double Kcal, double ChoG, double ProtG, double GrasaG,
    int KcalPorKg, int ProtPct, int ChoPct, int GrasaPct);

/// <summary>
/// Un subgrupo de intercambios (ej. "Cereales / Altos en CHO y bajos en grasas").
/// Los valores Cho/Prot/Grasa/Kcal son por 1 porción y vienen del libro de intercambios (USFQ).
/// Las porciones las asigna PorcionesService (método de Belén).
/// </summary>
public sealed class GrupoIntercambio
{
    public required string Grupo { get; init; }
    public required string Subgrupo { get; init; }
    public double Cho { get; init; }
    public double Prot { get; init; }
    public double Grasa { get; init; }
    public double Kcal { get; init; }
    public double[] PesosComida { get; init; } = new double[5]; // reparto deseado Desayuno,MM,Almuerzo,MT,Cena
    public double Porciones { get; set; }
}

public static class GruposIntercambio
{
    public const string Azucares = "Azúcares y alimentos azucarados";

    // Reparto por comida de cada subgrupo (Desayuno, Media mañana, Almuerzo, Media tarde, Cena); editable aquí.
    static readonly double[] Carnes = { 0.20, 0, 0.45, 0, 0.35 };

    /// <summary>Crea todos los subgrupos que usa el programa (siempre instancias nuevas).</summary>
    public static List<GrupoIntercambio> Crear() => new()
    {
        new() { Grupo = "Carnes, pescados y huevos", Subgrupo = "Bajos en grasas",
                Cho = 0, Prot = 11, Grasa = 2, Kcal = 65, PesosComida = Carnes },
        new() { Grupo = "Carnes, pescados y huevos", Subgrupo = "Medios en grasas",
                Cho = 0, Prot = 11, Grasa = 7, Kcal = 110, PesosComida = Carnes },
        new() { Grupo = "Cereales", Subgrupo = "Altos en CHO y bajos en grasas",
                Cho = 30, Prot = 4, Grasa = 1, Kcal = 150, PesosComida = new[] { 0.30, 0.10, 0.40, 0.05, 0.15 } },
        // Valores por porción tomados del libro USFQ (p. 10). Ojo: en el Excel del nutricionista estos dos están cruzados.
        new() { Grupo = "Cereales", Subgrupo = "Medios en CHO y bajos en grasas",   // pan de rodaja, tortilla...: desayuno y media tarde
                Cho = 20, Prot = 3, Grasa = 1, Kcal = 105, PesosComida = new[] { 0.60, 0, 0, 0.40, 0 } },
        new() { Grupo = "Cereales", Subgrupo = "Medios en CHO y medios en grasas",  // chulpi, tostado, canguil: media mañana
                Cho = 20, Prot = 2, Grasa = 6, Kcal = 145, PesosComida = new[] { 0, 1.0, 0, 0, 0 } },
        new() { Grupo = "Leguminosas", Subgrupo = "Leguminosas",
                Cho = 20, Prot = 9, Grasa = 1, Kcal = 120, PesosComida = new[] { 0, 0, 0.5, 0, 0.5 } },
        new() { Grupo = "Lácteos", Subgrupo = "Semidescremados",
                Cho = 11, Prot = 8, Grasa = 4, Kcal = 110, PesosComida = new[] { 0.5, 0, 0, 0.5, 0 } },
        new() { Grupo = "Lácteos", Subgrupo = "Descremados (bajos en grasas)",     // así cada comida lleva un solo lácteo
                Cho = 12, Prot = 8, Grasa = 1, Kcal = 90, PesosComida = new[] { 0, 1.0, 0, 0, 0 } },
        new() { Grupo = "Vegetales", Subgrupo = "Bajos en CHO",
                Cho = 5, Prot = 1, Grasa = 0, Kcal = 30, PesosComida = new[] { 0, 0, 0.55, 0, 0.45 } },   // verduras: almuerzo y cena
        new() { Grupo = "Frutas", Subgrupo = "Medias en CHO",
                Cho = 20, Prot = 1, Grasa = 0, Kcal = 90, PesosComida = new[] { 0.25, 0.20, 0.20, 0.20, 0.15 } },
        new() { Grupo = "Frutas", Subgrupo = "Bajas en CHO",
                Cho = 10, Prot = 1, Grasa = 0, Kcal = 50, PesosComida = new[] { 0.25, 0.20, 0.20, 0.20, 0.15 } },
        new() { Grupo = "Grasas y frutos secos", Subgrupo = "Altos en grasas",
                Cho = 1, Prot = 1, Grasa = 12, Kcal = 120, PesosComida = new[] { 0.20, 0.125, 0.30, 0.125, 0.25 } },
        new() { Grupo = "Grasas y frutos secos", Subgrupo = "Medios en grasas",   // aguacate
                Cho = 2, Prot = 1, Grasa = 7, Kcal = 75, PesosComida = new[] { 0.30, 0, 0.35, 0, 0.35 } },
        new() { Grupo = Azucares, Subgrupo = Azucares,
                Cho = 10, Prot = 0, Grasa = 0, Kcal = 40, PesosComida = new[] { 0, 0, 0, 1.0, 0 } },
    };
}
