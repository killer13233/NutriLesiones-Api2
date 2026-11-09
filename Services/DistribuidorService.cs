using NutriLesiones.Api.Models;

namespace NutriLesiones.Api.Services;

/// <summary>Reparte las porciones diarias de cada subgrupo entre las 5 comidas (en pasos de 0.5).</summary>
public static class DistribuidorService
{
    /// <summary>
    /// Reparte un subgrupo. Para que cada comida muestre pocos alimentos distintos, las porciones se concentran en
    /// pocas comidas: como máximo tantas comidas como porciones enteras tenga el subgrupo (1,5 porciones -> 1 comida;
    /// 4,5 porciones -> hasta 4 comidas). QUÉ comidas se usan se sortea según los pesos y la semilla, así cada
    /// plan generado sale distinto (hoy la avena en el desayuno, mañana el arroz en el almuerzo). Los totales no cambian.
    /// </summary>
    public static double[] Distribuir(GrupoIntercambio g, CatalogoService catalogo, Random rng)
    {
        var total = g.Porciones;
        if (total <= 0) return new double[5];
        var pesos = PesosDisponibles(g.PesosComida,
            m => catalogo.Opciones(g.Grupo, g.Subgrupo, CatalogoService.ComidaIds[m]).Count > 0);
        return Repartir(total, Concentrar(pesos, total, rng));
    }

    /// <summary>
    /// Reparte las dos carnes (bajas y medias en grasa) juntas, de modo que cada comida lleve UN solo subgrupo de carne
    /// (así se elige una sola fuente proteica y no salen, por ejemplo, trucha + salmón en el mismo almuerzo).
    /// Los totales de cada subgrupo se conservan. Si no existe una combinación exacta, se divide una sola comida
    /// (de preferencia almuerzo o cena); si ni así se puede, se reparten por separado.
    /// </summary>
    public static (double[] bajas, double[] medias) DistribuirCarnes(GrupoIntercambio bajas, GrupoIntercambio medias, CatalogoService catalogo, Random rng)
    {
        var rb = new double[5];
        var rm = new double[5];
        if (bajas.Porciones <= 0 && medias.Porciones <= 0) return (rb, rm);
        if (medias.Porciones <= 0) return (Distribuir(bajas, catalogo, rng), rm);
        if (bajas.Porciones <= 0) return (rb, Distribuir(medias, catalogo, rng));

        bool HayB(int m) => catalogo.Opciones(bajas.Grupo, bajas.Subgrupo, CatalogoService.ComidaIds[m]).Count > 0;
        bool HayM(int m) => catalogo.Opciones(medias.Grupo, medias.Subgrupo, CatalogoService.ComidaIds[m]).Count > 0;

        var totalCarnes = bajas.Porciones + medias.Porciones;
        var pesos = PesosDisponibles(bajas.PesosComida, m => HayB(m) || HayM(m));
        var unidadesBajas = (int)Math.Round(bajas.Porciones / 0.5);

        // Se sortea en qué comidas va la carne y se buscan los grupos de comidas cuyas porciones se acerquen más a las de
        // "bajas en grasa" (las demás llevan "medias en grasa"). Se reintenta el sorteo hasta lograr una combinación exacta.
        var junto = new double[5];                 // porciones de carne por comida
        var comidas = new List<int>();
        var candidatas = new List<int>();
        var mejorDif = int.MaxValue;
        for (var intento = 0; intento < 15 && mejorDif != 0; intento++)
        {
            var j = Repartir(totalCarnes, Concentrar(pesos, totalCarnes, rng));
            var cs = Enumerable.Range(0, 5).Where(m => j[m] > 0).ToList();
            var cand = new List<int>();
            var difIntento = int.MaxValue;
            for (var mask = 0; mask < (1 << cs.Count); mask++)
            {
                var suma = 0;
                var valido = true;
                for (var i = 0; i < cs.Count; i++)
                {
                    var m = cs[i];
                    if ((mask & (1 << i)) != 0)
                    {
                        suma += (int)Math.Round(j[m] / 0.5);
                        if (!HayB(m)) valido = false;
                    }
                    else if (!HayM(m)) valido = false;
                }
                if (!valido) continue;
                var dif = Math.Abs(unidadesBajas - suma);
                if (dif < difIntento) { difIntento = dif; cand.Clear(); cand.Add(mask); }
                else if (dif == difIntento) cand.Add(mask);
            }
            if (cand.Count > 0 && difIntento < mejorDif)
            {
                junto = j; comidas = cs; candidatas = cand; mejorDif = difIntento;
            }
        }
        if (candidatas.Count == 0) return (Distribuir(bajas, catalogo, rng), Distribuir(medias, catalogo, rng));
        var mejorMask = candidatas[rng.Next(candidatas.Count)];

        var sumaMejor = 0;
        for (var i = 0; i < comidas.Count; i++)
        {
            var m = comidas[i];
            if ((mejorMask & (1 << i)) != 0) { rb[m] = junto[m]; sumaMejor += (int)Math.Round(junto[m] / 0.5); }
            else rm[m] = junto[m];
        }

        // Si no hay combinación exacta, se divide UNA sola comida para que los totales de cada subgrupo se conserven.
        var d = unidadesBajas - sumaMejor; // >0: faltan porciones "bajas"; <0: sobran
        if (d != 0)
        {
            var corregido = false;
            foreach (var m in comidas.OrderBy(x => x == 2 || x == 4 ? 0 : 1)) // mejor dividir almuerzo o cena que el desayuno
            {
                var u = (int)Math.Round(junto[m] / 0.5);
                if (d > 0 && rm[m] > 0 && u > d && HayB(m)) { rb[m] = d * 0.5; rm[m] = (u - d) * 0.5; corregido = true; }
                else if (d < 0 && rb[m] > 0 && u > -d && HayM(m)) { rm[m] = -d * 0.5; rb[m] = (u + d) * 0.5; corregido = true; }
                if (corregido) break;
            }
            if (!corregido) return (Distribuir(bajas, catalogo, rng), Distribuir(medias, catalogo, rng));
        }
        return (rb, rm);
    }

