# NutriLesiones.Api (.NET 8)

Backend del sistema: calcula los requerimientos (kcal, CHO, proteína, grasa) según peso y fase de la lesión,
reparte las porciones por grupo de alimentos (90–110 % de adecuación), las divide en las 5 comidas del día
y arma el menú con alimentos reales y sus fotos del libro de la USFQ.

## Ejecutar en local (Windows, PowerShell)
```powershell
# 1) Instalar el SDK de .NET 8 (solo la primera vez)
winget install Microsoft.DotNet.SDK.8

# 2) Descomprimir y entrar a la carpeta
Expand-Archive NutriLesiones.Api.zip -DestinationPath .
cd NutriLesiones.Api

# 3) Levantar el backend
dotnet run --urls http://localhost:5099
```
- Página de prueba: http://localhost:5099
- Probar la API: `Invoke-RestMethod -Method Post -Uri http://localhost:5099/api/plan -ContentType "application/json" -Body '{"peso":65,"fase":"inflamatoria"}' | ConvertTo-Json -Depth 10`
- Revisar que responde: `Invoke-RestMethod http://localhost:5099/api/salud`

Si solo tienes .NET 10 instalado, cambia la versión antes de correr (no probado con 10, pero el código no usa nada específico de 8):
```powershell
(Get-Content NutriLesiones.Api.csproj) -replace 'net8.0','net10.0' | Set-Content NutriLesiones.Api.csproj
(Get-Content Dockerfile) -replace ':8.0',':10.0' | Set-Content Dockerfile
```

Con Docker (opcional):
```powershell
docker build -t nutrilesiones-api .
docker run -p 8080:8080 nutrilesiones-api
```

## Endpoints
| Método | Ruta | Para qué |
|---|---|---|
| GET | `/` | Página de prueba (formulario + menú con fotos) |
| GET | `/api/salud` | Revisar que el servidor responde |
| GET | `/api/catalogos` | Fases, lecciones y comidas (para los combos de Angular) |
| GET | `/api/alimentos?grupo=cereales&comida=Desayuno` | Catálogo de alimentos (filtros opcionales) |
| POST | `/api/plan` | **Plan completo del día** |
| GET | `/imagenes/<archivo>.jpg` | Fotos del libro |

### POST /api/plan
```json
{ "peso": 65, "fase": "inflamatoria", "leccion": "desgarros", "semilla": 7 }
```
- `fase`: `inflamatoria`, `proliferativa` o `remodelacion`.
- `semilla` es opcional: **sin semilla cada llamada devuelve un menú distinto** (botón "Generar otro menú"); con la misma semilla devuelve el mismo menú.
- Respuesta de ejemplo completa: `docs/ejemplo-respuesta.json`.
- Cada alimento del menú trae `alternativas` (los **intercambios**): otros alimentos del mismo subgrupo, permitidos en esa comida, que se pueden usar en su lugar con las mismas porciones.
- `advertencias` avisa si algo no se pudo cumplir (adecuación fuera de 90–110 %, alimentos sin foto, choques con las reglas de compatibilidad).
- La foto de cada alimento viene en `foto` (ruta relativa). En Angular: `apiUrl + '/' + alimento.foto`. Si `foto` es `null`, ese alimento no tiene foto en el libro.

## Qué archivo tocar para cada cambio
| Cambio | Archivo |
|---|---|
| Agregar/quitar alimentos, medidas, comidas permitidas o fotos | `Data/alimentos.json` y `wwwroot/imagenes/` |
| Reglas de compatibilidad (ej. leche con huevo) | `Data/reglas.json` |
| Valores por porción de cada subgrupo y reparto por comida | `Models/Modelos.cs` |
| Cómo se calculan las porciones (método de Belén) y las porciones fijas | `Services/PorcionesService.cs` |
| kcal/kg y % de macros por fase | `Services/RequerimientosService.cs` |
| Horarios de las comidas | `Services/CatalogoService.cs` |

## Publicar gratis (Render, con Docker)
1. Sube esta carpeta a un repositorio de GitHub.
2. En Render: **New → Web Service →** conecta el repo → Runtime **Docker** → plan **Free**.
3. Health Check Path: `/api/salud`.
4. (Opcional) Variable de entorno `CORS_ORIGINS` = `https://tu-front.vercel.app` para que solo tu Angular pueda llamar a la API. Sin ella acepta cualquier origen.
5. Render te da un link `https://nombre.onrender.com`. El plan gratis se "duerme" tras un rato sin uso y la primera llamada tarda unos segundos en despertar.

## Método de cálculo de porciones (Belén)
`Services/PorcionesService.cs` aplica el método de Belén, en pasos de 0,5 porciones:
1. **Cereales** (altos en CHO, 30 g) = 1/3 del CHO requerido ÷ 30.
2. **Vegetales y lácteos** = límite inferior del rango DASH para esa energía (interpolado entre 2000 y 2600 kcal); **leguminosas** = 1,5.
3. **Carnes** = (proteína requerida − la ya aportada) ÷ 11, hacia abajo; la mitad en "bajas en grasa" (hacia arriba) y el resto en "medias".
4. **Grasas** (altas, 12 g) = (grasa requerida − la ya aportada) ÷ 12.
5. **Azúcares** = 1 intercambio (10 g CHO, 40 kcal).
6. **Frutas** (medias en CHO, 20 g) = (CHO requerido − el ya aportado) ÷ 20. Si queda fuera del rango DASH de frutas, el cereal sube o baja 0,5 y se repite desde el paso 2.

