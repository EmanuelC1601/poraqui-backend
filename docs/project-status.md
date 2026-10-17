# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` contiene PR #1–#5 hasta `d677167` y es la base de integración. PR #6 (reparto externo), #7 (sincronización offline), #8 (SignalR), #9 (opciones de catálogo), #10 (horarios) y #11 (cancelaciones/incidencias/reembolsos) están abiertos y apilados en ese orden. El desarrollo tiene un único responsable; las asignaciones previas a OpenCode se conservan como antecedente, sin dependencia operativa.

## Implementado en `main`

- Solución .NET 9 compilable: Domain, Application, Infrastructure y Api; EF Core SQL Server e Identity con GUID.
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
- Migración aditiva `BusinessOpeningHours` y script `docs/sql/BusinessOpeningHours.sql`; no aplicado. Pruebas nuevas cubren horario local, cierre, rango y permisos entre negocios.

## PR #11 abierto: cancelaciones, incidencias y reembolsos manuales

Rama `feature/order-cancellations-refunds-incidents`, commit `1347648`, PR https://github.com/EmanuelC1601/poraqui-backend/pull/11, basado en PR #10.

- Cliente cancela solo antes de enviar comprobante; negocio/admin puede cancelar antes de asignar courier. Las ofertas externas abiertas se cancelan junto con el pedido. La repetición del mismo comando de cancelación no duplica historial.
- Si hay evidencia de transferencia enviada o el pago fue aprobado, se crea un caso de revisión manual. Rechazar un pedido con transferencia aprobada también crea el caso. El negocio registra si reembolsó manualmente, el importe y referencia; el cliente ve solo el estado del caso.
- Participantes autorizados informan y consultan incidencias; negocio/admin las resuelve. Incidencias y reembolsos quedan en historial/versionado del pedido y notifican después de persistir.
- Migraciones `OrderCancellationRefundsIncidents` y `OrderCancellationAuditConstraints`, script `docs/sql/OrderCancellationRefundsIncidents.sql`; no aplicadas. La alteración de `CancellationReason` solo afecta a una columna nullable que esta misma pila acaba de añadir, y el dominio limita el texto a 500 caracteres.

## Verificaciones actuales

- Build: `dotnet build Poraqui.sln`, 0 advertencias y 0 errores.
- Pruebas: `dotnet test Poraqui.sln`, 18/18 con SQLite en memoria y archivos temporales. El recorrido verifica horarios, opciones, permisos entre negocios, importes, cancelación/refund manual, incidencias, snapshots, SignalR y entrega persistida.
- Scripts `ExternalCourierOffers`, `OfflineDeliveryCommandSync`, `CatalogProductOptions`, `BusinessOpeningHours` y `OrderCancellationRefundsIncidents` están disponibles para revisión; la última migración agrega `OrderCancellationAuditConstraints`. No hay cambios pendientes del modelo.
- `BusinessOpeningHours` y cancelaciones/incidencias se generaron desde el modelo; los scripts no eliminan tablas ni filas. El segundo script estrecha `CancellationReason`, una columna nueva y todavía sin datos; falta validarlo en SQL Server.
- Portal MonsterASP, solo lectura: `dbo` aparece sin tablas. Esto informa el esquema actual; no verifica conexión EF ni restricciones/carreras SQL Server.

## Pendiente

- Verificar migraciones, restricciones y aceptación simultánea en SQL Server antes de integrar PR #6/#7.
- Prueba de aceptación simultánea de ofertas y ejecución/revisión de migraciones en SQL Server.
- Probar reconexión SignalR en cliente real; la suscripción ya devuelve instantánea persistida al reconectar.
- PR #9: `feature/catalog-options` en commit `084acb3`, publicado en https://github.com/EmanuelC1601/poraqui-backend/pull/9.
- OTP e idempotencia general de operaciones que aún no la tienen.
- PR #10: `feature/business-hours`, commit `d25e64f`, publicado en https://github.com/EmanuelC1601/poraqui-backend/pull/10. Requiere validación SQL Server antes de integrar.
- El incremento de cancelaciones/incidencias/reembolsos está en PR #11; falta verificar migraciones en SQL Server antes de integrar.
- Métricas del piloto, instrucciones finales de despliegue en MonsterASP.NET y recorrido SQL Server reproducible.

## Bloqueado por configuración privada

No existe `ConnectionStrings:DefaultConnection` en User Secrets/entorno de la API. Configúrala con `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api`. No compartas el valor ni uses secretos del chat. Sin la clave no se verifica conexión EF, no se puede comprobar esquema vivo con la aplicación ni aplicar migraciones. Ninguna migración se ha aplicado.

## Fuera de alcance

GPS y distancias automáticas, transferencias bancarias automáticas y frontend.
