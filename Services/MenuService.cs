using NutriLesiones.Api.Models;

namespace NutriLesiones.Api.Services;

/// <summary>
/// Arma el menú del día: para cada comida elige alimentos reales del catálogo (con foto del libro)
/// que sean del subgrupo correcto, permitidos en esa comida y variados entre comidas y entre llamadas.
/// </summary>
public sealed class MenuService
{
    // Orden en que se muestran los subgrupos dentro de una comida
    const string GrupoCarnes = "Carnes, pescados y huevos";
    static readonly string[] OrdenGrupos = { "Lácteos", "Cereales", "Carnes, pescados y huevos", "Leguminosas", "Vegetales", "Frutas", "Grasas y frutos secos", GruposIntercambio.Azucares };

    /// <summary>Texto breve para explicar cómo leer las cantidades (se muestra al inicio de "Comidas del día").</summary>
    public const string GuiaPorciones =
        "Cada tiempo de comida muestra los alimentos y las cantidades que te corresponden, expresadas en intercambios: " +
        "1 intercambio equivale a la medida casera indicada y 2 intercambios, al doble. " +
        "Puedes cambiar un alimento por cualquiera de sus alternativas: son equivalentes y se usan con la misma cantidad de intercambios, " +
        "siempre dentro del mismo grupo. No cambies un grupo por otro (por ejemplo, un cereal por una fruta).";

    readonly CatalogoService _catalogo;
    public MenuService(CatalogoService catalogo) => _catalogo = catalogo;