    /// <summary>Deja en 0 el peso de las comidas sin alimentos de ese subgrupo; si no queda ninguna, reparte parejo entre las que sí tienen.</summary>
    static double[] PesosDisponibles(double[] deseados, Func<int, bool> hayOpciones)
    {
        var pesos = new double[5];
        for (var m = 0; m < 5; m++) pesos[m] = hayOpciones(m) ? deseados[m] : 0;
        if (pesos.Sum() <= 0)
        {
            for (var m = 0; m < 5; m++) pesos[m] = hayOpciones(m) ? 1 : 0;
            if (pesos.Sum() <= 0) pesos[2] = 1; // sin opciones en ninguna comida: va al almuerzo
        }
        return pesos;
    }

    /// <summary>
    /// Sortea (según los pesos) qué comidas se usan, tantas como porciones enteras haya (mínimo 1).
    /// Una comida con más peso tiene más probabilidad, pero ninguna con peso > 0 queda descartada.
    /// </summary>
    static double[] Concentrar(double[] pesos, double total, Random rng)
    {
        var maxComidas = Math.Max(1, (int)Math.Floor(total + 1e-9));
        var disponibles = Enumerable.Range(0, 5).Where(i => pesos[i] > 0).ToList();
        var elegidas = new HashSet<int>();
        while (elegidas.Count < maxComidas && disponibles.Count > 0)
        {
            var suma = disponibles.Sum(i => pesos[i]);
            var r = rng.NextDouble() * suma;
            var acumulado = 0.0;
            var elegida = disponibles[disponibles.Count - 1];
            foreach (var i in disponibles)
            {
                acumulado += pesos[i];
                if (r < acumulado) { elegida = i; break; }
            }
            elegidas.Add(elegida);
            disponibles.Remove(elegida);
        }
        return pesos.Select((p, i) => elegidas.Contains(i) ? p : 0.0).ToArray();
    }

    /// <summary>Reparte "total" porciones según los pesos, en pasos de 0.5 (método del mayor resto).</summary>
    static double[] Repartir(double total, double[] pesos)
    {
        var resultado = new double[5];
        var unidades = (int)Math.Round(total / 0.5);   // media porción = 1 unidad
        var suma = pesos.Sum();
        var ideal = pesos.Select(p => unidades * p / suma).ToArray();
        var piso = ideal.Select(Math.Floor).ToArray();
        var faltan = unidades - (int)piso.Sum();

        var orden = Enumerable.Range(0, 5)
            .OrderByDescending(i => ideal[i] - piso[i]).ThenByDescending(i => pesos[i]).ToArray();
        for (var k = 0; k < faltan; k++) piso[orden[k % 5]]++;

        for (var m = 0; m < 5; m++) resultado[m] = piso[m] * 0.5;
        return resultado;
    }
}
