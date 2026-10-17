# Flujo de desarrollo

## Responsable único

Una persona mantiene identidad y permisos, comercio, pedidos, pagos manuales, reparto propio y externo, notificaciones, sincronización, métricas, infraestructura y despliegue. También es propietaria de `AppDbContext`, contratos persistidos, migraciones y su aplicación. OpenCode ya no participa; su asignación previa a reparto se conserva solo como antecedente.

## Propiedad de archivos

- `src/Poraqui.Domain/Orders/OrderStatus.cs` y `src/Poraqui.Application/Orders/OrderWorkflowService.cs`: estados y reglas del recorrido.
- `src/Poraqui.Infrastructure/Persistence/AppDbContext.cs`, configuración EF, `src/Poraqui.Infrastructure/Persistence/Migrations/` y scripts `docs/sql/`: propietario único del esquema, sin cambios paralelos.
- `docs/api-contracts.md`, `docs/project-status.md`, `docs/handoff-codex.md` y OpenAPI: actualizar junto con cada cambio observable.
- Controllers coordinan HTTP y autorización; Application aplica casos de uso; Domain concentra invariantes; Infrastructure persiste y protege datos.

## Ramas, PR y bases

`main` es la base de integración. Crear `feature/*` desde `main` actualizado, mantener cambios revisables y abrir PR. Antes de integrar, restaurar, compilar, probar reglas/permisos y revisar el SQL generado. No declarar terminado un módulo por entidades o mapeos sin recorrer sus casos de uso.

La conexión compartida solo se usa para lecturas seguras y datos funcionales identificables. Las pruebas que modifiquen, compitan o limpien datos deben apuntar a una BD aislada. Nunca borrar/recrear/resetear la compartida. Mantener cadenas y secretos en User Secrets o configuración privada del hosting. La API no aplica migraciones al arrancar.

## Antecedentes

El plan inicial contempló dos agentes y asignó reparto a OpenCode. Después se cambió a un responsable único; el contrato provisional de reparto fue sustituido al implementar la API de entrega. PR #1 ya está integrado; las asignaciones previas no implican dependencias operativas.
