# Flujo de desarrollo

## Responsable

Una persona mantiene todos los módulos: identidad y permisos, comercio, pedidos, pagos manuales, reparto propio y externo, notificaciones, sincronización, métricas, infraestructura y despliegue. Esa persona es también la única responsable de `AppDbContext`, las migraciones y su aplicación a las bases de desarrollo y producción. OpenCode ya no participa; su asignación anterior a reparto se conserva como antecedente y no es un bloqueo.

## Archivos y contratos

- `src/Poraqui.Domain/Orders/OrderStatus.cs` y `src/Poraqui.Application/Orders/OrderWorkflowService.cs` definen el flujo operativo implementado; mantener API, mapeos, pruebas y docs sincronizados. El contrato de reparto inicial fue sustituido al implementar el recorrido propio.
- `src/Poraqui.Infrastructure/Persistence/AppDbContext.cs`, configuración de DI y `src/Poraqui.Infrastructure/Persistence/Migrations/` pertenecen al responsable único del backend.
- `docs/api-contracts.md` y `docs/project-status.md` son la fuente de estado actual y contratos.

## Ramas y base de datos

`main` es la base de integración. Un solo responsable trabaja en incrementos `feature/*`, con PR, build, pruebas y chequeos SQL Server pertinentes antes de integrar. Actualiza `docs/project-status.md`, `docs/handoff-codex.md` y `docs/api-contracts.md` por incremento. PR #1–#5 están integrados; PR #6–#10 están apilados; el trabajo de cancelaciones/incidencias/reembolsos continúa desde PR #10.

Las pruebas que creen o limpien datos usan bases aisladas. La BD de desarrollo puede recibir solo registros identificables de prueba. No borrar ni recrear datos ni reiniciar la BD compartida. No incluir credenciales en Git; usar User Secrets localmente y la configuración privada del hosting en despliegue. La API no aplica migraciones al arrancar.

## Antecedentes

El plan inicial contempló dos agentes y asignó reparto a OpenCode. El equipo cambió a un responsable único antes de integrar PR #1. Los contratos y esa asignación se conservan como antecedentes; no hay dependencia operativa de otra persona.