    public PlanResponse CrearPlan(double peso, string fase, string? leccion, int semilla)
    {
        var req = RequerimientosService.Calcular(peso, fase);
        var grupos = PorcionesService.Calcular(req);
        var ad = PorcionesService.Adecuaciones(grupos, req);
        var enRango = ad.All(a => a >= 90 && a <= 110);

        var rng = new Random(semilla);
        // Las dos carnes se reparten juntas para que cada comida lleve una sola fuente proteica (ver DistribuidorService).
        var carnesBajas = grupos.First(g => g.Grupo == GrupoCarnes && g.Subgrupo == "Bajos en grasas");
        var carnesMedias = grupos.First(g => g.Grupo == GrupoCarnes && g.Subgrupo == "Medios en grasas");

        // El sorteo de comidas se repite (hasta 40 veces) si deja una comida muy cargada o muy liviana en kcal.
        var reparto = new Dictionary<GrupoIntercambio, double[]>();
        var mejorPuntaje = double.MaxValue;
        for (var intento = 0; intento < 40; intento++)
        {
            var (repBajas, repMedias) = DistribuidorService.DistribuirCarnes(carnesBajas, carnesMedias, _catalogo, rng);
            var candidato = grupos.ToDictionary(g => g, g =>
                g == carnesBajas ? repBajas : g == carnesMedias ? repMedias : DistribuidorService.Distribuir(g, _catalogo, rng));
            AsegurarBaseDesayuno(candidato);
            var desbalance = DesbalanceKcal(grupos, candidato);
            var cerealesJuntos = ComidasConDosCereales(grupos, candidato); // p. ej. avena + tortilla en la misma comida
            var puntaje = desbalance + 0.25 * cerealesJuntos;
            if (puntaje < mejorPuntaje) { mejorPuntaje = puntaje; reparto = candidato; }
            if (desbalance <= DesbalanceMaximo && cerealesJuntos == 0) break;
        }
        var usados = new Dictionary<string, int>(); // cuántas veces se usó cada alimento en el día
        var usadasFamilias = new Dictionary<string, int>(); // cuántas veces se usó cada fuente proteica (pollo, res, pescado, huevo...)
        var advertencias = new List<string>();
        var comidas = new List<ComidaDto>();

        for (var m = 0; m < 5; m++)
        {
            var items = new List<(Alimento a, double porc, GrupoIntercambio g)>();
            var gruposComida = grupos
                .Where(g => reparto[g][m] > 0)
                .OrderBy(g => Array.IndexOf(OrdenGrupos, g.Grupo)).ThenBy(g => g.Subgrupo);

            var listaGrupos = gruposComida.ToList();
            // una sola fuente proteica por comida (no pollo + res en el mismo plato)
            var familiaProteica = ElegirFamiliaProteica(
                listaGrupos.Where(g => g.Grupo == GrupoCarnes).ToList(), m, usadasFamilias, rng,
                listaGrupos.Any(g => g.Grupo == "Lácteos"),
                listaGrupos.Any(g => g.Grupo == "Grasas y frutos secos" && g.Subgrupo == "Altos en grasas"));

            foreach (var g in listaGrupos)
            {
                var porciones = reparto[g][m];
                var pool = _catalogo.Opciones(g.Grupo, g.Subgrupo, CatalogoService.ComidaIds[m]);
                if (g.Grupo == GrupoCarnes && familiaProteica != null)
                {
                    var deLaFamilia = pool.Where(a => Texto.Familia(a.Nombre) == familiaProteica).ToList();
                    if (deLaFamilia.Count > 0) pool = deLaFamilia;
                }
                if (pool.Count == 0) continue;
                // la mermelada solo va si la comida lleva pan; si no, miel, panela o azúcar
                if (g.Grupo == GruposIntercambio.Azucares && !items.Any(i => i.a.Grupo == "Cereales" && Texto.Norm(i.a.Nombre).StartsWith("pan")))
                {
                    var sinMermelada = pool.Where(a => !Texto.Norm(a.Nombre).Contains("mermelada")).ToList();
                    if (sinMermelada.Count > 0) pool = sinMermelada;
                }

                // avena, morocho y máchica se preparan con leche o yogur: solo van en comidas que llevan un lácteo
                if (g.Grupo == "Cereales" && !listaGrupos.Any(gr => gr.Grupo == "Lácteos"))
                {
                    var sinCalientes = pool.Where(al =>
                        !(Texto.Norm(al.Nombre).StartsWith("avena") || Texto.Norm(al.Nombre).StartsWith("morocho")
                          || Texto.Norm(al.Nombre).StartsWith("harina de cebada"))).ToList();
                    if (sinCalientes.Count > 0) pool = sinCalientes;
                }

                // Un solo alimento por subgrupo y comida: si corresponden 2 intercambios, se muestra 1 alimento con la cantidad total.
                // Única excepción: 2 o más intercambios de verduras pueden ser 2 verduras (ensalada).
                var unidades = (int)Math.Round(porciones / 0.5);
                var n = CuantosAlimentos(g, porciones, pool.Count);
                var elegidos = new List<Alimento>();
                for (var k = 0; k < n; k++)
                {
                    var siguiente = Elegir(pool, elegidos, items.Select(i => i.a), usados, rng);
                    if (siguiente == null) break;
                    elegidos.Add(siguiente);
                    usados[siguiente.Id] = usados.GetValueOrDefault(siguiente.Id) + 1;
                }

                if (elegidos.Count == 0) continue;
                // reparte las porciones entre los alimentos elegidos (los primeros reciben la unidad extra)
                var baseU = unidades / elegidos.Count;
                var extra = unidades % elegidos.Count;
                for (var k = 0; k < elegidos.Count; k++)
                    items.Add((elegidos[k], (baseU + (k < extra ? 1 : 0)) * 0.5, g));
            }

            // avisa si quedó en la misma comida algo que la regla de compatibilidad no recomienda
            var platoComida = items.Select(i => i.a).ToList();
            for (var x = 0; x < platoComida.Count; x++)
                for (var y = x + 1; y < platoComida.Count; y++)
                    if (ReglaChoca(platoComida[x], platoComida[y]))
                        advertencias.Add($"{CatalogoService.ComidaNombres[m]}: {platoComida[x].Nombre} con {platoComida[y].Nombre} (regla de compatibilidad provisional; no había otra opción en esa comida).");

            var dtoItems = items.Select(i => new AlimentoMenuDto(
                i.a.Id, i.a.Nombre, i.a.Grupo, i.a.Subgrupo, i.porc, i.a.MedidaCasera, i.a.PesoG, i.a.Foto, i.a.FotoReferencia, i.a.Nota,
                Alternativas(i.a, items.Select(x => x.a).ToList(), m))).ToList();
            var aporte = new MacrosDto(
                Math.Round(items.Sum(i => i.g.Kcal * i.porc), 1), Math.Round(items.Sum(i => i.g.Cho * i.porc), 1),
                Math.Round(items.Sum(i => i.g.Prot * i.porc), 1), Math.Round(items.Sum(i => i.g.Grasa * i.porc), 1));
            var indicacion = IndicacionService.Crear(CatalogoService.ComidaIds[m], items.Select(i => i.a));
            comidas.Add(new ComidaDto(CatalogoService.ComidaIds[m], CatalogoService.ComidaNombres[m],
                CatalogoService.ComidaHorarios[m], dtoItems, aporte, indicacion));
        }

        if (!enRango) advertencias.Add("No se logró dejar los 4 nutrientes entre 90 % y 110 % con las reglas actuales.");
        var sinFoto = comidas.SelectMany(c => c.Alimentos).Where(a => a.Foto == null).Select(a => a.Nombre).Distinct().ToList();
        if (sinFoto.Count > 0) advertencias.Add("Alimentos sin foto en el libro: " + string.Join(", ", sinFoto) + ".");
        var conReferencia = comidas.SelectMany(c => c.Alimentos).Where(a => a.FotoReferencia != null).Select(a => a.Nombre).Distinct().ToList();
        if (conReferencia.Count > 0) advertencias.Add("Foto de referencia (no es el mismo alimento): " + string.Join(", ", conReferencia) + ".");

        var total = new MacrosDto(
            Math.Round(comidas.Sum(c => c.Aporte.Kcal), 1), Math.Round(comidas.Sum(c => c.Aporte.Cho), 1),
            Math.Round(comidas.Sum(c => c.Aporte.Proteina), 1), Math.Round(comidas.Sum(c => c.Aporte.Grasa), 1));

        return new PlanResponse(
            fase, leccion, peso, semilla,
            new RequerimientosDto(Math.Round(req.Kcal, 1), Math.Round(req.ChoG, 1), Math.Round(req.ProtG, 1),
                Math.Round(req.GrasaG, 1), req.KcalPorKg, req.ProtPct, req.ChoPct, req.GrasaPct),
            grupos.Select(g => new GrupoPorcionDto(g.Grupo, g.Subgrupo, g.Porciones, reparto[g])).ToList(),
            new AdecuacionDto(Math.Round(ad[0], 1), Math.Round(ad[1], 1), Math.Round(ad[2], 1), Math.Round(ad[3], 1), enRango),
            comidas, total, advertencias, GuiaPorciones);
    }

