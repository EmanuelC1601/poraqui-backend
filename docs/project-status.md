# Estado del proyecto

Actualizado: 2026-10-17. `main` es la base de integración, con un solo responsable. Los incrementos salen de `main` en ramas `feature/*` y se integran mediante PR. Contratos persistidos, `AppDbContext` y migraciones tienen un propietario único. La antigua asignación de OpenCode queda solo como antecedente; no hay dependencia operativa.

## Ledger verificable

| Estado | Capacidad | Evidencia / límite |
|---|---|---|
| Integrado; cobertura funcional parcial | Clean Architecture, Identity, roles y autorización por relación con negocio/pedido; comercios, catálogo y opciones, zonas, tarifas, horarios y precios calculados por servidor | PR #1, #22–#33; prueba API integral sobre SQLite; ver requisitos SQL y hosting pendientes |
| Integrado; cobertura funcional parcial | Pedido, comprobante privado, revisión manual de transferencia, decisión del negocio, preparación, repartidor propio/externo, ofertas, códigos, contacto autorizado e historial | Prueba `BusinessApiTests.Manual_payment_and_internal_delivery_flow_preserves_server_pricing_and_history`; `docs/order-flow.md` describe su ejecución y límites |
| Integrado; cobertura funcional parcial | Sincronización offline validada por servidor, SignalR autorizado con snapshot actual, cancelaciones, incidencias, reembolsos manuales e idempotencia | Prueba integral SQLite; las notificaciones de WhatsApp/llamada son datos de contacto para acción humana, no mensajes automáticos |
| Integrado | Idempotencia de pedido, comprobante, asignación interna, incidentes y aceptación de oferta externa; códigos protegidos persistidos | PR #29–#31 y #47; replay retorna resultado estable y payload diferente produce conflicto |
| Integrado; validación SQL pendiente | Restricciones y control optimista para limitar la aceptación/asignación concurrente a un courier | PR #32; pruebas SQL Server existentes y aisladas, pero no ejecutadas porque falta la cadena `ConnectionStrings__SqlServerTest` |
| Integrado | Ofertas externas por zona, rechazo, expiración, cancelación, courier ausente y SignalR privado tras persistir | PR #43–#46; pruebas API con SQLite |
| Integrado | `GET /api/admin/metrics/pilot`, restringido a PlatformAdmin | PR #33; permisos, límites de rango y valores agregados probados; query traducido por provider SQL Server sin conexión real |
| Integrado; SMS real pendiente | OTP Twilio Verify, E.164, confirmación Identity y límite de 5 intentos/10 min/IP | PR #36; pruebas HTTP simuladas. Requiere secretos Twilio privados para comprobar entrega real |
| Integrado | Snapshots SignalR al reconectar y ofertas actuales al suscribirse | PR #38 y #45–#46; reconexión y destinatarios cubiertos por pruebas |
| Integrado | Guía de despliegue MonsterASP.NET y recorrido reproducible | PR #39 y #49–#50; aún no hay publicación ni verificación de hosting |
| Integrado; migraciones aplicadas a desarrollo | 13 migraciones EF integradas, incluida `ExternalOfferAcceptancePickupCode` | `dotnet-ef migrations list` conectado en lectura el 2026-10-17 mostró 13 migraciones, ninguna pendiente. La más reciente solo añadió `ProtectedPickupCode nvarchar(2000) NULL` |
| Integrado | Actualización del target a .NET 10 LTS y EF Core 10 | PR #51, merge `5078005`; no cambia el modelo ni genera migraciones |
| Pendiente | Ejecutar dos pruebas de concurrencia contra SQL Server aislado | Crear/configurar `ConnectionStrings__SqlServerTest` en User Secrets y habilitar acceso Remote únicamente en la base aislada de pruebas; no apuntar a la BD compartida |
| Pendiente | Publicar y comprobar API, SignalR y almacenamiento privado en MonsterASP.NET | Se necesita sitio destino, variables privadas de producción y ruta persistente autorizada |
| Pendiente | Verificar OTP Twilio extremo a extremo | Se necesitan `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid`, `TwilioVerify:ApiKeySecret` en configuración privada y un teléfono de prueba autorizado |
| Fuera de alcance | GPS, distancias inferidas, transferencias bancarias automáticas y frontend | Se usan zonas/tarifas y revisión/pago manual |

## Verificaciones de .NET 10 integradas por PR #51

Commit `05d5410`, integrado en `main` como `5078005`:

- `dotnet build Poraqui.sln --no-restore` con SDK local 10.0.402: 0 advertencias, 0 errores.
- `dotnet test Poraqui.sln --no-restore`: 31 pasaron; 2 pruebas SQL Server omitidas por falta de configuración aislada.
- `dotnet list Poraqui.sln package --vulnerable --include-transitive`: ningún paquete vulnerable en los cinco proyectos.
- `dotnet-ef migrations has-pending-model-changes`: no hay cambios de modelo.
- `dotnet-ef migrations list`: conexión privada de desarrollo comprobada en lectura; 13 migraciones y ninguna pendiente.

Los tests API usan SQLite en memoria y almacenamiento temporal. No prueban SQL Server real, Twilio ni el entorno MonsterASP.NET. La API no aplica migraciones al iniciar. Antes de aplicar cambios de esquema, revisar SQL generado y el historial de destino; nunca ejecutar limpiezas ni recreaciones sobre la base compartida.

## Configuración privada requerida para la prueba SQL

`ConnectionStrings__SqlServerTest` debe estar presente como variable de entorno del proceso `dotnet test` y contener la cadena Remote de una base aislada. La prueba lee `ConnectionStrings:DefaultConnection` desde User Secrets y omite las carreras si falta la cadena aislada, falta la conexión privada compartida para comparar catálogos, o ambos destinos usan la misma base. No se habilitó Remote ni se guardaron credenciales en el repositorio. Configura la variable solo en el entorno local privado desde el que ejecutarás los tests; no añadas el valor a archivos versionados.

Verificación repetida tras integrar PR #52 (`edac6bf`): SDK local 10.0.402, compilación con 0 advertencias/errores, 31 tests aprobados y las dos carreras SQL omitidas porque falta `ConnectionStrings__SqlServerTest`. `dotnet-ef migrations list` conectó en lectura a la BD de desarrollo privada y mostró las 13 migraciones integradas sin pendientes. No se ejecutaron pruebas mutables ni migraciones en esta comprobación.
