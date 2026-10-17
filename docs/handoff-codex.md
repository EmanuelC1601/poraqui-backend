# Handoff de desarrollo

## Base y responsabilidad

`main` es la base de integración y está en `d39d265` (PR #1–#6). Un responsable mantiene módulos, contratos, `AppDbContext`, migraciones, pruebas y despliegue. OpenCode ya no participa; la asignación inicial de reparto queda como antecedente. Se trabaja en ramas `feature/*` y se integra mediante PR. Una integración en una rama apilada no equivale a integración en `main`.

## Ramas y PRs

- PR #7 (sync offline) está integrado en `feature/external-courier-offers` mediante `fe566db`; aún fuera de `main`.
- PR #8 (SignalR) está integrado en `feature/offline-delivery-sync` mediante `f7b4c20`; aún fuera de `main`.
- PR #9 (opciones de catálogo) está integrado en `feature/realtime-order-updates` mediante `6a73c9d`; aún fuera de `main`.
- PR #10 (horarios de negocio) está abierto desde `feature/business-hours`, basado en `feature/catalog-options`.
- PR #11 (cancelaciones, incidencias y reembolsos) sigue abierto; PR #12 de métricas está integrado solo en su rama.
- PR #13 (reintentos de reparto), #14 (OTP) y la pila #18–#21 (idempotencia durable) siguen publicados en ramas; #18–#21 están integrados en `feature/phone-verification`, fuera de `main`.

## SQL Server y migraciones

`ConnectionStrings:DefaultConnection` está presente en User Secrets y EF pudo leer el historial sin revelar la cadena. EF reporta aplicadas las migraciones hasta `BusinessOpeningHours`; la migración es aditiva. La clave `ConnectionStrings:SqlServerTest` no está configurada, por lo que las pruebas concurrentes que limpian datos deben seguir omitidas. Nunca uses la BD compartida para esas pruebas.

El responsable único genera, revisa y aplica las migraciones. La API no migra al iniciar. No ejecutar borrados, recreaciones ni reinicios de la BD compartida. No guardar credenciales en Git ni usar contraseñas/datos bancarios tomados del chat.

## Build y pruebas

En la rama #10, `dotnet build Poraqui.sln` terminó con 0 advertencias/errores y `dotnet test Poraqui.sln` pasó 15/15 con SQLite y archivos temporales. Como la solución apunta a net9 y aquí solo está instalado runtime net10, se usó `DOTNET_ROLL_FORWARD=Major` para el test host. La prueba SQL Server, restricciones vivas y aceptación simultánea siguen pendientes.

Para ejecutar localmente:

```powershell
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

No imprimas ni registres el valor del secreto. Consulta `docs/sql` antes de aplicar cambios. Revisa `docs/project-status.md`, `docs/api-contracts.md` y `docs/order-flow.md` para contratos y estado funcional.

## Siguiente trabajo

Revisar PR #10 actualizado, limpiar `git diff --check` y confirmar que su migración solo agrega columnas y una tabla antes de integrarlo en la base declarada. Después revisar PR #11–#14 y la pila #18–#21; incluir pruebas de permisos, importes, estados, códigos e idempotencia. Completar después pruebas SQL aisladas, OpenAPI/recorrido reproducible y despliegue MonsterASP.

## Antecedente

El plan inicial repartía comercio y reparto entre Codex y OpenCode. La organización cambió a un único responsable antes de integrar PR #1. Los contratos iniciales de reparto se reemplazaron por el flujo propio/externo actual; la autoría anterior no representa una dependencia operativa.
