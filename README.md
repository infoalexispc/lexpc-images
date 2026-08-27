# LexPC Images

API en **.NET 10** con arquitectura **hexagonal + DDD** en monolito modular. Composition root único: `src/LexPCImages.API/Program.cs`. v0.1 expone el módulo `Optimizer` (background removal + resize a 320×315 WebP).

## ¿Qué hace?

Pipeline de optimización de imágenes para el catálogo público:

```
upload (image/*)  →  remove background (BiRefNet)  →  resize fit+pad 320×315  →  WebP lossless  →  download
```

v0.1 solo soporta el slot `optimizar-imagen-pc-home`. Los otros 3 slots del frontend son placeholders visuales.

## Arquitectura

Monolito modular con DDD + arquitectura hexagonal. Cada capa es un proyecto C# separado — el compilador fuerza la dirección de dependencias.

```
API  →  Module.Presentation  →  Module.Application  →  Module.Domain
                                     ↓
                              Module.Infrastructure
                                     ↓
                              Shared  (IModuleRegistration)
```

`IModuleRegistration` (en `Shared`) es el contrato que cada módulo implementa para enchufarse al composition root. El host descubre los módulos, les pide `RegisterServices` y luego `MapEndpoints`.

## Requisitos

- **.NET 10 SDK** (`10.0.203+`)
- (Opcional) **PostgreSQL** si se añaden jobs persistentes (F5+)
- (Opcional) **Modelo ONNX** en `models/birefnet-general-fp16.onnx` (F3; no necesario en F0)

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

## Tests

```bash
dotnet test LexPCImages.slnx
```

Cobertura objetivo ≥ 90% (F5+).

## Estructura

```
src/
├── LexPCImages.API/                          # composition root
├── Shared/LexPCImages.Shared/                # contratos transversales
├── Infrastructure/LexPCImages.Infrastructure/
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

Más detalle: [`AGENTS.md`](./AGENTS.md).

git-canary-line-1787831616
