# Estado verificable del proyecto

PR #17 (https://github.com/EmanuelC1601/poraqui-backend/pull/17) se integró en `feature/phone-verification` mediante `0f31ed4`; actualizó la pila a .NET 10 / EF Core 10.0.13. PR #18 (https://github.com/EmanuelC1601/poraqui-backend/pull/18), rama `feature/command-idempotency-increment` apilada sobre `feature/phone-verification`, agrega reintento durable para reportes de incidencias y su migración, aplicada a desarrollo. PR #19 (https://github.com/EmanuelC1601/poraqui-backend/pull/19), rama `feature/order-create-idempotency` apilada sobre PR #18, hace idempotente `POST /api/orders` y añade `20261017211445_OrderCreationCommandIdempotency`, que crea una tabla para claves/resultado protegido y se aplicó explícitamente a desarrollo. Commit `bc73e9a`. Build: 0 advertencias/errores; suite: 24 aprobadas y 1 optativa omitida porque falta la base aislada `ConnectionStrings__SqlServerTest`; diez migraciones aplicadas. No se probó aceptación concurrente contra SQL Server. No se subió configuración privada.

Actualizado: 2026-10-17. `main` contiene PR #1–#5 hasta `d677167` y es la base de integración. PR #6 (reparto externo), #7 (sincronización offline), #8 (SignalR), #9 (opciones de catálogo), #10 (horarios) y #11 (cancelaciones/incidencias/reembolsos) están abiertos y apilados en ese orden. PR #12 (métricas) fue integrado en la rama de PR #11 mediante `d04882a`; no está todavía en `main`. PR #17 (.NET 10) se integró en `feature/phone-verification`, rama que contiene la pila funcional y sigue fuera de `main`. La BD SQL Server de desarrollo tiene diez migraciones aplicadas hasta `OrderCreationCommandIdempotency`. El desarrollo tiene un único responsable; las asignaciones previas a OpenCode se conservan como antecedente, sin dependencia operativa.

## Ledger de funciones

