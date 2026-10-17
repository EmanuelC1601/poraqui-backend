# Estado del proyecto

Actualizado: 2026-10-17. `main` es la base de integración y tiene un único responsable. Cada incremento parte del `main` actualizado en una rama `feature/*`, se valida localmente y se entrega en PR. Los contratos, `AppDbContext` y las migraciones tienen un solo propietario. Las asignaciones anteriores a OpenCode quedan como antecedente; no hay dependencia de entregas externas.

## Ledger verificable

| Estado | Capacidad | Evidencia |
|---|---|---|
| Integrado; cobertura funcional parcial | Clean Architecture, Identity, roles y autorización por relación con negocio/pedido; comercios, catálogo y opciones, zonas y tarifas; horario; precios y snapshots calculados en servidor | PR #1 y #22–#33; `BusinessApiTests`; ver pendientes de cobertura de extremo a extremo abajo |
| Integrado; cobertura funcional parcial | Comprobante privado, revisión manual de transferencia, decisión del negocio, preparación, reparto propio/externo, ofertas, recogida con pago del negocio, contacto autorizado, códigos e historial | contratos y recorrido `docs/order-flow.md`; pruebas API con SQLite; faltan pruebas SQL Server aisladas y verificación real de almacenamiento/hosting |
| Integrado; cobertura funcional parcial | Confirmación offline validada por servidor, SignalR con destinatarios autorizados y snapshot al suscribirse, cancelaciones, incidencias, reembolsos manuales y reintentos idempotentes de transiciones e incidencias | PR #22–#28; pruebas API con SQLite; ver ledger de pruebas omitidas |
| Integrado | Idempotencia de `POST /api/orders`, respuesta estable con replay y conflicto 409 por payload distinto | PR #29, merge `36a71fc`; migración aditiva ya integrada, no aplicada por este cambio |
| Integrado | Idempotencia de comprobantes por `commandId` y hash del archivo | PR #30, merge `2a15ea2`; reintentos idénticos devuelven replay y no duplican revisión/archivo |
| Integrado | Idempotencia de asignación de courier propio y persistencia protegida del código de recogida | PR #31, merge `cf3da9f`; prueba API de replay y conflictos |
| Integrado | Pruebas guardadas de concurrencia SQL Server para aceptación simultánea de oferta externa y asignación interna; conflicto concurrente de oferta responde 409 | PR #32, merge `22cec11`; ambos casos requieren la conexión aislada indicada abajo |
| Integrado | `GET /api/admin/metrics/pilot`, limitado a PlatformAdmin; agrega pedidos por intervalo UTC, importe bruto, estados, tiempo medio, incidencias abiertas y reembolsos por revisar | PR #33, merge `bddc081`; prueba de permiso/rango/valores y traducción de consultas comprobada con proveedor SQL Server sin abrir conexión |
| Integrado; verificación externa pendiente | OTP vía Twilio Verify, E.164, confirmación Identity y límite de 5 intentos/10 min/IP | PR #36, merge `0f4dd51`; proveedor cubierto con handler HTTP simulado. Envío real requiere secretos y no se ha comprobado |
| Integrado | Consulta del snapshot persistido al reconectar SignalR | PR #38, merge `a0c3b0f`; prueba con `Microsoft.AspNetCore.SignalR.Client` comprueba nueva conexión, resuscripción y estado/versión actuales |
| Integrado | Guía de publicación MonsterASP.NET | PR #39; documentación preparada, sitio aún no publicado |
| Integrado | Guard de pruebas SQL Server: omite las carreras si el catálogo aislado coincide con `DefaultConnection`, incluso con alias de servidor distintos | PR #41; cinco pruebas unitarias cubren configuración ausente, catálogo compartido, base distinta y cadena inválida |
| Pendiente | Ejecutar recorrido de punta a punta y carreras concurrentes en SQL Server aislado; publicar/verificar en MonsterASP.NET | El recorrido amplio de `BusinessApiTests` usa SQLite; no se ha ejecutado todo el recorrido ni concurrencia sobre SQL Server, ni se ha desplegado |
| Integrado | Reintento de aceptación de oferta externa devuelve el mismo código de recogida protegido y persistido | PR #47, merge `99397d6`; prueba API cubre replay; migración aditiva `ExternalOfferAcceptancePickupCode` aplicada a la BD de desarrollo |
| Bloqueado por acceso remoto/configuración | Ejecutar carreras reales en SQL Server | Se creó una base MSSQL gratuita aislada desde el panel; está vacía. Aún falta `ConnectionStrings__SqlServerTest` en User Secrets y acceso desde la estación externa. Las pruebas nuevas comparan ese destino con `DefaultConnection` y se omiten si coinciden. No se habilitó acceso remoto. |
| Fuera de alcance | GPS, distancias estimadas, transferencias bancarias automáticas y frontend | Se usan zonas/tarifas configuradas y revisión manual |