    /// <summary>
    /// El desayuno siempre lleva una base (un lácteo o un cereal). Si el sorteo no la puso, se trae al desayuno el lácteo
    /// semidescremado (o, si no, el cereal medio bajo en grasa) completo; los totales del día no cambian.
    /// </summary>
    void AsegurarBaseDesayuno(Dictionary<GrupoIntercambio, double[]> reparto)
    {
        if (reparto.Any(kv => (kv.Key.Grupo == "Lácteos" || kv.Key.Grupo == "Cereales") && kv.Value[0] > 0)) return;
        foreach (var (grupo, subgrupo) in new[] { ("Lácteos", "Semidescremados"), ("Cereales", "Medios en CHO y bajos en grasas") })
        {
            var g = reparto.Keys.FirstOrDefault(x => x.Grupo == grupo && x.Subgrupo == subgrupo);
            if (g == null || g.Porciones <= 0) continue;
            if (_catalogo.Opciones(g.Grupo, g.Subgrupo, CatalogoService.ComidaIds[0]).Count == 0) continue;
            var r = reparto[g];
            r[0] = g.Porciones;
            for (var m = 1; m < 5; m++) r[m] = 0;
            return;
        }
    }

    // Reparto ideal de las kcal del día (Desayuno, Media mañana, Almuerzo, Media tarde, Cena) y desvío máximo aceptado por comida.
    static readonly double[] KcalIdealPorComida = { 0.20, 0.14, 0.31, 0.14, 0.21 };
    const double DesbalanceMaximo = 0.08;

