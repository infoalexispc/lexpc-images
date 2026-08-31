# AGENTS.md — lexpc-images

API en **.NET 10** con arquitectura **hexagonal + DDD** en monolito modular. Composition root único: `src/LexPCImages.API/Program.cs`. v0.1 expone el módulo `Optimizer` con tres pipelines de reescalado seleccionados por el `SlotMode` del slot.

## Stack

- **.NET 10** (`net10.0`), `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<LangVersion>latest</LangVersion>`, `TreatWarningsAsErrors=true`
- **`.editorconfig` en la raíz** con las reglas de estilo y nombres. `EnforceCodeStyleInBuild=true` + `GenerateDocumentationFile=true` hacen que IDE0005 (usings innecesarios) rompa la compilación.
- **Versiones NuGet centralizadas** en `Directory.Packages.props` raíz. Los `.csproj` llevan `<PackageReference Include="X" />` **sin** atributo `Version`.
- **CentralPackageTransitivePinningEnabled** para fijar versiones transitivas seguras (hoy `Microsoft.OpenApi 2.7.5`).
- **ImageSharp 3.1.12** — decode, recorte del marco alfa, resize con filtro configurable (`Box`/`Lanczos3`), encode WebP con pérdida a esfuerzo máximo.
- **Scalar.AspNetCore** en `/scalar` (OpenAPI en `/openapi/v1.json`), **Serilog.AspNetCore** + `Serilog.Sinks.Console`.
- **Tests**: `xUnit` + `FluentAssertions` + `NSubstitute` + `Microsoft.Extensions.TimeProvider.Testing` + `NetArchTest.Rules` + `Microsoft.AspNetCore.Mvc.Testing` + `coverlet.collector`.

## Módulos y capas

```
Domain                 →  (NADA: ni proyectos ni paquetes, solo el BCL)
Application            →  Domain, Shared
Module.Infrastructure  →  Application, Domain, Shared
Shared.Web             →  Shared + FrameworkReference AspNetCore.App
Module.Presentation    →  Application, Shared, Shared.Web + FrameworkReference AspNetCore.App
Shared                 →  (nada; sin dependencia web)
API                    →  TODAS
```

Las reglas anteriores están fijadas por tests de `NetArchTest`, incluida "solo las capas web pueden referenciar ASP.NET Core" y "el dominio no lee el reloj ambiental" (esta última inspecciona el IL).

**El dominio no referencia absolutamente nada.** Referenciaba `Shared` por un único fichero,
`OptimizerErrors`, que en realidad es el catálogo de respuestas de la API (`code` + mensaje para
el consumidor HTTP) y no vocabulario de negocio; por eso vive ahora en `Application/Errors/`. Las
reglas de negocio de verdad —transiciones de `ProcessJob`, rango de `CropMarginPct`— siguen en el
dominio y se defienden con excepciones, sin necesitar `Result<T>`.

**El host no declara controladores propios y no tiene carpeta `Controllers/`.** Cada módulo aporta
los suyos desde su proyecto `*.Presentation`, y el composition root los registra con
`AddOptimizerPresentation()`, que hace el `AddApplicationPart` del ensamblado del módulo. Si
aparece un `src/LexPCImages.API/Controllers/` es scaffolding de `dotnet new webapi`, no forma parte
del diseño.

```
LexPCImages.slnx
├── src/
│   ├── LexPCImages.API/                  # composition root + Scalar + health + CORS
│   ├── Shared/
│   │   ├── LexPCImages.Shared/           # Result<T>, Error, ErrorType
│   │   └── LexPCImages.Shared.Web/       # ErrorHttpMapper: única traducción Error → HTTP
│   └── Modules/Optimizer/
│       ├── Domain/                       # ProcessJob, SlotDefinition, SlotBundle, CoverFitOptions (sin dependencias)
│       ├── Application/                  # casos de uso, puertos, pipelines, progreso, validación, errores
│       ├── Infrastructure/               # ImageSharp, cola, catálogo de slots, repositorio
│       └── Presentation/                 # OptimizerController, OptimizerModule, DTOs
└── tests/
    ├── LexPCImages.UnitTests/            # 142 tests
    ├── LexPCImages.ArchitectureTests/    # 18 tests
    └── LexPCImages.IntegrationTests/     # 16 tests
```

### Estructura interna de Application

