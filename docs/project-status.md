# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` es la base de integración y contiene PR #1–#6 (`d39d265`). PR #7–#21 están integrados en ramas de trabajo apiladas, todavía fuera de `main`. La rama `feature/backend-integration-verified` parte de la punta de esa pila (`origin/feature/courier-assignment-idempotency`, merge de PR #21 `236fd19`) y reúne los cambios publicados de la plataforma para su revisión integral. Hay un solo responsable; OpenCode ya no participa.

## Ledger de funciones

| Estado | Función | Evidencia / siguiente paso |
|---|---|---|
| Implementada en `main` | Arquitectura .NET, Identity/roles, permisos de negocio, catálogo básico, zonas/tarifas, pedidos con precio de servidor, transferencia manual, flujo de pedido y reparto propio/externo inicial | PR #1–#6; `main` en `d39d265` |
| Implementada en la pila de integración | Opciones/disponibilidad de catálogo, horarios por zona horaria, cancelación, incidencias, reembolsos manuales y métricas | PR #7–#12; ramas apiladas sobre `main` |
| Implementada en la pila de integración | Sincronización offline validada en servidor, SignalR con autorización y estado persistido, reintentos idempotentes de transiciones | PR #7, #8, #13, #15 |
| Implementada en la pila de integración | OTP telefónico con Twilio Verify, idempotencia durable de incidencias/creación de pedido/comprobante/asignación de courier y protección de códigos | PR #14, #18–#21; migraciones aplicadas según EF |
| Pendiente | Integrar la pila verificada en `main` mediante PR y revisión final | PR de integración por crear desde `feature/backend-integration-verified` |
| Pendiente | Pruebas SQL Server de aceptación concurrente externa y asignación concurrente propia | Requieren `ConnectionStrings__SqlServerTest` hacia una base aislada `PoraquiIntegrationTests`; no se ejecutaron porque esa clave no existe |
| Pendiente | Probar reconexión SignalR con un cliente real, SMS con Twilio real y publicación MonsterASP.NET con almacenamiento persistente | Configuración externa aún no validada |
| Fuera de alcance | GPS/distancias calculadas, transferencias bancarias automáticas y frontend | Usar zonas y tarifas configuradas; reembolso manual |

## Verificación de `feature/backend-integration-verified`

- `dotnet restore Poraqui.sln`: correcto.
- `dotnet build Poraqui.sln --no-restore`: correcto, 0 advertencias y 0 errores (.NET 10).
- `dotnet test Poraqui.sln --no-build --no-restore`: 24 aprobadas, 2 omitidas de un total de 26. Las omitidas son las carreras de aceptación de oferta y asignación de courier que requieren SQL Server aislado; el recorrido funcional usa SQLite aislado.
- EF se conectó a SQL Server de desarrollo mediante `ConnectionStrings:DefaultConnection` privada. La lista confirmó las 12 migraciones aplicadas hasta `CourierAssignmentCommandIdempotency`; `migrations has-pending-model-changes` confirmó que el modelo coincide con la última migración. Esta revisión no aplicó cambios de esquema.
- `ConnectionStrings:SqlServerTest` falta. No ejecutar las pruebas que limpian GUIDs de prueba sobre la BD compartida.
- La API no ejecuta migraciones automáticamente al iniciar. No hay credenciales en el repositorio.

## Migraciones y seguridad de datos

Las doce migraciones disponibles se enumeran en [api-contracts.md](api-contracts.md). SQL Server reporta todas aplicadas en el entorno de desarrollo. No se hicieron borrados, recreaciones ni reinicios en esta verificación. Las migraciones futuras deben revisarse por operaciones destructivas, generar SQL revisable y aplicarse explícitamente solo después de comprobar el estado de destino.

La cadena privada local está en User Secrets bajo `ConnectionStrings:DefaultConnection`. Para otra copia, configura esa clave en User Secrets para `src/Poraqui.Api` o en el proveedor privado del hosting. No copiar credenciales del chat ni guardarlas en Git.