    /// <summary>Mayor desvío (en fracción del día) entre lo que lleva una comida y lo ideal; 0 = reparto perfecto.</summary>
    static double DesbalanceKcal(List<GrupoIntercambio> grupos, Dictionary<GrupoIntercambio, double[]> reparto)
    {
        var kcal = new double[5];
        foreach (var g in grupos)
            for (var m = 0; m < 5; m++) kcal[m] += g.Kcal * reparto[g][m];
        var total = kcal.Sum();
        if (total <= 0) return 0;
        var peor = 0.0;
        for (var m = 0; m < 5; m++) peor = Math.Max(peor, Math.Abs(kcal[m] / total - KcalIdealPorComida[m]));
        return peor;
    }

    /// <summary>Cuenta las comidas que quedaron con dos subgrupos de cereales a la vez.</summary>
    static int ComidasConDosCereales(List<GrupoIntercambio> grupos, Dictionary<GrupoIntercambio, double[]> reparto)
    {
        var juntos = 0;
        for (var m = 0; m < 5; m++)
            if (grupos.Count(g => g.Grupo == "Cereales" && reparto[g][m] > 0) >= 2) juntos++;
        return juntos;
    }

    /// <summary>Cuántos alimentos distintos se muestran para un subgrupo en una comida (1, salvo 2 verduras cuando hay 2 o más intercambios).</summary>
    static int CuantosAlimentos(GrupoIntercambio g, double porciones, int disponibles)
    {
        var unidades = (int)Math.Round(porciones / 0.5);
        var deseados = g.Grupo == "Vegetales" && porciones >= 2 ? 2 : 1;
        return Math.Max(1, Math.Min(deseados, Math.Min(disponibles, unidades)));
    }

    /// <summary>
    /// Elige la fuente proteica de la comida (pollo, res, pescado, huevo...) entre las que tienen opciones
    /// en TODOS los subgrupos de carnes que tocan en esa comida; prefiere las menos usadas en el día.
    /// </summary>
    string? ElegirFamiliaProteica(List<GrupoIntercambio> gruposCarne, int comida, Dictionary<string, int> usadas, Random rng, bool hayLacteo, bool hayAceite)
    {
        if (gruposCarne.Count == 0) return null;
        HashSet<string>? comunes = null;
        foreach (var g in gruposCarne)
        {
            var familias = _catalogo.Opciones(g.Grupo, g.Subgrupo, CatalogoService.ComidaIds[comida])
                .Select(a => Texto.Familia(a.Nombre)).ToHashSet();
            if (familias.Count == 0) continue;
            if (comunes == null) comunes = familias;
            else { var cruce = comunes.Intersect(familias).ToHashSet(); if (cruce.Count > 0) comunes = cruce; }
        }
        if (comunes == null || comunes.Count == 0) return null;
        // si la comida lleva dos cortes (bajo y medio en grasa), se prefiere pollo o res antes que dos pescados o huevo entero + claras
        if (gruposCarne.Count > 1)
            foreach (var f in new[] { "pescado", "huevo" })
                if (comunes.Count > 1) comunes.Remove(f);
        // si la comida lleva leche o yogur, se prefiere una fuente que no sea pescado ni mariscos
        if (hayLacteo)
            foreach (var f in new[] { "pescado", "sardinas", "sardina", "camaron" })
                if (comunes.Count > 1) comunes.Remove(f);
        // si la comida lleva aceite o frutos secos como grasa, no se usan sardinas en aceite (doble aceite)
        if (hayAceite && comunes.Count > 1) comunes.Remove("sardinas");
        // el desayuno lleva huevo (entero o clara), no pescado
        if (comida == 0 && comunes.Count > 1) comunes.Remove("pescado");
        var elegida = comunes.OrderBy(f => usadas.GetValueOrDefault(f)).ThenBy(_ => rng.Next()).First();
        usadas[elegida] = usadas.GetValueOrDefault(elegida) + 1;
        return elegida;
    }

