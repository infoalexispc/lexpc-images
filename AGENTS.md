# AGENTS.md — lexpc-images

API en **.NET 10** con arquitectura **hexagonal + DDD** en monolito modular. Composition root único: `src/LexPCImages.API/Program.cs`. v0.1 expone el módulo `Optimizer` con tres pipelines reales seleccionados por el `SlotMode` del slot.

## Stack

- **.NET 10** (`net10.0`), `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<LangVersion>latest</LangVersion>`, `TreatWarningsAsErrors=true`
- **`.editorconfig` en la raíz** con las reglas de estilo y nombres. `EnforceCodeStyleInBuild=true` + `GenerateDocumentationFile=true` hacen que IDE0005 (usings innecesarios) rompa la compilación.
- **Versiones NuGet centralizadas** en `Directory.Packages.props` raíz. Los `.csproj` llevan `<PackageReference Include="X" />` **sin** atributo `Version`.
- **CentralPackageTransitivePinningEnabled** para fijar versiones transitivas seguras (hoy `Microsoft.OpenApi 2.7.5`).
- **ImageSharp 3.1.12** — decode, resize Lanczos3, encode WebP lossless.
- **OnnxRuntime 1.22** — inferencia del modelo de segmentación.
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
│       ├── Domain/                       # ProcessJob, SlotDefinition, RefinementOptions, CoverFitOptions (sin dependencias)
│       ├── Application/                  # casos de uso, puertos, pipelines, progreso, validación, errores
│       ├── Infrastructure/               # ONNX, ImageSharp, refinadores de máscara, cola, repositorio
│       └── Presentation/                 # OptimizerController, OptimizerModule, DTOs
└── tests/
    ├── LexPCImages.UnitTests/            # 185 tests
    ├── LexPCImages.ArchitectureTests/    # 18 tests
    └── LexPCImages.IntegrationTests/     # 16 tests