## Verificaciones de la rama activa

Última verificación de código (commit `3878d11`, integrado por PR #47 como merge `99397d6`): `dotnet test Poraqui.sln` aprobó 31 pruebas y omitió 2 carreras SQL porque falta `ConnectionStrings__SqlServerTest`; `dotnet build Poraqui.sln --no-restore` terminó con cero advertencias y errores; `git diff --check` pasó; `dotnet ef migrations has-pending-model-changes` informó que el snapshot coincide. Las carreras todavía no se han ejecutado contra SQL Server.

PR #43 (`6063058`) valida que courier externo active disponibilidad solo en zona existente y activa; prueba API SQLite cubre zona inexistente.

PR #44 (`099c3ad`) añade un proceso hospedado que barre ofertas vencidas cada minuto y persiste el estado `Expired`; prueba SQLite confirma que solo se actualizan las ofertas debidas.

PR #45 (`2988a00`) añade grupo SignalR privado por courier para ofertas externas; el cliente suscrito recibe la oferta después de persistir y obtiene snapshot actual al suscribirse.

PR #46 (`f53f627`, commit de trabajo `e86947d`) integra eventos SignalR de cambio para aceptación, rechazo, cancelación y vencimiento después de persistir; aceptar una oferta cancela las restantes del pedido. Pruebas cubren cancelación sincronizada para dos couriers y cancelación al aceptar. Este cambio no añade esquema ni migraciones.

PR #47 (`99397d6`, commit de trabajo `3878d11`) integra replay de aceptación de oferta externa y su código protegido. La migración `ExternalOfferAcceptancePickupCode` añade únicamente una columna nullable; fue revisada antes de aplicación y aplicada a la BD de desarrollo el 2026-10-17. `dotnet ef migrations list` volvió a confirmar las 13 migraciones integradas aplicadas.

## SQL Server privado

`ConnectionStrings:DefaultConnection` existe en User Secrets. El 2026-10-17, `dotnet ef migrations list` conectó en modo de consulta y devolvió las 12 migraciones de desarrollo aplicadas. No se ejecutó ninguna migración sobre esa BD. Se creó una segunda base MSSQL gratuita en el panel como destino aislado de pruebas; está habilitada, aún sin esquema ni datos. El proveedor indica que su acceso Local solo funciona desde sitios alojados; el acceso Remote de esta nueva base continúa deshabilitado. Para ejecutar pruebas desde el equipo, falta aprobar/habilitar ese acceso, obtener la cadena Remote y guardarla localmente como `ConnectionStrings__SqlServerTest`. El guard de pruebas rechaza cualquier test catalog que tenga el mismo nombre que el catálogo configurado en `DefaultConnection`. La API nunca migra al iniciar. Antes de una futura aplicación en la BD compartida, revisa el SQL generado y concilia el historial; no usar pruebas concurrentes ni limpieza en esa BD.

La migración `ExternalOfferAcceptancePickupCode` fue aplicada a la BD de desarrollo el 2026-10-17 después de integrar PR #47. El historial de solo lectura posterior devolvió las 13 migraciones integradas sin pendientes. El SQL aplicado agregó `ProtectedPickupCode nvarchar(2000) NULL`; no eliminó ni reconstruyó datos. Las carreras concurrentes continúan bloqueadas hasta configurar una conexión privada distinta a la base compartida.

El proveedor Twilio Verify requiere `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid` y `TwilioVerify:ApiKeySecret` en configuración privada. No están configurados para esta ejecución y no se envió SMS real.
