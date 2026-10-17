# Flujo de desarrollo

## Responsable

Una persona mantiene todos los módulos: identidad y permisos, comercio, pedidos, pagos manuales, reparto propio y externo, notificaciones, sincronización, métricas, infraestructura y despliegue. Esa persona es también la única responsable de `AppDbContext`, las migraciones y su aplicación a las bases de desarrollo y producción. OpenCode ya no participa; su asignación anterior a reparto se conserva como antecedente y no es un bloqueo.

## Archivos y contratos

- `src/Poraqui.Domain/Orders/OrderStatus.cs` y `src/Poraqui.Application/Delivery/DeliveryOrderContracts.cs` son contratos provisionales; mantenerlos alineados con la máquina de estados y casos de uso implementados.
- `src/Poraqui.Infrastructure/Persistence/AppDbContext.cs`, configuración de DI y `src/Poraqui.Infrastructure/Persistence/Migrations/` pertenecen al responsable único del backend.
- `docs/api-contracts.md` y `docs/project-status.md` son la fuente de estado actual y contratos.

## Ramas y base de datos

`main` es la base de integración. Trabajar en incrementos pequeños `feature/*`, con PR revisado, `dotnet build Poraqui.sln`, `dotnet test Poraqui.sln` y los chequeos SQL Server pertinentes antes de integrar. Actualizar `docs/project-status.md` y `docs/handoff-codex.md` por incremento. PR #1 está integrado; el ledger detalla qué tiene verificaciones y qué no.

Las pruebas que creen o limpien datos usan bases aisladas. La BD de desarrollo puede recibir solo registros identificables de prueba. No borrar ni recrear datos ni reiniciar la BD compartida. No incluir credenciales en Git; usar User Secrets localmente y la configuración privada del hosting en despliegue. La API no aplica migraciones al arrancar.

## Antecedentes

El plan inicial contempló dos agentes, con el módulo de reparto en OpenCode. El equipo cambió a un responsable único antes de integrar el PR #1. Los contratos preliminares de reparto se conservan como referencia, pero no hay implementación que dependa de otra persona.
