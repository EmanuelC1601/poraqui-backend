# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` contiene PR #1–#5 hasta `d677167`. PR #6 (reparto externo), #7 (sincronización offline) y #8 (SignalR) están abiertos y apilados en ese orden. La rama activa `feature/realtime-order-updates` termina en `2bcf2b3`.

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

## Verificaciones actuales

- Build: `dotnet build Poraqui.sln`, 0 advertencias y 0 errores después del incremento SignalR.
- Pruebas: `dotnet test Poraqui.sln`, 14/14 con SQLite en memoria y archivos temporales. El recorrido incluye WebSocket SignalR real en TestServer: el cliente autorizado recibe `OrderChanged` después del guardado y luego lee el estado persistido. También cubre negociación anónima rechazada y otro negocio sin acceso a la instantánea.
- Migraciones `ExternalCourierOffers` y `OfflineDeliveryCommandSync` tienen scripts revisables. `has-pending-model-changes` se verificó después de crear migración de sync y no indicó cambios del modelo.
- Portal MonsterASP, solo lectura: `dbo` aparece sin tablas. Esto informa el esquema actual; no verifica conexión EF ni restricciones/carreras SQL Server.

## Pendiente

- Verificar migraciones, restricciones y aceptación simultánea en SQL Server antes de integrar PR #6/#7.
- Prueba de aceptación simultánea de ofertas y ejecución/revisión de migraciones en SQL Server.
- Probar reconexión SignalR en cliente real; la suscripción ya devuelve instantánea persistida al reconectar.
- Idempotencia para creación de pedidos, pagos y demás operaciones que aún no la tienen.
- Catálogo de opciones, horarios, OTP, cancelación del pedido, incidencias y seguimiento completo de reembolsos manuales.
- Métricas del piloto, instrucciones finales de despliegue en MonsterASP.NET y recorrido SQL Server reproducible.

## Bloqueado por configuración privada

No existe `ConnectionStrings:DefaultConnection` en User Secrets/entorno de la API. Configúrala con `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api`. No compartas el valor ni uses secretos del chat. Sin la clave no se verifica conexión EF, no se puede comprobar esquema vivo con la aplicación ni aplicar migraciones. Ninguna migración se ha aplicado.

## Fuera de alcance

GPS y distancias automáticas, transferencias bancarias automáticas y frontend.
