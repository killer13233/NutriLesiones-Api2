using NutriLesiones.Api.Models;

namespace NutriLesiones.Api.Services;

/// <summary>
/// Redacta una indicación breve de cómo combinar los alimentos de una comida en un plato
/// ("Puedes combinar espinaca, tomate y aceite de oliva en una ensalada, acompañada de pescado y plátano").
/// No es una receta: solo orienta cómo armar el plato con lo que ya eligió el menú.
/// </summary>
public static class IndicacionService
{
    static readonly string[] CerealesCalientes = { "avena", "quinua", "máchica", "morocho" };

    public static string? Crear(string comidaId, IEnumerable<Alimento> alimentos)
    {
        var lista = alimentos.ToList();
        List<string> De(string grupo) =>
            lista.Where(a => a.Grupo == grupo).Select(a => Texto.NombreCorto(a.Nombre)).Distinct().ToList();

        var lacteos = De("Lácteos");
        var cereales = De("Cereales");
        var frutas = De("Frutas");
        var proteinas = De("Carnes, pescados y huevos");
        var legum = De("Leguminosas");
        var vegs = De("Vegetales");
        var azucares = De(GruposIntercambio.Azucares);
        var grasas = De("Grasas y frutos secos");

        return comidaId is "Almuerzo" or "Cena"
            ? ComidaFuerte(lacteos, cereales, frutas, proteinas, legum, vegs, grasas)
            : ComidaLigera(lacteos, cereales, frutas, proteinas, legum, vegs, grasas, azucares);
    }

    static string? ComidaFuerte(List<string> lacteos, List<string> cereales, List<string> frutas, List<string> proteinas,
        List<string> legum, List<string> vegs, List<string> grasas)
    {
        var frases = new List<string>();
        var aderezo = grasas.Count > 0 ? " con " + Texto.Unir(grasas) : "";
        var acomp = proteinas.Concat(cereales).Concat(legum).ToList();

        if (vegs.Count > 0)
        {
            // las calabazas (sambo, zapallo, zuquini) no van en ensalada: se sirven cocidas
            var calabaza = vegs.Any(v => v is "sambo" or "zapallo" or "zuquini");
            var f = calabaza
                ? $"Puedes preparar {Texto.Unir(vegs)} al vapor o salteados{aderezo}"
                : $"Puedes combinar {Texto.Unir(vegs)} en una ensalada{aderezo}";
            if (acomp.Count > 0) f += calabaza ? $", junto con {Texto.Unir(acomp)}" : $", acompañada de {Texto.Unir(acomp)}";
            frases.Add(f + ".");
        }
        else if (acomp.Count > 0)
            frases.Add($"Puedes combinar {Texto.Unir(acomp)}{aderezo}.");
        else if (grasas.Count > 0)
            frases.Add($"Puedes usar {Texto.Unir(grasas)} para aderezar tu comida.");

        if (frutas.Count > 0) frases.Add(frases.Count == 0 ? $"Puedes comer {Texto.Unir(frutas)}." : $"De postre, {Texto.Unir(frutas)}.");
        if (lacteos.Count > 0) frases.Add($"Para tomar, {Texto.Unir(lacteos)}.");
        return frases.Count == 0 ? null : string.Join(" ", frases);
    }

    static string? ComidaLigera(List<string> lacteos, List<string> cereales, List<string> frutas, List<string> proteinas,
        List<string> legum, List<string> vegs, List<string> grasas, List<string> azucares)
    {
        var frases = new List<string>();
        string baseFrase;

        if (cereales.Count > 0 && lacteos.Count > 0)
        {
            var calientes = cereales.Where(c => CerealesCalientes.Contains(c)).ToList();
            if (calientes.Count > 0)
            {
                // avena, quinua, máchica o morocho se preparan con el lácteo; otros cereales van como acompañamiento
                baseFrase = $"Puedes preparar {Texto.Unir(calientes)} con {Texto.Unir(lacteos)}";
                cereales = cereales.Except(calientes).ToList();
            }
            else
            {
                baseFrase = $"Puedes tomar {Texto.Unir(lacteos)} con {Texto.Unir(cereales)}";
                cereales = new List<string>();
            }
            lacteos = new List<string>();
        }
        else if (cereales.Count > 0)
        {
            baseFrase = $"Puedes comer {Texto.Unir(cereales)}";
            cereales = new List<string>();
        }
        else if (lacteos.Count > 0)
        {
            baseFrase = $"Puedes tomar {Texto.Unir(lacteos)}";
            lacteos = new List<string>();
        }
        else if (frutas.Count > 0)
        {
            baseFrase = $"Puedes comer {Texto.Unir(frutas)}";
            frutas = new List<string>();
        }
        else
        {
            var principal = proteinas.Count > 0 ? proteinas : legum.Count > 0 ? legum : vegs.Count > 0 ? vegs : grasas;
            if (principal.Count == 0) return null;
            baseFrase = $"Puedes comer {Texto.Unir(principal)}";
            principal.Clear();
        }

        // la fruta se suma a la base: "Puedes preparar avena con leche, añadiendo banano."
        if (frutas.Count > 0) baseFrase += $", añadiendo {Texto.Unir(frutas)}";
        frases.Add(baseFrase + ".");

        var acomp = cereales.Concat(proteinas).Concat(legum).Concat(vegs).ToList();
        if (acomp.Count > 0) frases.Add($"Acompaña con {Texto.Unir(acomp)}.");
        if (grasas.Count > 0) frases.Add($"Completa con {Texto.Unir(grasas)}.");
        if (azucares.Count > 0)
            frases.Add(azucares.Contains("mermelada") ? "Si deseas, unta mermelada en el pan." : $"Si deseas, endulza con {Texto.Unir(azucares)}.");
        return string.Join(" ", frases);
    }
}