| Carpeta | Contenido |
|---|---|
| `Abstractions/` | Puertos hacia servicios técnicos (`IImageDecoder`, `IImageEncoder`, `IImageResizer`, `IImagePadder`, `IImageTrimmer`, `IJobProgressNotifier`) y sus DTOs (`DecodedImage`, `EncodedImage`…) |
| `Ports/` | Puertos hacia infraestructura de estado: `IJobRepository`, `IJobQueueWriter`/`IJobQueueReader`, `ISlotRegistry` |
| `Pipelines/` | Una estrategia por `SlotMode`: `ResizeAndPadPipeline`, `CoverOrPadPipeline`, `FitTransparentPipeline` |
| `Progress/` | `OptimizerProgress` (tabla de tramos), `StageProgress`, extensiones del notificador |
| `Validation/` | `ImageContentTypes`: media types admitidos + firma real de los bytes |
| `Errors/` | `OptimizerErrors`: catálogo de `Error` con los `code` que publica la API |
| `UseCases/` | `EnqueueJob`, `GetJobStatus`, `GetJobDownload`, `ProcessImage` |

### Estructura interna de Infrastructure

| Carpeta | Contenido |
|---|---|
| `Imaging/` | Servicios respaldados por ImageSharp + `Internal/RgbaImageInterop` y `Internal/ResamplerSelector`. `AlphaBorderTrimmer` no usa ImageSharp: recorre el búfer RGBA |
| `Persistence/` | `InMemoryJobRepository` con retención y tope de trabajos |
| `Queue/`, `Registries/`, `BackgroundProcessing/`, `Configuration/` | Cola `Channel<T>`, catálogo de slots, worker, `OptimizerOptions` |

## Pipeline

`ProcessImageHandler` decodifica, elige la estrategia según `slot.Mode` y codifica. **Añadir un modo nuevo es añadir una implementación de `IImageProcessingPipeline` y una línea en `AddOptimizerApplication`; no se toca el orquestador.**

```
POST /api/optimizer/jobs  →  validación (tamaño + media type + firma real) → cola acotada
   ↓
[ImageProcessingBackgroundService]
   ↓
ProcessImageHandler.HandleAsync(job):
   1. IImageDecoder.DecodeAsync        → RGBA
   2. validación de dimensiones        → 200..8000 px
   3. IImageProcessingPipeline.ExecuteAsync (según SlotMode)
   4. IImageEncoder.EncodeAsync        → EncodedImage(bytes, "image/webp")
   ↓
ProcessJob.MarkDone(bytes, contentType, now)
   ↓
GET /api/optimizer/jobs/{id}/download  →  "{slotId}-{jobId:N}.webp"
```

`ResizeAndPadPipeline` (`SlotMode.ResizeAndPad`): escalado proporcional + relleno con el color de fondo dominante.
`FitTransparentPipeline` (`SlotMode.FitTransparent`): recorte del marco transparente + escalado proporcional
dejando transparente lo que sobra. Es el modo para imágenes que ya llegan sin fondo: no recorta contenido, no
deforma y no inventa un color de relleno que se notaría sobre el alfa del original. El recorte previo evita
gastar los píxeles del slot en el aire del máster, que es lo que hacía salir el producto más pequeño —y por
tanto menos nítido— de lo que cabe.
`CoverOrPadPipeline` (`SlotMode.CoverOrPad`): decide entre recortar y rellenar según la cobertura que dejaría el
recorte (`CoverFitOptions.ShouldCrop`). Por encima del umbral escala cubriendo y recorta centrado; por debajo
delega en el mismo relleno que `ResizeAndPad`. Un solo remuestreo en ambos caminos.

### Progreso

Los porcentajes viven **solo** en `Application/Progress/OptimizerProgress.cs`. Ningún literal de progreso fuera de ese fichero.

| Etapa | Tramo | Pipeline |
|---|---|---|
| `Decoding` | 5 → 15 | todos |
| `Resizing` | 15 → 90 | los tres |
| `Encoding` | 92 → 100 | todos |

## Endpoints

| Método | Ruta | Comportamiento |
|---|---|---|
| `POST` | `/api/optimizer/jobs` | `multipart/form-data` con `slotId` + `file`. `202` con `{ jobs: [{ jobId, slotId, width, height, status }] }`, **siempre una lista** |
| `GET` | `/api/optimizer/jobs/{id}` | `{ jobId, status, stage, progress, createdAt, completedAt, errorMessage }` |
| `GET` | `/api/optimizer/jobs/{id}/download` | WebP final (200) o `409 Conflict` si el job no está en `Done` |
| `GET` | `/api/optimizer/health` | Health check del módulo |
| `GET` | `/health` | Health check de ASP.NET |
| `GET` | `/openapi/v1.json` | Spec OpenAPI 3.1 |
| `GET` | `/scalar` | UI de Scalar |

### Errores

Todos los errores salen como `application/problem+json` con un campo `code` estable, traducidos por `ErrorHttpMapper` (`src/Shared/LexPCImages.Shared.Web`). El middleware global **solo** responde `500` genérico: los errores esperables viajan como `Result` desde los casos de uso.

