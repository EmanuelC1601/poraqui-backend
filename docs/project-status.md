# Estado del proyecto

Actualizado: 2026-10-17. `main` es la base de integración y tiene un único responsable. Cada incremento parte del `main` actualizado en una rama `feature/*`, se valida localmente y se entrega en PR. Los contratos, `AppDbContext` y las migraciones tienen un solo propietario. Las asignaciones anteriores a OpenCode quedan como antecedente; no hay dependencia de entregas externas.

## Ledger verificable

| Estado | Capacidad | Evidencia |
|---|---|---|
| Integrado | Clean Architecture, Identity, roles y autorización por relación con negocio/pedido; comercios, catálogo y opciones, zonas y tarifas; horario; precios y snapshots calculados en servidor | `main` en `7216c23`, PR #1 y #22–#28; `BusinessApiTests` |
| Integrado | Comprobante privado, revisión manual de transferencia, decisión del negocio, preparación, reparto propio/externo, ofertas, recogida con pago del negocio, contacto autorizado, códigos e historial | contratos y recorrido `docs/order-flow.md`; pruebas SQLite API |
| Integrado | Confirmación offline validada por servidor, SignalR con destinatarios autorizados y snapshot al suscribirse, cancelaciones, incidencias, reembolsos manuales y reintentos idempotentes de transiciones e incidencias | PR #22–#28; pruebas SQLite API |
| En rama | Idempotencia de `POST /api/orders`, persistencia protegida del código de entrega para repetir la respuesta; migración revisable | `feature/order-creation-idempotency`, cherry-pick `bc73e9a` adaptado sobre `7216c23`; build y 21/21 pruebas SQLite pasan |
| Pendiente | Idempotencia de carga de comprobante y asignación; OTP con proveedor real; métricas piloto; pruebas SQL Server aisladas de migraciones/índices/concurrencia; reconexión real en cliente; despliegue MonsterASP.NET | Sin integrar. `ConnectionStrings:SqlServerTest` no está configurada |
| Bloqueado por configuración | Aplicar de forma segura toda la cadena de migraciones a SQL Server compartido y correr pruebas de concurrencia sobre SQL Server aislado | Historial compartido contiene migraciones registradas de incrementos que aún no están todos reconciliados en `main`; revisar cadena antes de aplicar. No se ejecutaron migraciones |
| Fuera de alcance | GPS, distancias estimadas, transferencias bancarias automáticas y frontend | Se usan zonas/tarifas configuradas y revisión manual |

## Verificaciones de la rama activa

`dotnet restore Poraqui.sln`, `dotnet build Poraqui.sln --no-restore` (0 advertencias, 0 errores), `dotnet test Poraqui.sln --no-build` (21/21; SQLite en memoria y archivos temporales) y `dotnet ef migrations has-pending-model-changes` (sin cambios sin migración). `git diff --check` limpio. La migración de idempotencia crea tabla, FK e índices de forma aditiva; todavía no se ha aplicado.

## SQL Server privado

`ConnectionStrings:DefaultConnection` existe en User Secrets y una lectura de solo consulta confirmó conexión al servidor y acceso al historial; no imprimir la cadena. `ConnectionStrings:SqlServerTest` falta. Configúrala como secreto local en `src/Poraqui.Api` y apunta exclusivamente a una BD aislada para pruebas de integración. La API nunca migra al iniciar. Revisar y conciliar el historial completo antes de aplicar nuevas migraciones a la BD compartida.