    /// <summary>
    /// Elige un alimento del pool: prefiere los menos usados en el día, al azar entre los empatados,
    /// y evita (si hay otra opción) repetir la misma "familia" (arroz+arroz, pescado+pescado) o juntar
    /// alimentos incompatibles en la misma comida.
    /// </summary>
    Alimento? Elegir(List<Alimento> pool, List<Alimento> yaDelGrupo, IEnumerable<Alimento> yaEnComida,
        Dictionary<string, int> usados, Random rng)
    {
        var enComida = yaEnComida.Concat(yaDelGrupo).ToList();
        var disponibles = pool.Where(a => !yaDelGrupo.Contains(a))
            .OrderBy(a => a.Foto == null ? 2 : a.FotoReferencia != null ? 1 : 0) // primero foto propia, luego de referencia, al final sin foto
            .ThenBy(a => usados.GetValueOrDefault(a.Id))          // luego los menos usados en el día
            .ThenBy(_ => rng.Next()).ToList();                    // y al azar entre los empatados
        if (disponibles.Count == 0) return null;
        return disponibles.FirstOrDefault(a => !Conflicto(a, enComida)) ?? disponibles[0];
    }

    /// <summary>
    /// Intercambios de un alimento: otros del mismo subgrupo, permitidos en esa comida, que no estén ya en el plato
    /// y que no choquen con lo demás de la comida. Primero los que tienen foto y los de la misma fuente.
    /// </summary>
    List<AlternativaDto> Alternativas(Alimento elegido, List<Alimento> plato, int comida)
    {
        var resto = plato.Where(x => x.Id != elegido.Id).ToList();
        var elegidoYaChoca = Conflicto(elegido, resto); // si el plato ya tiene el choque, no se le quita el intercambio por eso
        return _catalogo.Opciones(elegido.Grupo, elegido.Subgrupo, CatalogoService.ComidaIds[comida])
            .Where(a => a.Id != elegido.Id && !plato.Any(x => x.Id == a.Id) && (elegidoYaChoca || !Conflicto(a, resto)))
            .OrderBy(a => a.Foto == null ? 2 : a.FotoReferencia != null ? 1 : 0)
            .ThenBy(a => Texto.Familia(a.Nombre) == Texto.Familia(elegido.Nombre) ? 0 : 1)
            .ThenBy(a => a.Nombre)
            .Take(8)
            .Select(a => new AlternativaDto(a.Id, a.Nombre, a.MedidaCasera, a.PesoG, a.Foto, a.FotoReferencia))
            .ToList();
    }

    bool ReglaChoca(Alimento a, Alimento b)
    {
        var na = Texto.Norm(a.Nombre); var nb = Texto.Norm(b.Nombre);
        foreach (var r in _catalogo.Reglas)
            if ((na.Contains(r.A) && nb.Contains(r.B)) || (na.Contains(r.B) && nb.Contains(r.A)))
                return true;
        return false;
    }

    bool Conflicto(Alimento a, List<Alimento> enComida)
    {
        var fam = Texto.Familia(a.Nombre);
        foreach (var otro in enComida)
        {
            var ambosCarnes = a.Grupo == GrupoCarnes && otro.Grupo == GrupoCarnes; // en carnes la misma fuente SÍ se permite
            if (!ambosCarnes && Texto.Familia(otro.Nombre) == fam) return true;
            if (ReglaChoca(a, otro)) return true;
        }
        return false;
    }
}
