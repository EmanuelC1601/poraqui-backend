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
| Integrado | Idempotencia de asignación de courier propio y persistencia protegida del código de recogida | PR #31, merge `cf3da9f`; prueba API de replay y conflictos |
| En rama | Pruebas de concurrencia SQL Server para aceptación simultánea de oferta externa y asignación interna; DbUpdateException de oferta concurrente se traduce a 409 | `feature/sqlserver-concurrency-tests-main`, commits `5a130e6` y `ab4d874`; requiere base aislada `PoraquiIntegrationTests` |
| Pendiente | OTP con proveedor real; métricas piloto integradas; aplicar migraciones en SQL Server compartido tras reconciliar historial; ejecutar pruebas SQL de concurrencia; prueba de reconexión de SignalR con cliente real; despliegue MonsterASP.NET | No integrado o no verificado en SQL Server |
| Bloqueado por configuración | Ejecutar carreras y verificar índices/restricciones/migraciones con SQL Server | Falta `ConnectionStrings__SqlServerTest` apuntando a la base aislada `PoraquiIntegrationTests`; no se ejecutaron migraciones |
| Fuera de alcance | GPS, distancias estimadas, transferencias bancarias automáticas y frontend | Se usan zonas/tarifas configuradas y revisión manual |

## Verificaciones de la rama activa

PR #29–#31: restore/build (0 advertencias/errores), 21 pruebas SQLite correctas, OpenAPI local y sin cambios pendientes del modelo. En la rama activa se ejecutaron 23 casos: 21 correctos y 2 omitidos por no estar configurada la conexión SQL Server aislada. Los casos de concurrencia todavía no se han ejecutado contra SQL Server.

## SQL Server privado

`ConnectionStrings:DefaultConnection` existe en User Secrets y una lectura de solo consulta confirmó conexión al servidor y acceso al historial; no imprimir la cadena. `ConnectionStrings:SqlServerTest` falta. Para ejecutar pruebas SQL, configura la variable privada `ConnectionStrings__SqlServerTest` y apunta exclusivamente al catálogo aislado `PoraquiIntegrationTests`. La API nunca migra al iniciar. Revisar y conciliar el historial completo antes de aplicar nuevas migraciones a la BD compartida.