Códigos: `optimizer.slot_not_found`, `optimizer.slot_id_required`, `optimizer.file_required`, `optimizer.image_empty`, `optimizer.image_too_large`, `optimizer.image_format_not_supported`, `optimizer.image_content_mismatch`, `optimizer.image_too_small`, `optimizer.image_dimensions_too_large`, `optimizer.processing_queue_full`, `optimizer.job_not_found`, `optimizer.job_not_ready`, `optimizer.pipeline_not_available`, `internal.error`.

### Validación de la subida

El `Content-Type` lo declara el cliente, así que además se comprueba la **firma real** de los bytes (PNG, JPEG, RIFF+WEBP). Un fichero que no sea una imagen admitida se rechaza con `400 optimizer.image_content_mismatch` **antes** de crear el trabajo.

## Configuración

Enlazada con el patrón Options y validada al arrancar (`ValidateDataAnnotations().ValidateOnStart()`): un valor inválido detiene el proceso en lugar de degradarse en silencio.

```jsonc
{
  "Optimizer": {
    "QueueCapacity": 100,                     // 1..10000
    "JobRetention": "00:30:00",               // 1 min .. 24 h
    "MaxTrackedJobs": 500                     // tope duro de trabajos en memoria
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:4300" ] // al menos uno; sin AllowCredentials
  }
}
```

`InMemoryJobRepository` descarta los trabajos terminados al superar `JobRetention` y recorta los más antiguos al pasar de `MaxTrackedJobs`: cada trabajo guarda la imagen de entrada (hasta 15 MB) y la de salida.

## Paquetes de slots

Un id público puede resolver a **varias salidas**. `ISlotRegistry.Resolve(SlotId)` devuelve la
lista: un slot suelto da una y un `SlotBundle` da todas las suyas, así que `EnqueueJobHandler` no
distingue entre los dos casos. La imagen se valida una vez y se crea **un `ProcessJob` por
salida** compartiendo los mismos bytes; el invariante "un trabajo produce una imagen" no cambia.

Hoy hay un único paquete, `SlotBundle.PcHome` (`optimizar-imagen-pc-home`), que publica en
`optimizar-imagen-pc-home-320x315` y `optimizar-imagen-pc-home-992x715`. Los ids de salida llevan
el tamaño porque el nombre del fichero descargado sale de ahí (`GetJobDownloadHandler.BuildFileName`):
al bajar las dos de golpe se distinguen solas.

Si la cola se llena a mitad de un paquete, los trabajos ya creados se cierran en error: ninguno
puede quedarse en `Queued` para siempre.

## Convenciones a respetar

1. **Ningún literal de progreso** fuera de `OptimizerProgress`.
2. **Ningún `switch` sobre `ErrorType` → HTTP** fuera de `ErrorHttpMapper` (hay un test que lo comprueba).
3. **El dominio no llama a `DateTimeOffset.UtcNow`**: la hora entra como parámetro (hay un test que inspecciona el IL).
4. **Nada de excepciones como flujo de control** en validación: usar `TryCreate`/`TryWith`/`Result`.
5. **Toda mutación del agregado se confirma con `IJobRepository.UpdateAsync`**, aunque el repo en memoria comparta referencia.
6. **Conversión RGBA ↔ ImageSharp** solo por `RgbaImageInterop`; elección de remuestreador solo por `ResamplerSelector`.
7. Los controladores **no validan reglas de negocio**: traducen y delegan.
8. **Nada nuevo en `Domain` que necesite una referencia externa.** Si algo requiere `Result<T>`,
   `Error` o un paquete, es que pertenece a `Application`.

## Comandos

```bash
dotnet restore LexPCImages.slnx
dotnet build LexPCImages.slnx
dotnet test LexPCImages.slnx
dotnet run --project src/LexPCImages.API     # puerto 5232
```

## Tests

| Suite | Tests | Cubre |
|---|---|---|
| `UnitTests` | 162 | Dominio, casos de uso, pipelines, repositorio (con `FakeTimeProvider`), validación de firma, cola, ImageSharp, `Result<T>` |
| `ArchitectureTests` | 18 | Capas, dominio sin dependencias, contratos, independencia web, reloj del dominio, mapeo de errores centralizado, no console |
| `IntegrationTests` | 16 | `WebApplicationFactory` sin dobles: enqueue → polling → download, paquete y slots sueltos, `problem+json` |

**Total: 196/196 verdes.**

## Estado de las fases

- **F0 Skeleton** ✅
- **F1 Hardening arquitectura** ✅
- **F2 Stub HTTP** ✅
- **F3 Pipeline real** ✅
- **F3.5 Refactor arquitectura** ✅ — pipelines por estrategia, patrón Options, repositorio con retención, mapeo de errores unificado, validación por firma, dominio con reloj inyectado.
- **F4 PWA polish** (frontend)
- **F5 Polish** — rate limiting, persistencia durable, cobertura 90%, CI.