```

### Estructura interna de Application

| Carpeta | Contenido |
|---|---|
| `Abstractions/` | Puertos hacia servicios técnicos (`IImageDecoder`, `IImageEncoder`, `IBackgroundRemovalService`, refinadores, `IJobProgressNotifier`) y sus DTOs (`DecodedImage`, `MaskResult`, `EncodedImage`…) |
| `Ports/` | Puertos hacia infraestructura de estado: `IJobRepository`, `IJobQueueWriter`/`IJobQueueReader`, `ISlotRegistry` |
| `Pipelines/` | Una estrategia por `SlotMode`: `BackgroundRemovalPipeline`, `ResizeAndPadPipeline`, `CoverOrPadPipeline` |
| `Progress/` | `OptimizerProgress` (tabla de tramos), `StageProgress`, extensiones del notificador |
| `Imaging/` | `MaskCompositor`: composición de la máscara sobre el RGBA |
| `Validation/` | `ImageContentTypes`: media types admitidos + firma real de los bytes |
| `Errors/` | `OptimizerErrors`: catálogo de `Error` con los `code` que publica la API |
| `UseCases/` | `EnqueueJob`, `GetJobStatus`, `GetJobDownload`, `ProcessImage` |

### Estructura interna de Infrastructure

| Carpeta | Contenido |
|---|---|
| `Ai/` | `OnnxBackgroundRemovalService` |
| `Imaging/` | Servicios respaldados por ImageSharp + `Internal/RgbaImageInterop` y `Internal/Morphology` |
| `MaskRefinement/` | Algoritmos puros sobre arrays: `ShadowSuppressor`, `DeskMaskRefiner`, `LegProtector`, `TightCropper` |
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

`BackgroundRemovalPipeline` (`SlotMode.BackgroundRemoval`): RMBG → protección de patas → mesa → sombras → recorte ajustado → composición de máscara → estirado al tamaño del slot.
`ResizeAndPadPipeline` (`SlotMode.ResizeAndPad`): escalado proporcional + relleno con el color de fondo dominante.
`CoverOrPadPipeline` (`SlotMode.CoverOrPad`): decide entre recortar y rellenar según la cobertura que dejaría el
recorte (`CoverFitOptions.ShouldCrop`). Por encima del umbral escala cubriendo y recorta centrado; por debajo
delega en el mismo relleno que `ResizeAndPad`. Un solo remuestreo en ambos caminos.

### Progreso

Los porcentajes viven **solo** en `Application/Progress/OptimizerProgress.cs`. Ningún literal de progreso fuera de ese fichero.

| Etapa | Tramo | Pipeline |
|---|---|---|
| `Decoding` | 5 → 15 | todos |
| `Inferring` | 15 → 50 | BackgroundRemoval |
| `LegProtecting` | 50 → 58 | BackgroundRemoval (opcional) |
| `DeskRemoving` | 58 → 66 | BackgroundRemoval (opcional) |
| `ShadowSuppressing` | 66 → 74 | BackgroundRemoval (opcional) |
| `Cropping` | 74 → 82 | BackgroundRemoval |
| `Resizing` | 82 → 90 | BackgroundRemoval |
| `Resizing` | 15 → 90 | ResizeAndPad |
| `Resizing` | 15 → 90 | CoverOrPad |
| `Encoding` | 92 → 100 | todos |

## Endpoints

| Método | Ruta | Comportamiento |
|---|---|---|
| `POST` | `/api/optimizer/jobs` | `multipart/form-data` con `slotId` + `file` (+ `shadowSuppression`, `deskRemoval`, `legProtection`, `cropMarginPct`). `202` con `{ jobId, status }` |
| `GET` | `/api/optimizer/jobs/{id}` | `{ jobId, status, stage, progress, createdAt, completedAt, errorMessage }` |
| `GET` | `/api/optimizer/jobs/{id}/download` | WebP final (200) o `409 Conflict` si el job no está en `Done` |
| `GET` | `/api/optimizer/health` | Health check del módulo |
| `GET` | `/health` | Health check de ASP.NET |
| `GET` | `/openapi/v1.json` | Spec OpenAPI 3.1 |
| `GET` | `/scalar` | UI de Scalar |

### Errores

Todos los errores salen como `application/problem+json` con un campo `code` estable, traducidos por `ErrorHttpMapper` (`src/Shared/LexPCImages.Shared.Web`). El middleware global **solo** responde `500` genérico: los errores esperables viajan como `Result` desde los casos de uso.

Códigos: `optimizer.slot_not_found`, `optimizer.slot_id_required`, `optimizer.file_required`, `optimizer.image_empty`, `optimizer.image_too_large`, `optimizer.image_format_not_supported`, `optimizer.image_content_mismatch`, `optimizer.image_too_small`, `optimizer.image_dimensions_too_large`, `optimizer.crop_margin_out_of_range`, `optimizer.processing_queue_full`, `optimizer.job_not_found`, `optimizer.job_not_ready`, `optimizer.pipeline_not_available`, `internal.error`.

### Validación de la subida

El `Content-Type` lo declara el cliente, así que además se comprueba la **firma real** de los bytes (PNG, JPEG, RIFF+WEBP). Un fichero que no sea una imagen admitida se rechaza con `400 optimizer.image_content_mismatch` **antes** de crear el trabajo.

## Configuración

Enlazada con el patrón Options y validada al arrancar (`ValidateDataAnnotations().ValidateOnStart()`): un valor inválido detiene el proceso en lugar de degradarse en silencio.

```jsonc
{
  "Optimizer": {
    "ModelPath": "models/rmbg-1.4-fp16.onnx", // relativa → AppContext.BaseDirectory
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

## Modelo ONNX

- **Modelo**: `briaai/RMBG-1.4` FP16 (~84 MB) en `models/rmbg-1.4-fp16.onnx`
- **Input**: tensor `input` `[1, 3, 1024, 1024]` float32, `mean=0.5, std=1.0`
- **Output**: tensor `output` `[1, 1, 1024, 1024]` float32, sigmoid ya aplicada
- **Tests**: `OptimizerWebApplicationFactory` sustituye `IBackgroundRemovalService` por un doble con máscara circular, así que la suite no necesita el fichero del modelo.

El modelo se copia al output en cada build vía `<None Include="..\..\models\**\*" ... CopyToOutputDirectory="PreserveNewest" />`.

## Convenciones a respetar

1. **Ningún literal de progreso** fuera de `OptimizerProgress`.
2. **Ningún `switch` sobre `ErrorType` → HTTP** fuera de `ErrorHttpMapper` (hay un test que lo comprueba).
3. **El dominio no llama a `DateTimeOffset.UtcNow`**: la hora entra como parámetro (hay un test que inspecciona el IL).
4. **Nada de excepciones como flujo de control** en validación: usar `TryCreate`/`TryWith`/`Result`.
5. **Toda mutación del agregado se confirma con `IJobRepository.UpdateAsync`**, aunque el repo en memoria comparta referencia.
6. **Conversión RGBA ↔ ImageSharp** solo por `RgbaImageInterop`; morfología solo por `Morphology`.
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
| `UnitTests` | 185 | Dominio, casos de uso, pipelines, repositorio (con `FakeTimeProvider`), validación de firma, cola, ImageSharp, `Result<T>` |
| `ArchitectureTests` | 18 | Capas, dominio sin dependencias, contratos, independencia web, reloj del dominio, mapeo de errores centralizado, no console |
| `IntegrationTests` | 16 | `WebApplicationFactory` con doble de segmentación: enqueue → polling → download, los tres `SlotMode`, `problem+json` |

**Total: 219/219 verdes.**

## Estado de las fases

- **F0 Skeleton** ✅
- **F1 Hardening arquitectura** ✅
- **F2 Stub HTTP** ✅
- **F3 Pipeline real** ✅ — verificado end-to-end con el modelo descargado.
- **F3.5 Refactor arquitectura** ✅ — pipelines por estrategia, patrón Options, repositorio con retención, mapeo de errores unificado, validación por firma, dominio con reloj inyectado.
- **F4 PWA polish** (frontend)
- **F5 Polish** — rate limiting, persistencia durable, cobertura 90%, CI.