**Porciones fijas que siempre van** (son parámetros, no vienen del libro; se cambian en `Fijos` de `PorcionesService.cs`): aguacate (grasas medias) 1 porción = 1/4 de unidad, cereales medios en CHO y bajos en grasas 1, cereales medios en CHO y medios en grasas 0,5, frutas bajas en CHO 1. Los lácteos se reparten mitad semidescremados y mitad descremados. Todo cuenta dentro de los totales antes de calcular carnes, grasas y frutas.

- Los valores por porción de los cereales medios salen del libro (p. 10). En el Excel del nutricionista esos dos subgrupos tienen los valores cruzados (20/2/6/145 corresponde a "medios en grasas" y 20/3/1/105 a "bajos en grasas").
- Siguen fuera por ahora (pendiente de la tutora): quesos, lácteos enteros, lácteos descremados altos en CHO y grasas altas en grasas y bajas en CHO.
- En qué comidas va cada subgrupo se ajusta en `PesosComida` de `Models/Modelos.cs` (solo se asigna a comidas donde el documento del nutricionista permite alimentos de ese subgrupo).
- Azúcares: "Mer" del documento se interpretó como media tarde (la mermelada también en desayuno).

## Fotos
Casi todas las fotos salen del libro de la USFQ. Para poner o cambiar una foto: guarda el archivo con el nombre indicado en `wwwroot/imagenes/` y listo (el programa la detecta sola, reemplaza la de referencia y no hay que tocar el JSON).

**Con foto de referencia (reemplazar cuando tengan la real):**

| Alimento | Archivo de la foto real | Foto de referencia usada |
|---|---|---|
| Choclo en grano | `choclo-en-grano.jpg` | Maíz, dulce, enlatado. |
| Sardinas en aceite, enlatadas | `sardinas-en-aceite-enlatadas.jpg` | Sardina enlatada, en salsa de tomate. |
| Res, bistec, crudo | `res-bistec-crudo.jpg` | Res, bife de chorizo, crudo. |
| Res, carne magra, cruda | `res-carne-magra-cruda.jpg` | Res, carne semimagra, cruda. |
| Pescado, atún en agua o aceite, enlatado | `pescado-atun-en-agua-o-aceite-enlatado.jpg` | Pescado, albacora (atún blanco), cocido. |

**Sin ninguna foto (hay que tomarlas):**

| Alimento | Archivo |
|---|---|
| Harina de cebada, máchica | `harina-de-cebada-machica.jpg` |
| Pescado, bagre | `pescado-bagre.jpg` |

## Confirmado por el nutricionista
- Leguminosas: 1 a 2 porciones **por día** (el código ya lo hace: 1 de base, máximo 2).
- Lesión, edad, altura, sexo y posición **no** afectan el cálculo; solo cuentan peso y fase (por eso `leccion` en `/api/plan` es opcional y solo se devuelve tal cual).
- Leche/yogur con huevo sí se permite.

## Pendientes
- Sardinas en aceite, atún enlatado y bistec: el nutricionista las va a fotografiar (guardar como `sardinas-en-aceite-enlatadas.jpg`, `pescado-atun-en-agua-o-aceite-enlatado.jpg` y `res-bistec-crudo.jpg`).
- Fase proliferativa: hoy la proteína da 1,5 g/kg y la Tabla 2 pide mínimo 1,6 g/kg; confirmar el valor en `Services/RequerimientosService.cs`.
- Más de 2600 kcal: el rango DASH se mantiene fijo desde 2600 kcal (decisión de la tutora).
- Pechuga de pollo: peso 40 g sin confirmar si es crudo o cocido (el catálogo lo marca con una nota).
- Las porciones fijas (aguacate, cereales medios, frutas bajas) son parámetros por confirmar con el nutricionista.

## Cambios de noviembre (sugerencias de Belén)
- `comidas[].indicacion`: texto breve con cómo combinar los alimentos de cada comida en un plato (`IndicacionService.cs`).
- `guiaPorciones`: texto para "Cómo funciona" / inicio de "Comidas del día" (constante `MenuService.GuiaPorciones`).
- Un solo alimento por subgrupo y comida (2 intercambios = 1 alimento con cantidad 2). Excepción: 2 o más intercambios de verduras pueden mostrarse como 2 verduras.
- Cada comida lleva una sola fuente de carne (ya no sale trucha + salmón). Los totales por subgrupo no cambian.
- Las porciones de cada subgrupo se concentran en pocas comidas (máximo tantas como porciones enteras). Cuáles comidas se sortea según los pesos y la `semilla`, así cada plan generado sale distinto.
- `Data/reglas.json`: reglas blandas (huevo entero con clara; pescado y mariscos con leche o yogur), pendientes de confirmar con Belén.
- Pesos por comida ajustados en `Models/Modelos.cs`: verduras solo almuerzo y cena; cereales medios bajos en grasa 60 % desayuno / 40 % media tarde.
- `POST /api/plan` acepta `edad` opcional; si viene, debe estar entre 12 y 19 (no cambia el cálculo).
- `docs/ejemplo-respuesta.json` quedó del formato anterior: regenerarlo con una llamada a /api/plan.
