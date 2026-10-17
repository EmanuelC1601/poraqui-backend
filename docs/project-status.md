# Estado del proyecto

Actualizado: 2026-10-17. `main` es la base de integración y tiene un único responsable. Cada incremento parte del `main` actualizado en una rama `feature/*`, se valida localmente y se entrega en PR. Los contratos, `AppDbContext` y las migraciones tienen un solo propietario. Las asignaciones anteriores a OpenCode quedan como antecedente; no hay dependencia de entregas externas.

## Ledger verificable

| Estado | Capacidad | Evidencia |
|---|---|---|
| Integrado | Clean Architecture, Identity, roles y autorización por relación con negocio/pedido; comercios, catálogo y opciones, zonas y tarifas; horario; precios y snapshots calculados en servidor | `main` en `2a15ea2`, PR #1 y #22–#30; `BusinessApiTests` |
| Integrado | Comprobante privado, revisión manual de transferencia, decisión del negocio, preparación, reparto propio/externo, ofertas, recogida con pago del negocio, contacto autorizado, códigos e historial | contratos y recorrido `docs/order-flow.md`; pruebas SQLite API |
| Integrado | Confirmación offline validada por servidor, SignalR con destinatarios autorizados y snapshot al suscribirse, cancelaciones, incidencias, reembolsos manuales y reintentos idempotentes de transiciones e incidencias | PR #22–#28; pruebas SQLite API |
| Integrado | Idempotencia de `POST /api/orders`, respuesta estable con replay y conflicto 409 por payload distinto | PR #29, merge `36a71fc`; migración aditiva ya integrada, no aplicada por este cambio |
| Integrado | Idempotencia de comprobantes por `commandId` y hash del archivo | PR #30, merge `2a15ea2`; reintentos idénticos devuelven replay y no duplican revisión/archivo |
| En rama | Idempotencia de asignación de courier propio y persistencia protegida del código de recogida | `feature/courier-assignment-idempotency-main`, cherry-pick `12f0d97` adaptado sobre `2a15ea2`; verificación en curso |
| Pendiente | Idempotencia de asignación de courier; OTP con proveedor real; métricas piloto; pruebas SQL Server aisladas de migraciones/índices/concurrencia; reconexión real en cliente; despliegue MonsterASP.NET | Sin integrar. `ConnectionStrings:SqlServerTest` no está configurada |
| Bloqueado por configuración | Aplicar de forma segura toda la cadena de migraciones a SQL Server compartido y correr pruebas de concurrencia sobre SQL Server aislado | Historial compartido contiene migraciones registradas de incrementos que aún no están todos reconciliados en `main`; revisar cadena antes de aplicar. No se ejecutaron migraciones |
| Fuera de alcance | GPS, distancias estimadas, transferencias bancarias automáticas y frontend | Se usan zonas/tarifas configuradas y revisión manual |

## Verificaciones de la rama activa

PR #29 y #30: restore/build (0 advertencias/errores), 21/21 pruebas SQLite, OpenAPI local y sin cambios pendientes del modelo. Rama activa de asignación: restore/build (0 advertencias/errores), 21/21 pruebas SQLite; replay/conflicto incluidos. Falta carrera SQL Server aislada.

## SQL Server privado

`ConnectionStrings:DefaultConnection` existe en User Secrets y una lectura de solo consulta confirmó conexión al servidor y acceso al historial; no imprimir la cadena. `ConnectionStrings:SqlServerTest` falta. Configúrala como secreto local en `src/Poraqui.Api` y apunta exclusivamente a una BD aislada para pruebas de integración. La API nunca migra al iniciar. Revisar y conciliar el historial completo antes de aplicar nuevas migraciones a la BD compartida.
