# LexPC Images

API en **.NET 10** con arquitectura **hexagonal + DDD** en monolito modular. Composition root único: `src/LexPCImages.API/Program.cs`. v0.1 expone el módulo `Optimizer` (adaptación de imágenes a los slots del catálogo).

## ¿Qué hace?

Pipeline de optimización de imágenes para el catálogo público. El slot elegido decide el tratamiento:

```
optimizar-imagen-pc-home              →  PAQUETE: una imagen entra, dos trabajos salen
                                          ├─ 320×315  escalar proporcional sobre transparente
                                          └─ 992×715  escalar proporcional sobre transparente
optimizar-imagen-pc-seccion-principal →  escalar proporcional + relleno del color de fondo → 1000×720
optimizar-imagen-pc-ultima-seccion    →  recortar centrado, o rellenar si el recorte mutila → 619×720
```

Todas las salidas son WebP con pérdida a calidad 75, configurable. El canal alfa se codifica
aparte y sin pérdida, así que la máscara de los recortes sale exacta. Las salidas de `pc-home`
esperan imágenes que ya vienen sin fondo: lo que sobra al encajar la proporción queda
transparente, nunca se rellena con un color.

## Arquitectura

Monolito modular con DDD + arquitectura hexagonal. Cada capa es un proyecto C# separado, así que el compilador fuerza la dirección de las dependencias y los tests de `NetArchTest` fijan el resto de reglas.

```
API  →  Module.Presentation  →  Module.Application  →  Module.Domain
              ↓                        ↑                     ↓
        Shared.Web            Module.Infrastructure        Shared
```

Decisiones que conviene conocer antes de tocar el código:

- **Una estrategia por `SlotMode`.** `ProcessImageHandler` solo decodifica, delega en el `IImageProcessingPipeline` que corresponde y codifica. Añadir un modo no obliga a modificarlo.
- **`Result<T>` en vez de excepciones** para los errores esperables. El middleware global responde siempre `500` genérico: si algo llega hasta allí es un fallo no previsto.
- **Una sola traducción `Error` → HTTP**, en `LexPCImages.Shared.Web`, usada tanto por los controladores como por el host.
- **El dominio no lee el reloj**: la marca de tiempo entra como parámetro y en producción viene del `TimeProvider` registrado.
- **El dominio no referencia nada**, ni siquiera `Shared`: es la capa más interna y solo usa el BCL.
- **Configuración con el patrón Options**, validada al arrancar: un valor mal escrito detiene el proceso en lugar de degradarse en silencio.

Más detalle: [`AGENTS.md`](./AGENTS.md).

## Requisitos

- **.NET 10 SDK** (`10.0.203+`). No hace falta nada más: la API ya no descarga ni carga ningún modelo.

## Setup local

```bash
dotnet restore LexPCImages.slnx
dotnet build LexPCImages.slnx
dotnet run --project src/LexPCImages.API
# API: http://localhost:5232
# OpenAPI: /openapi/v1.json
# Scalar UI: /scalar
```

Frontend hermano en [`lexpc-images-pwa/`](../lexpc-images-pwa) (Angular 21 PWA).

## Uso

```bash
curl -X POST http://localhost:5232/api/optimizer/jobs \
  -F "slotId=optimizar-imagen-pc-home" \
  -F "file=@pc.png;type=image/png"
# → 202 { "jobs": [
#          { "jobId": "...", "slotId": "optimizar-imagen-pc-home-320x315", "width": 320, "height": 315, "status": "Queued" },
#          { "jobId": "...", "slotId": "optimizar-imagen-pc-home-992x715", "width": 992, "height": 715, "status": "Queued" }
#        ] }

curl http://localhost:5232/api/optimizer/jobs/{jobId}
# → { "status": "Processing", "stage": "Resizing", "progress": 15, ... }

curl -O -J http://localhost:5232/api/optimizer/jobs/{jobId}/download
# → optimizar-imagen-pc-home-320x315-{jobId}.webp
```

**La respuesta del encolado es siempre una lista**, también cuando el slot produce una sola
salida: así el cliente no distingue entre un slot suelto y un paquete.

Los errores se devuelven como `application/problem+json` con un campo `code` estable (`optimizer.slot_not_found`, `optimizer.image_content_mismatch`, …).

## Configuración

```jsonc
{
  "Optimizer": {
    "QueueCapacity": 100,
    "JobRetention": "00:30:00",               // cuánto se conserva un trabajo terminado
    "MaxTrackedJobs": 500,
    "WebpQuality": 75,                        // calidad del WebP con pérdida, 1-100
    "WebpLossless": false                     // exacto pixel a pixel, ~8x mas peso
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:4300" ]
  }
}
```

Los orígenes CORS ya no están en el código: para otro entorno basta con `appsettings.{Environment}.json` o variables de entorno (`Cors__AllowedOrigins__0`).

## Tests

```bash
dotnet test LexPCImages.slnx
```

| Suite | Tests | Cubre |
|---|---|---|
| `UnitTests` | 150 | Dominio, casos de uso, pipelines, registro de slots, repositorio, validación, ImageSharp, codificación WebP, `Result<T>` |
| `ArchitectureTests` | 18 | Capas, contratos, independencia web, reloj del dominio, mapeo de errores |
| `IntegrationTests` | 16 | Host real, sin dobles: enqueue → polling → download, paquete y slots sueltos, `problem+json` |

Cobertura objetivo ≥ 90% (F5+).

## Estructura

```
src/
├── LexPCImages.API/                          # composition root
├── Shared/
│   ├── LexPCImages.Shared/                   # Result<T>, Error (sin dependencia web)
│   └── LexPCImages.Shared.Web/               # Error → ProblemDetails
└── Modules/Optimizer/
    ├── LexPCImages.Modules.Optimizer.Domain/
    ├── LexPCImages.Modules.Optimizer.Application/
    ├── LexPCImages.Modules.Optimizer.Infrastructure/
    └── LexPCImages.Modules.Optimizer.Presentation/
tests/
├── LexPCImages.UnitTests/
├── LexPCImages.ArchitectureTests/
└── LexPCImages.IntegrationTests/
```

git-canary-line-1787831616
