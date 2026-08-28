# LexPC Images

API en **.NET 10** con arquitectura **hexagonal + DDD** en monolito modular. Composition root único: `src/LexPCImages.API/Program.cs`. v0.1 expone el módulo `Optimizer` (eliminación de fondo y adaptación de imágenes a los slots del catálogo).

## ¿Qué hace?

Pipeline de optimización de imágenes para el catálogo público. El slot elegido decide el tratamiento:

```
optimizar-imagen-pc-home            →  quitar fondo (RMBG-1.4) → refinar máscara → recortar → 320×315  → WebP lossless
optimizar-imagen-pc-seccion-principal →  escalar proporcional + relleno de fondo   → 1000×720 → WebP lossless
```

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

- **.NET 10 SDK** (`10.0.203+`)
- **Modelo ONNX** en `models/rmbg-1.4-fp16.onnx` (`briaai/RMBG-1.4` FP16, ~84 MB). Solo hace falta para ejecutar la API: la suite de tests usa un doble.

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
# → 202 { "jobId": "...", "status": "Queued" }

curl http://localhost:5232/api/optimizer/jobs/{jobId}
# → { "status": "Processing", "stage": "Inferring", "progress": 15, ... }

curl -O -J http://localhost:5232/api/optimizer/jobs/{jobId}/download
# → optimizar-imagen-pc-home-{jobId}.webp
```

Los errores se devuelven como `application/problem+json` con un campo `code` estable (`optimizer.slot_not_found`, `optimizer.image_content_mismatch`, …).

## Configuración

```jsonc
{
  "Optimizer": {
    "ModelPath": "models/rmbg-1.4-fp16.onnx", // relativa → junto al .dll
    "QueueCapacity": 100,
    "JobRetention": "00:30:00",               // cuánto se conserva un trabajo terminado
    "MaxTrackedJobs": 500
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
| `UnitTests` | 151 | Dominio, casos de uso, pipelines, repositorio, validación, ImageSharp, `Result<T>` |
| `ArchitectureTests` | 18 | Capas, contratos, independencia web, reloj del dominio, mapeo de errores |
| `IntegrationTests` | 14 | `WebApplicationFactory`: enqueue → polling → download, ambos slots, `problem+json` |

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