Estado de la pila local `feature/order-create-idempotency` (base PR #18); no confundir con lo integrado en `main`:

- **Implementado en código y suite local:** autenticación Identity, roles y autorización por recurso; negocio, catálogo, opciones, disponibilidad, zonas y tarifas; precios calculados en servidor; transferencia y comprobantes privados con revisión manual; decisiones, preparación explícita, reparto propio con códigos, pago al courier al recoger, contacto autorizado, estados e historial; reparto externo con ofertas/cancelación/vencimiento; SignalR con autorización y estado persistido al suscribirse; confirmación offline validada por el servidor; incidencias, cancelación y seguimiento manual de reembolso; métricas; reintentos idempotentes de transiciones, incidencias y creación de pedidos.
- **Pendiente:** idempotencia durable de carga/reintento de comprobantes y asignación propia de courier con replay seguro del código; carrera de aceptación de oferta sobre SQL Server aislado; OTP/SMS real; reconexión SignalR desde cliente real; publicar e integrar la pila; comprobar publicación MonsterASP con persistencia de Data Protection y almacenamiento privado.
- **Bloqueado por configuración:** prueba SQL Server de concurrencia, porque falta `ConnectionStrings__SqlServerTest` hacia la BD aislada `PoraquiIntegrationTests`. No se usa la BD compartida para tests que limpian datos. Para despliegue, falta una ruta persistente privada `DataProtection__KeysPath`.

## Implementado en `main`

- Solución .NET 9 compilable en `main`; la rama `feature/net10-lts` actualiza Domain, Application, Infrastructure y Api a .NET 10 / EF Core 10.
- Identity API, roles y policies; negocio con permisos por propietario/recurso.
- Catálogo básico, zonas, tarifas y pedido con importes calculados por el servidor.
- Datos de transferencia del negocio y snapshot por pedido; comprobante privado y revisión manual.
- Decisión del negocio, preparación explícita, repartidor propio, códigos de recogida/recepción, registro separado del pago al courier, contacto autorizado y consulta de estado/historial.

## PR #6 abierto: reparto externo

- Perfil externo administrado por admin, disponibilidad por zona, publicar/ver/rechazar/cancelar ofertas, vencimiento y respuesta `NoCouriersAvailable`.
- La oferta muestra origen, destino, zona, condiciones y remuneración. Reintentos de publicación no generan ofertas duplicadas mientras exista una abierta.
- Aceptar persiste oferta y asignación; `Order.Version`, `ExternalDeliveryOffer.Version` e índice filtrado único protegen frente a duplicidad.
- Build/test locales pasan en SQLite; migración de tablas/índices y script SQL están en PR. SQL Server y la aceptación simultánea real aún no se han verificado.

## Rama actual: sincronización offline

- `POST /api/orders/{orderId}/sync/delivery-confirmation` recibe ID idempotente, código y hora local de captura.
- El servidor valida courier asignado, estado y código antes de aceptar. Hora del servidor es la de aceptación; la hora del dispositivo solo es auditoría.
- Reintento igual reproduce la aceptación guardada sin duplicar historial; mismo ID con payload/orden distinto da conflicto. Código inválido no consume el ID.
- Registro idempotente y transición de pedido se guardan en la misma transacción; la migración agrega una tabla nueva.

## PR #8 abierto: SignalR de pedidos

- Hub autenticado `/hubs/orders`; la negociación acepta `access_token` únicamente en la ruta del hub.
- `SubscribeOrder` comprueba la relación de usuario con el pedido y devuelve una instantánea persistida con estado y versión. Los eventos `OrderChanged` salen después del guardado, solo a los grupos autorizados por pedido y sin datos personales.
- Al reconectar, el cliente vuelve a suscribirse y recibe el estado vigente. El fallo de publicación se registra y no cambia la respuesta de una transición ya persistida.

## PR #9 abierto: opciones del catálogo

- Grupos de opciones obligatorios/opcionales con límite mínimo/máximo, diferencias no negativas de precio y disponibilidad por grupo/variante.
- El cliente elige IDs de opciones; el servidor valida pertenencia/disponibilidad y calcula cada precio unitario. Pedido e historial de consulta conservan snapshots de nombres/diferencias.
- Migración `CatalogProductOptions` y `docs/sql/CatalogProductOptions.sql` crean tres tablas e índices únicos. El script de aplicación es aditivo; no se aplicó.

## Rama actual: horarios del negocio

- Dueño/admin configura los siete días y una zona horaria; consulta pública devuelve horario y si acepta pedidos ahora.
- La creación valida la hora local del negocio. El comportamiento previo sigue hasta que el comercio configura su horario.
- Migración aditiva `BusinessOpeningHours` y script `docs/sql/BusinessOpeningHours.sql`; aplicada en la BD de desarrollo. Pruebas nuevas cubren horario local, cierre, rango y permisos entre negocios; la ruta de horario aún no se ha ejecutado contra SQL Server.

## PR #11 abierto: cancelaciones, incidencias y reembolsos manuales

Rama `feature/order-cancellations-refunds-incidents`, commit `1347648`, PR https://github.com/EmanuelC1601/poraqui-backend/pull/11, basado en PR #10.

- Cliente cancela solo antes de enviar comprobante; negocio/admin puede cancelar antes de asignar courier. Las ofertas externas abiertas se cancelan junto con el pedido. La repetición del mismo comando de cancelación no duplica historial.
- Si hay evidencia de transferencia enviada o el pago fue aprobado, se crea un caso de revisión manual. Rechazar un pedido con transferencia aprobada también crea el caso. El negocio registra si reembolsó manualmente, el importe y referencia; el cliente ve solo el estado del caso.
- Participantes autorizados informan y consultan incidencias; negocio/admin las resuelve. Incidencias y reembolsos quedan en historial/versionado del pedido y notifican después de persistir.
- Migraciones `OrderCancellationRefundsIncidents` y `OrderCancellationAuditConstraints`, script `docs/sql/OrderCancellationRefundsIncidents.sql`; aplicadas en la BD de desarrollo. La alteración de `CancellationReason` solo afecta a la columna nullable creada en la migración anterior; al aplicar el conjunto, la BD estaba vacía.

## PR #12 integrado en la rama de PR #11: métricas del piloto

- Rama `feature/pilot-metrics`, PR https://github.com/EmanuelC1601/poraqui-backend/pull/12, integrada en `feature/order-cancellations-refunds-incidents` mediante `d04882a`. Añade `GET /api/admin/metrics/pilot`, con permiso exclusivo de admin, filtro UTC de hasta 366 días y datos agregados sin información personal. Resume pedidos por estado, valor total, entregas, cancelaciones, incidencias abiertas, reembolsos pendientes y tiempo medio de entrega. Sigue pendiente de integración en `main` junto con la cadena anterior.
- No requiere migración. El endpoint se prueba en SQLite; queda pendiente revisar el SQL generado y el comportamiento en SQL Server cuando exista una conexión remota accesible para la aplicación.

## Verificaciones actuales

- Build: `dotnet build Poraqui.sln`, 0 advertencias y 0 errores.
- Pruebas: `dotnet test Poraqui.sln`, 19/19: 18 de recorrido API con SQLite en memoria/archivos temporales y una prueba de traducción SQL Server que no abre conexión. El recorrido verifica horarios, opciones, permisos entre negocios, importes, cancelación/refund manual, incidencias, métricas, snapshots, SignalR y entrega persistida.
- Scripts `ExternalCourierOffers`, `OfflineDeliveryCommandSync`, `CatalogProductOptions`, `BusinessOpeningHours` y `OrderCancellationRefundsIncidents` están disponibles para revisión; la última migración agrega `OrderCancellationAuditConstraints`. No hay cambios pendientes del modelo.
- Las diez migraciones hasta `OrderCreationCommandIdempotency` se aplicaron a la BD de desarrollo. Los scripts de nuevas tablas/campos/índices se revisaron antes de aplicar; no se ejecutaron `DROP`, `DELETE FROM` ni `TRUNCATE TABLE`. La prueba confirma migración de esquema SQL Server, no comportamiento concurrente.
- `dotnet-ef migrations list` verificó la conexión EF desde la API a SQL Server. El panel ya muestra tablas del esquema aplicado; no se guardaron registros funcionales ni de prueba durante esta tarea.

## Incremento local: reintentos de recogida y entrega

Rama `feature/delivery-command-retries`, commit `0b9f2a5`, PR #13: https://github.com/EmanuelC1601/poraqui-backend/pull/13. Parte de `d04882a` (PR #12 ya integrado en la rama de PR #11, aún no en `main`). `POST /api/orders/{id}/pickup` y `POST /api/orders/{id}/delivery/confirm` aceptan el reintento con el mismo courier asignado y el código correcto después de una confirmación persistida. Reintentos no aumentan `Order.Version`, no duplican historial ni emiten otra notificación. Un código incorrecto se rechaza. Es una extensión del comportamiento en el servidor; no altera el esquema.

Verificado en este incremento: `dotnet build Poraqui.sln` (0 advertencias, 0 errores) y `dotnet test Poraqui.sln --no-build` (19/19; 18 recorridos API usan SQLite temporal/en memoria y 1 comprueba traducción del proveedor SQL Server sin conectarse). La prueba de dominio comprueba los dos reintentos y códigos erróneos. No se probó contra la BD compartida.

## Rama actual: verificación telefónica SMS

Rama `feature/phone-verification`, PR #14: https://github.com/EmanuelC1601/poraqui-backend/pull/14, basada en PR #13. `POST /api/account/phone/verification` solicita OTP por Twilio Verify; `POST /api/account/phone/verification/confirm` solo confirma el teléfono en Identity tras estado `approved`. Se exige E.164 y no se persiste el OTP. Error/caducidad responde 400; proveedor no configurado o inaccesible responde 503. Ambos endpoints comparten rate limit de 5 solicitudes/IP/10 minutos (429). OpenAPI se actualiza por los endpoints MVC. El PR está abierto contra `feature/delivery-command-retries`; commits `81f2106`, `7d7130f`, `7a87572`, `17cd5f6` y `7611ddc`.

Pruebas: 2 nuevas pruebas verifican formularios/URLs de Twilio y aprobación/código inválido/404 usando HttpMessageHandler falso; el recorrido API usa fake solo en prueba para verificar que código inválido no confirma el teléfono, el correcto sí lo confirma y la sexta solicitud en la ventana recibe 429. Build limpio y 21/21 pasan; API con SQLite aislado. No se probaron credenciales reales ni envío SMS real. No cambia el modelo ni requiere migración.

## Rama actual: reintentos idempotentes de comandos de flujo

Rama `feature/workflow-idempotency`, PR #15: https://github.com/EmanuelC1601/poraqui-backend/pull/15, fue integrada en la rama base `feature/phone-verification` mediante `700355a`; sigue fuera de `main`. Una repetición con el mismo actor y datos de revisión, decisión de negocio, inicio de preparación, “listo”, inicio de recorrido, o rechazo de oferta no agrega historial ni vuelve a publicar SignalR; una repetición con valores distintos sigue en conflicto. No alteró el esquema. Pruebas agregadas cubren la versión/historial estable y el rechazo repetido.

Verificado en la rama: `dotnet build Poraqui.sln --no-restore` (0 advertencias/errores), `dotnet test Poraqui.sln` (24/24; base API SQLite temporal, dominio sin BD) y `git diff --check`. No valida concurrencia sobre SQL Server.

## Incremento integrado en la rama de PR #14: concurrencia SQL Server

PR #16, rama `feature/order-command-idempotency`, se integró en la rama del PR #14 mediante `5f464c7`; permanece fuera de `main`. Añade una prueba de integración que fuerza dos aceptaciones de ofertas a llegar simultáneamente a `SaveChanges`, verifica que solo una asigna al pedido y responde conflicto para la otra. Solo corre cuando `ConnectionStrings__SqlServerTest` apunta a la BD aislada `PoraquiIntegrationTests`; aplica las migraciones en esa base y limpia únicamente los registros con GUID generados por la prueba. El endpoint traduce conflictos de escritura de aceptación a HTTP 409.

Verificado localmente: `dotnet build Poraqui.sln` (0 advertencias/errores), `dotnet test Poraqui.sln` (24 aprobadas, 1 omitida porque no está configurada `ConnectionStrings__SqlServerTest`), `git diff --check`. Todavía no se ha ejecutado la prueba contra SQL Server.

## Pendiente

- Verificar migraciones, restricciones y aceptación simultánea en SQL Server antes de integrar PR #6/#7.
- Ejecutar la prueba de aceptación simultánea y revisar/aplicar las migraciones en SQL Server.
- Probar reconexión SignalR en cliente real; la suscripción ya devuelve instantánea persistida al reconectar.
- PR #9: `feature/catalog-options` en commit `084acb3`, publicado en https://github.com/EmanuelC1601/poraqui-backend/pull/9.
- Validar SMS OTP contra cuenta real tras configurar `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid` y `TwilioVerify:ApiKeySecret`; falta idempotencia durable para subida/reintento de comprobantes y asignación de courier con código. La creación de pedidos y el reporte de incidencias ya guardan claves durables; recogida y entrega toleran reintentos con el mismo courier y código. Falta validar aceptación concurrente sobre SQL Server aislado.
- PR #10: `feature/business-hours`, commit `d25e64f`, publicado en https://github.com/EmanuelC1601/poraqui-backend/pull/10. Requiere validación SQL Server antes de integrar.
- PR #11 implementa cancelaciones/incidencias/reembolsos; falta verificar migraciones en SQL Server antes de integrar.
- PR #12 agrega métricas del piloto; está integrado en la rama de PR #11, pero aún no en `main`; falta validar sobre SQL Server.
- PR #13 hace idempotentes reintentos de recogida/entrega; está abierto para revisión/integración en la rama anterior. No necesita migración.
- Instrucciones finales de despliegue en MonsterASP.NET y recorrido SQL Server reproducible.

## Pendiente: base SQL Server aislada para pruebas de concurrencia

`ConnectionStrings:DefaultConnection` está configurada localmente en User Secrets y EF comprobó la conexión. La prueba optativa de concurrencia permanece omitida hasta configurar `ConnectionStrings__SqlServerTest` con una base de pruebas separada llamada exactamente `PoraquiIntegrationTests`. No apuntar esa prueba a la BD compartida. Los endpoints y migraciones aún no se han desplegado a un sitio MonsterASP.

## Fuera de alcance

GPS y distancias automáticas, transferencias bancarias automáticas y frontend.
