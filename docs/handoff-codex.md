# Handoff de desarrollo

## Base y responsabilidad

`main` es la base de integración y está en `d39d265` (PR #1–#6). Un responsable mantiene todos los módulos, contratos, `AppDbContext`, migraciones, pruebas y despliegue. OpenCode ya no participa; la asignación inicial de reparto queda como antecedente. Los incrementos se trabajan en `feature/*` y se integran mediante PR; una integración a otra rama apilada no equivale a integración en `main`.

## Estado de las ramas y PRs

- PR #7 (sincronización offline) está integrado en `feature/external-courier-offers` mediante `fe566db`; aún no está en `main`.
- PR #8 (SignalR) está integrado en `feature/offline-delivery-sync` mediante `f7b4c20`; aún no está en `main`.
- PR #9 (opciones de catálogo y precio) está abierto desde `feature/catalog-options`, basado en `feature/realtime-order-updates`.
- PR #10 (horarios) y #11 (cancelaciones/incidencias/reembolsos) siguen abiertos en ramas apiladas. PR #12 (métricas) está integrado solo en la rama de #11.
- PR #13 (reintentos de reparto), #14 (OTP) y la pila #18–#21 (idempotencia durable) están publicados; #18–#21 se integraron en `feature/phone-verification`, aún fuera de `main`.

## Estado SQL Server

La clave privada `ConnectionStrings:DefaultConnection` existe localmente. EF Core pudo consultar el historial sin revelar la cadena y reportó aplicadas `InitialCommerceAndIdentity`, `BusinessTransferInstructionsSnapshot`, `ExternalCourierOffers`, `OfflineDeliveryCommandSync` y `CatalogProductOptions`. La conexión aislada `ConnectionStrings:SqlServerTest` no existe; las pruebas de concurrencia que requieren limpieza de datos no se ejecutaron. No usar la BD compartida para esas pruebas.

Todas las migraciones deben revisarse antes de aplicación y solo el responsable único las genera/aplica. La API no migra al iniciar. No usar datos bancarios ni contraseñas del chat. No borrar ni recrear la BD, ni reiniciar datos compartidos.

## Verificación y ejecución

En los incrementos #6–#8: `dotnet build Poraqui.sln` terminó con 0 advertencias y errores; `dotnet test Poraqui.sln` pasó 14/14 con SQLite y archivos temporales. La herramienta SDK net10 está disponible localmente mientras la solución apunta a net9; el test host net9 se ejecutó con `DOTNET_ROLL_FORWARD=Major`. PR #8 tiene prueba real de WebSocket SignalR en TestServer; el estado se vuelve a consultar por snapshot.

Para ejecutar localmente, restaura la herramienta EF y configura la conexión privada solo en User Secrets o entorno:

```powershell
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

No pegues ni registres el valor de conexión. Para migraciones locales revisables usa `dotnet ef migrations list` y scripts SQL en `docs/sql`; no ejecutes `database update` hasta revisar el destino y confirmar el cambio.

## Siguiente revisión

Revisar PR #9 (`feature/catalog-options`) y ejecutar build/tests sobre su HEAD actualizado. Confirmar en su PR que la migración solo añade tablas/índices, sin registrar como integrada la función hasta que entre en `main`. Después revisar PR #10–#14 y continuar por la pila de idempotencia. Consultar `docs/project-status.md`, `docs/api-contracts.md` y `docs/order-flow.md` para el ledger y los contratos actuales.

## Antecedentes conservados

El plan inicial dividía comercio y reparto entre Codex y OpenCode. La organización cambió a una sola persona responsable antes de integrar PR #1. Los contratos iniciales de reparto se reemplazaron por el flujo propio y externo actual; la autoría anterior no representa una dependencia operativa.
