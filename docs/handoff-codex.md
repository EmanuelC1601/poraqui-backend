# Handoff de desarrollo

## Base integrada

`main` es la base de integración y apunta al merge commit `e9c4435` (PR #4); PR #3 (`962e74a`) integró catálogo/pedido, PR #2 (`a38ed4e`) Identity/negocio, y PR #1 (`e346995`) la arquitectura y los mapeos iniciales. El flujo actual tiene un único responsable; OpenCode ya no tiene entregas asignadas.

## Incremento en curso

PR #2 a #4 integraron Identity/negocio, catálogo, y el recorrido interno de pedido/comprobante/pago/reparto. `feature/business-payment-details`, basada en el nuevo `main`, añade configuración privada del destinatario de transferencia, snapshot por pedido y una migración aditiva. Está en implementación local sin commit/PR. Hay dos migraciones revisables: `InitialCommerceAndIdentity` y `BusinessTransferInstructionsSnapshot`; ambas generan altas/esquema aditivo. No están aplicadas. Bootstrap admin requiere `Bootstrap:AdminKey` privado.

## Verificaciones

PR #2, #3 y #4 pasaron build sin warnings y 10/10, 11/11 y 13/13 pruebas aisladas, respectivamente; GitHub reportó cero check runs. En la rama actual, `dotnet build Poraqui.sln` pasa con 0 warnings/errors, `dotnet test Poraqui.sln` pasa 14/14 y `dotnet ef migrations has-pending-model-changes` confirma snapshot alineado. La migración aditiva de transferencia se generó y revisó como solo AddColumn nullable. `ConnectionStrings:DefaultConnection` sigue ausente, no hay conexión de aplicación a SQL Server ni migraciones aplicadas. El portal MonsterASP respondió antes a consulta de solo lectura y reportó cero tablas de usuario.

## Configuración SQL privada

La aplicación requiere `ConnectionStrings:DefaultConnection` (variable `ConnectionStrings__DefaultConnection`). Está ausente en la configuración privada local comprobada. Configurar sin compartir el valor:

El `UserSecretsId` local del proyecto ya está declarado; agrega la clave sin imprimirla ni compartirla:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
```

No usar valores del chat. La API no ejecuta migraciones al arrancar. Cuando esté configurada, primero probar conexión y revisar la migración generada; aplicarla solo si preserva datos y no requiere decisiones funcionales.

## Pendientes inmediatos

Opciones de producto y horarios; datos de transferencia del negocio; reparto externo; notificaciones SignalR, sincronización/idempotencia, incidencias/reembolsos, métricas y despliegue. El estado verificable está en `docs/project-status.md`; el recorrido API está en `docs/order-flow.md`.

## Actualización para la siguiente persona/entorno

```bash
git fetch origin
git switch main
git pull --ff-only
git switch -c feature/<nombre>
```

La rama de trabajo actual se basa en `main`. Al integrar cada PR: actualizar con `git switch main && git pull --ff-only`, y regenerar/revisar cualquier migración antes de aplicarla. Los siguientes incrementos deben actualizar este handoff y el ledger.
