# Handoff de desarrollo

## Responsabilidad y flujo

Este repositorio tiene un único responsable para todos los módulos: identidad/permisos, comercio, pedidos, pagos manuales, reparto propio/externo, notificaciones, sincronización, métricas, persistencia, migraciones y despliegue. OpenCode ya no participa; se conserva esa asignación anterior únicamente como antecedente. `main` es la base de integración. Trabajar en `feature/*`, mantener PR pequeños, compilar/probar y revisar migraciones antes de integrar. Actualizar `docs/project-status.md`, `docs/api-contracts.md` y este handoff en cada incremento.

## Estado de ramas y PR

- `main` integra PR #1–#6 (`d39d265`).
- PR #7–#9 fueron integrados en sus ramas apiladas.
- PR #10 se integró en `feature/catalog-options` con merge commit `fc68274` y ya está cerrado como merged.
- PR #11 (cancelaciones/incidencias/reembolsos) sigue abierto; contiene PR #12 (métricas) ya integrado en su rama.
- PR #13–#14 y PR #18–#21 se mantienen en ramas apiladas y todavía no están en `main`.

## SQL Server y migraciones

La clave privada `ConnectionStrings:DefaultConnection` está configurada en User Secrets y EF logró consultar la base SQL Server. El historial lista las migraciones hasta `OrderCancellationAuditConstraints`; `migrations has-pending-model-changes` no detecta desajustes. No se aplicaron migraciones en esta sesión. La API no migra al arrancar.

`ConnectionStrings:SqlServerTest` no está configurada. Añádela en User Secrets de `src/Poraqui.Api` apuntando a una base SQL Server separada para las pruebas que creen, limpien o compitan por datos. No dirigir esas pruebas a la BD compartida ni borrar, recrear o reiniciar sus datos. Mantener secretos fuera de Git, consola y logs.

## Build y pruebas

Sobre la rama de PR #11, incluyendo métricas #12: `dotnet build Poraqui.sln --no-restore` pasa con 0 advertencias/errores; `dotnet test Poraqui.sln --no-build --no-restore` pasa 19/19 con SQLite aislada. Una prueba comprueba traducción de la consulta SQL Server sin conectarse a SQL Server. Este host tiene SDK/runtime .NET 10 mientras la solución apunta a net9; para ejecutar test host usar `$env:DOTNET_ROLL_FORWARD='Major'`. No confundir SQLite/traducción con verificación de restricciones o concurrencia SQL Server.

El recorrido funcional, endpoints y límites están en `docs/order-flow.md`. Contratos vigentes y ledger verificable están en `docs/api-contracts.md` y `docs/project-status.md`. Para OpenAPI en Development, ejecutar `dotnet run --project src/Poraqui.Api` y consultar `/openapi/v1.json`.

## Próximos pasos

1. Terminar revisión e integrar PR #11 cuando la rama fuente esté sincronizada con `feature/business-hours` y pase el árbol de merge.
2. Revisar PR #13–#14 y #18–#21 en orden, con foco en permisos cruzados, transiciones, códigos, concurrencia e idempotencia.
3. Preparar una BD aislada SQL Server para migraciones/restricciones y pruebas concurrentes; nunca hacer pruebas destructivas sobre la BD compartida.
4. Completar despliegue MonsterASP.NET, almacenamiento privado persistente y recorrido publicado; validar reconexión SignalR con cliente.

## Antecedente

El plan inicial dividía comercio y reparto entre Codex y OpenCode. El cambio a responsable único ocurrió antes de integrar PR #1. Los contratos iniciales y esa historia se conservan como contexto; no son una dependencia operativa.
