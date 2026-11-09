using System.Text;

namespace NutriLesiones.Api.Services;

public static class Texto
{
    static readonly Dictionary<char, char> Mapa = new()
    {
        ['á'] = 'a', ['é'] = 'e', ['í'] = 'i', ['ó'] = 'o', ['ú'] = 'u', ['ü'] = 'u', ['ñ'] = 'n'
    };

    /// <summary>Minúsculas y sin tildes (para comparar textos).</summary>
    public static string Norm(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant()) sb.Append(Mapa.TryGetValue(ch, out var m) ? m : ch);
        return sb.ToString();
    }

    /// <summary>Primera palabra del nombre ("Arroz blanco..." -> "arroz"); sirve para no repetir el mismo alimento en una comida.</summary>
    public static string Familia(string nombre)
    {
        var n = Norm(nombre);
        var i = n.IndexOfAny(new[] { ',', ' ' });
        var primera = i < 0 ? n : n[..i];
        return primera is "sambo" or "zapallo" or "zuquini" ? "calabaza" : primera; // evita 2 calabazas en la misma comida
    }

    /// <summary>Nombre corto para frases ("Pescado, trucha, cruda" -> "trucha"; "Avena en hojuelas..., Mosh" -> "avena").</summary>
    public static string NombreCorto(string nombre)
    {
        var partes = nombre.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0) return nombre.Trim().ToLowerInvariant();
        var p0 = Norm(partes[0]);
        var p1 = partes.Length > 1 ? Norm(partes[1]) : "";

        if (p0 == "pescado" && partes.Length > 1) return Recortar(partes[1]);
        if (p0 == "pollo")
            return p1.StartsWith("pechuga") ? "pechuga de pollo" : p1.StartsWith("muslo") ? "muslo de pollo"
                 : p1.StartsWith("pierna") ? "pierna de pollo" : "pollo";
        if (p0 == "res")
        {
            if (p1.StartsWith("bistec")) return "bistec de res";
            if (p1.StartsWith("lomo")) return "lomo fino de res";
            if (p1.StartsWith("molida")) return "carne molida";
            if (p1.StartsWith("higado")) return "hígado de res";
            return "carne de res";
        }
        if (p0 == "huevo de gallina") return p1.StartsWith("clara") ? "claras de huevo" : "huevo";
        if (p0.StartsWith("harina de cebada")) return "máchica";
        if (p0.StartsWith("avena")) return "avena";
        if (p0.StartsWith("leche")) return "leche";
        if (p0.StartsWith("pan de trigo")) return "pan";
        if (p0.StartsWith("pan integral")) return "pan integral";
        if (p0.StartsWith("pan blanco")) return "pan blanco";
        if (p0.StartsWith("maiz tipo mote")) return "mote";
        if (p0.StartsWith("tomate rinon")) return "tomate";
        return Recortar(partes[0]);
    }

    static string Recortar(string s)
    {
        var r = s.Trim().ToLowerInvariant();
        foreach (var corte in new[] { " o ", " u ", " en agua", " nacional", " común", " criollo", " con cáscara" })
        {
            var i = r.IndexOf(corte, StringComparison.Ordinal);
            if (i > 0) r = r[..i];
        }
        return r;
    }

    /// <summary>Une una lista en español: "a", "a y b", "a, b y c" ("e" antes de palabras que empiezan con i/hi).</summary>
    public static string Unir(IEnumerable<string> partes)
    {
        var l = partes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (l.Count == 0) return "";
        if (l.Count == 1) return l[0];
        var ultimo = l[^1];
        var n = Norm(ultimo);
        var conj = n.StartsWith("i") || n.StartsWith("hi") ? "e" : "y";
        return string.Join(", ", l.Take(l.Count - 1)) + " " + conj + " " + ultimo;
    }
}
