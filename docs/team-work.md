# Flujo de desarrollo de responsable único

## Responsabilidad y ramas

Una sola persona mantiene identidad/permisos, comercio, pedidos, pagos manuales, reparto propio/externo, notificaciones, sincronización, métricas, infraestructura y despliegue. Esa persona es dueña de `AppDbContext`, sus configuraciones y todas las migraciones. `main` es la base de integración; los incrementos se desarrollan en ramas `feature/*`, con PR y verificación antes de integrar.

OpenCode ya no participa. La asignación previa del módulo de reparto y los contratos compartidos se conserva como antecedente; no hay entrega pendiente de otra persona.

## Propietarios de archivos compartidos

- Responsable único: `src/Poraqui.Infrastructure/Persistence/AppDbContext.cs`, configuraciones EF, `src/Poraqui.Infrastructure/Persistence/Migrations/`, scripts SQL y configuración del proveedor.
- Responsable único: `src/Poraqui.Domain/Orders/OrderStatus.cs`, entidades y reglas del dominio, `src/Poraqui.Application/Orders/OrderWorkflowService.cs`, controladores, autorización, OpenAPI y pruebas de API.
- Responsable único: `docs/api-contracts.md`, `docs/project-status.md`, `docs/handoff-codex.md`, `docs/order-flow.md` y este archivo.
- Ninguna rama debe sobrescribir trabajo de otra rama: actualizar desde la base, resolver cambios conservando ambas intenciones y registrar contratos/migraciones en el PR.

## Base de datos y verificaciones

La BD de desarrollo es compartida. Se permiten registros funcionales identificables; no borrar, recrear ni reiniciar datos. Las pruebas con limpieza usan una BD aislada. Mantener secretos en User Secrets o configuración privada del hosting, nunca en Git. La API no ejecuta migraciones al arrancar.

Antes de cada PR: revisar `git diff --check`, compilar y ejecutar las pruebas relevantes. Para cambios EF, comprobar `migrations has-pending-model-changes`, generar SQL revisable y verificar que la migración conserva datos. Antes de aplicar en desarrollo, consultar historial y revisar el script; aplicar explícitamente. Las carreras SQL Server requieren la base separada `PoraquiIntegrationTests`.

## Integración actual y antecedentes

`main` contiene PR #1–#6 (`d39d265`). PR #7–#21 están integrados en ramas apiladas. La rama `feature/backend-integration-verified` consolida esa punta para revisión final hacia `main`; build actual 0 warnings/errores, 24 pruebas aprobadas y 2 carreras SQL Server omitidas por falta de configuración aislada.

La planificación inicial dividió el trabajo entre Codex y OpenCode y asignó reparto al segundo. El acuerdo cambió a responsable único; se mantienen los contratos y el historial como referencia.
