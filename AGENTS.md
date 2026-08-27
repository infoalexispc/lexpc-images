# AGENTS.md — lexpc-images

API en **.NET 10** con arquitectura **hexagonal + DDD** en monolito modular. Composition root único: `src/LexPCImages.API/Program.cs`. v0.1 expone el módulo `Optimizer` con pipeline real: decode → BiRefNet (RMBG) → mask → fit+pad 320×315 → WebP.

## Stack

- **.NET 10** (`net10.0`), `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<LangVersion>latest</LangVersion>`, `TreatWarningsAsErrors=true`
- **Versiones NuGet centralizadas** en `Directory.Packages.props` raíz. Los `.csproj` llevan `<PackageReference Include="X" />` **sin** atributo `Version`.
- **CentralPackageTransitivePinningEnabled** para fijar versiones vulnerables (hoy `Microsoft.OpenApi 2.0.0`).
- **ImageSharp 3.1.12** (`SixLabors.ImageSharp`) — decode, resize Lanczos3, encode WebP (lossless via `WebpFileFormatType.Lossless`).
- **OnnxRuntime 1.22** (`Microsoft.ML.OnnxRuntime`) — inferencia del modelo de segmentación.
- **Scalar.AspNetCore** en `/scalar` (OpenAPI en `/openapi/v1.json`).
- **Serilog.AspNetCore** + `Serilog.Sinks.Console`.
- **Tests**: `xUnit` + `FluentAssertions` + `NSubstitute` + `NetArchTest.Rules` + `Microsoft.AspNetCore.Mvc.Testing` + `coverlet.collector`.

## Módulos y capas

```
Domain          →  (nada)
Application     →  Domain, Shared
Module.Infrastructure  →  Application, Domain, Shared
Module.Presentation    →  Application, Shared + FrameworkReference AspNetCore.App
Shared          →  (nada) + FrameworkReference AspNetCore.App
Infrastructure  →  Shared
API             →  TODAS
```

```
LexPCImages.slnx
├── src/
│   ├── LexPCImages.API/                  # composition root + Scalar + health + CORS
│   ├── Shared/LexPCImages.Shared/        # Result<T>, Error, ErrorType, IModuleRegistration
│   ├── Infrastructure/LexPCImages.Infrastructure/   # (vacío por ahora)
│   └── Modules/Optimizer/
│       ├── Domain/                       # ProcessJob, SlotDefinition, OptimizerErrors, IJobRepository, ISlotRegistry
│       ├── Application/                  # EnqueueJobHandler, GetJobStatusHandler, ProcessImageHandler, abstracciones
│       ├── Infrastructure/              # OnnxBackgroundRemovalService, ImageSharp*, WebpEncoderService, BackgroundService
│       └── Presentation/                # OptimizerController, OptimizerModule, DTOs
└── tests/
    ├── LexPCImages.UnitTests/         # xUnit (Domain + Application + ImageSharp/Onnx/Webp services)
    ├── LexPCImages.ArchitectureTests/ # NetArchTest (capas + interfaces + no console)
    └── LexPCImages.IntegrationTests/  # WebApplicationFactory (pipeline completo con FakeBackgroundRemovalService)
```

## Pipeline (F3)

```
POST /api/optimizer/jobs  →  enqueue en Channel<Guid>
   ↓
[ImageProcessingBackgroundService]
   ↓
ProcessImageHandler.HandleAsync(job):
   1. IImageDecoder.DecodeAsync       (ImageSharp: cualquier formato → RGBA)
   2. IBackgroundRemovalService.RemoveBackgroundAsync  (RMBG-1.4 ONNX → mask)
   3. Apply mask: rgba.a *= mask
   4. IImageResizer.ResizeAsync        (ImageSharp ResizeMode.Pad, Lanczos3, PadColor=Transparent)
   5. IImageEncoder.EncodeWebPAsync     (ImageSharp WebP lossless)
   ↓
ProcessJob.MarkDone(outputBytes, "image/webp")
   ↓
GET /api/optimizer/jobs/{id}/download  →  File(outputBytes, "image/webp", "pc-home-{id}.webp")
```

