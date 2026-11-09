namespace NutriLesiones.Api.Models;

/// <summary>"Edad" es opcional y no cambia el cálculo; si viene, debe estar entre 12 y 19 años.</summary>
public sealed record PlanRequest(double Peso, string? Fase, string? Leccion, int? Semilla, int? Edad = null);

public sealed record MacrosDto(double Kcal, double Cho, double Proteina, double Grasa);

public sealed record RequerimientosDto(double Kcal, double Carbohidratos, double Proteina, double Grasa,
    int KcalPorKg, int PctProteina, int PctCarbohidratos, int PctGrasa);

public sealed record AdecuacionDto(double Carbohidratos, double Proteina, double Grasa, double Kcal, bool EnRango90a110);

public sealed record GrupoPorcionDto(string Grupo, string Subgrupo, double PorcionesTotal, double[] PorComida);

public sealed record AlternativaDto(string Id, string Nombre, string MedidaPorPorcion, string PesoPorPorcionG, string? Foto, string? FotoReferencia);

/// <summary>Un alimento del menú. "Alternativas" son los intercambios: alimentos del mismo subgrupo,
/// permitidos en esa comida, que se pueden usar en su lugar con la misma cantidad de porciones.</summary>
public sealed record AlimentoMenuDto(string Id, string Nombre, string Grupo, string Subgrupo,
    double Porciones, string MedidaPorPorcion, string PesoPorPorcionG, string? Foto, string? FotoReferencia, string? Nota,
    List<AlternativaDto> Alternativas);

/// <summary>"Indicacion" sugiere cómo combinar los alimentos de la comida en un plato (texto breve, no es una receta).</summary>
public sealed record ComidaDto(string Id, string Nombre, string Horario, List<AlimentoMenuDto> Alimentos, MacrosDto Aporte, string? Indicacion = null);

public sealed record PlanResponse(
    string Fase, string? Leccion, double Peso, int Semilla,
    RequerimientosDto Requerimientos,
    List<GrupoPorcionDto> Porciones,
    AdecuacionDto Adecuacion,
    List<ComidaDto> Comidas,
    MacrosDto TotalDia,
    List<string> Advertencias,
    string GuiaPorciones = "");
