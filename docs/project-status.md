# Estado del proyecto

Actualizado: 2026-10-17. `main` es la base de integración y tiene un único responsable. Cada incremento parte del `main` actualizado en una rama `feature/*`, se valida localmente y se entrega en PR. Los contratos, `AppDbContext` y las migraciones tienen un solo propietario. Las asignaciones anteriores a OpenCode quedan como antecedente; no hay dependencia de entregas externas.

## Ledger verificable

| Estado | Capacidad | Evidencia |
|---|---|---|
| Integrado | Clean Architecture, Identity, roles y autorización por relación con negocio/pedido; comercios, catálogo y opciones, zonas y tarifas; horario; precios y snapshots calculados en servidor | `main` en `bddc081`, PR #1 y #22–#33; `BusinessApiTests` |
| Integrado | Comprobante privado, revisión manual de transferencia, decisión del negocio, preparación, reparto propio/externo, ofertas, recogida con pago del negocio, contacto autorizado, códigos e historial | contratos y recorrido `docs/order-flow.md`; pruebas SQLite API |
| Integrado | Confirmación offline validada por servidor, SignalR con destinatarios autorizados y snapshot al suscribirse, cancelaciones, incidencias, reembolsos manuales y reintentos idempotentes de transiciones e incidencias | PR #22–#28; pruebas SQLite API |
| Integrado | Idempotencia de `POST /api/orders`, respuesta estable con replay y conflicto 409 por payload distinto | PR #29, merge `36a71fc`; migración aditiva ya integrada, no aplicada por este cambio |
| Integrado | Idempotencia de comprobantes por `commandId` y hash del archivo | PR #30, merge `2a15ea2`; reintentos idénticos devuelven replay y no duplican revisión/archivo |
| Integrado | Idempotencia de asignación de courier propio y persistencia protegida del código de recogida | PR #31, merge `cf3da9f`; prueba API de replay y conflictos |
| Integrado | Pruebas guardadas de concurrencia SQL Server para aceptación simultánea de oferta externa y asignación interna; conflicto concurrente de oferta responde 409 | PR #32, merge `22cec11`; ambos casos requieren la conexión aislada indicada abajo |
| Integrado | `GET /api/admin/metrics/pilot`, limitado a PlatformAdmin; agrega pedidos por intervalo UTC, importe bruto, estados, tiempo medio, incidencias abiertas y reembolsos por revisar | PR #33, merge `bddc081`; prueba de permiso/rango/valores y traducción de consultas comprobada con proveedor SQL Server sin abrir conexión |
| Integrado; verificación externa pendiente | OTP vía Twilio Verify, E.164, confirmación Identity y límite de 5 intentos/10 min/IP | PR #36, merge `0f4dd51`; proveedor cubierto con handler HTTP simulado. Envío real requiere secretos y no se ha comprobado |
| Integrado | Consulta del snapshot persistido al reconectar SignalR | Prueba con `Microsoft.AspNetCore.SignalR.Client`: nueva conexión vuelve a suscribirse y recibe estado/versión actuales |
| Pendiente | Ejecutar carreras SQL de concurrencia; publicar/verificar en MonsterASP.NET | No verificado en SQL Server o no desplegado; guía de hosting en `docs/deployment-monsterasp.md` |
| Bloqueado por configuración | Ejecutar carreras reales en SQL Server | Falta crear/configurar `ConnectionStrings__SqlServerTest` apuntando a una base aislada `PoraquiIntegrationTests`; las pruebas se omiten mientras tanto |
| Fuera de alcance | GPS, distancias estimadas, transferencias bancarias automáticas y frontend | Se usan zonas/tarifas configuradas y revisión manual |

## Verificaciones de la rama activa

PR #29–#36 integrados y rama activa SignalR: restore/build, pruebas SQLite y OpenAPI. PR #36 tuvo 26 casos, 24 correctos y 2 omitidos; la rama SignalR añade cliente oficial y comprobación de snapshot tras nueva conexión. Las carreras todavía no se han ejecutado contra SQL Server.

## SQL Server privado

`ConnectionStrings:DefaultConnection` existe en User Secrets. El 2026-10-17, `dotnet ef migrations list` conectó en modo de consulta y devolvió las 12 migraciones de `main`, sin entradas pendientes. Ninguna migración se ejecutó durante esta revisión. `ConnectionStrings:SqlServerTest` falta; configura esa clave privada exclusivamente para una BD aislada cuyo catálogo sea `PoraquiIntegrationTests`. La API nunca migra al iniciar. Antes de una futura aplicación en la BD compartida, revisa el SQL generado y concilia el historial; no usar pruebas concurrentes ni limpieza en esa BD.

El proveedor Twilio Verify requiere `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid` y `TwilioVerify:ApiKeySecret` en configuración privada. No están configurados para esta ejecución y no se envió SMS real.