Progreso notificado vía `IJobProgressNotifier` → `IJobRepository`:
- `Decoding` 10% → 20%
- `Inferring` 25% → 60%
- `Masking` 65% → 75%
- `Resizing` 80% → 92%
- `Encoding` 95% → 100%

## Endpoints

| Método | Ruta | Comportamiento |
|---|---|---|
| `POST` | `/api/optimizer/jobs` | `multipart/form-data` con `slotId` + `file`. Devuelve `202` con `{ jobId, status }` |
| `GET` | `/api/optimizer/jobs/{id}` | Estado: `{ jobId, status, stage, progress, createdAt, completedAt, errorMessage }` |
| `GET` | `/api/optimizer/jobs/{id}/download` | Sirve el WebP final (200 OK) o `409 Conflict` si el job no está en `Done` |
| `GET` | `/api/optimizer/health` | Health check del módulo |
| `GET` | `/health` | Health check de ASP.NET |
| `GET` | `/openapi/v1.json` | Spec OpenAPI 3.1 |
| `GET` | `/scalar` | UI de Scalar (302 redirect) |

## Modelo ONNX

- **Modelo**: `briaai/RMBG-1.4` FP16 (~84 MB), descargado en `models/rmbg-1.4-fp16.onnx`
- **Input**: tensor `input` shape `[1, 3, 1024, 1024]` float32, normalizado con `mean=0.5, std=1.0`
- **Output**: tensor `output` shape `[1, 1, 1024, 1024]` float32, sigmoid ya aplicada (`[0, 1]`)
- **Config**: `appsettings.json` → `Optimizer:ModelPath`. Si es relativo, se resuelve contra `AppContext.BaseDirectory` (junto al .dll)
- **Reemplazo para tests**: en `OptimizerPipelineTests` se sustituye `IBackgroundRemovalService` por un fake que genera una máscara circular, permitiendo testear el pipeline sin modelo

El modelo se copia automáticamente al output en cada build vía:
```xml
<None Include="..\..\models\**\*" Link="models\%(RecursiveDir)%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />
```

## CORS

`http://localhost:4300` (frontend dev) está allowlist. Para producción, ampliar en `appsettings.Production.json` o variable de entorno.

## Estado de las fases

- **F0 Skeleton** ✅ — solución, 10 proyectos, `/health` 200, Scalar sirviendo.
- **F1 Hardening arquitectura** ✅ — `Result<T>`, `GlobalExceptionMiddleware`, 7 tests NetArchTest.
- **F2 Stub HTTP** ✅ — `POST /api/optimizer/jobs` con validación + encolar, `GET /status`, frontend con UploadDialog + ProcessingDialog.
- **F3 Pipeline real** ✅ — `OnnxBackgroundRemovalService` + `ImageSharp*` + `WebpEncoderService` + `BackgroundService` con `Channel<T>`, `GET /download`, **pipeline end-to-end verificado con el modelo real descargado**.
- **F4 PWA polish** (frontend) — install prompt + SwUpdate (ya cubierto por el schematic en F0).
- **F5 Polish** — Serilog estructura fina, rate limit, health checks, cobertura 90%, CI.

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
| `UnitTests` | 34 | `ProcessJob`, `EnqueueJobHandler`, `GetJobStatusHandler`, `ProcessImageHandler`, `ImageSharpDecoder`, `ImageSharpResizer`, `WebpEncoderService`, `Result<T>` |
| `ArchitectureTests` | 11 | Capas, interfaces vs records, no console, no System.Web |
| `IntegrationTests` | 8 | `WebApplicationFactory` con `FakeBackgroundRemovalService`: enqueue → polling → download |

**Total: 53/53 verdes.**
