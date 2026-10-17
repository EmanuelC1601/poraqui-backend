# Recorrido reproducible de pedido

Este recorrido requiere `ConnectionStrings:DefaultConnection`, `Bootstrap:AdminKey` y `PrivateFiles:Root` privados. No uses valores de credenciales publicados en el chat.

## Ejecutar

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
dotnet user-secrets set "Bootstrap:AdminKey" "<clave privada aleatoria de al menos 32 caracteres>" --project src/Poraqui.Api
dotnet user-secrets set "PrivateFiles:Root" "<ruta privada fuera del web root>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

El `UserSecretsId` ya está configurado. Las 12 migraciones revisables se listan en [api-contracts.md](api-contracts.md); EF confirmó aplicadas las 12 en desarrollo y el modelo no tiene cambios pendientes. La API no migra al iniciar. Antes de aplicar migraciones futuras, revisa que el esquema y los datos actuales coincidan y usa `dotnet tool run dotnet-ef database update --project src/Poraqui.Infrastructure --startup-project src/Poraqui.Api` explícitamente. Las migraciones tienen scripts revisables en `docs/sql/`. En Development se publica OpenAPI actualizado en `/openapi/v1.json`.

## Secuencia

1. Crea el primer administrador con `POST /api/auth/bootstrap-admin` y luego inicia sesión en `POST /api/auth/login?useCookies=false`.
2. Registra una cuenta de negocio, inicia sesión, crea negocio con `POST /api/businesses`, actívalo como admin con `PUT /api/businesses/{id}/active` y configura transferencia con `PUT /api/businesses/{id}/transfer-details`.
3. El admin crea una zona con `POST /api/delivery-zones`. El negocio registra productos con `POST /api/businesses/{id}/products`, configura grupos de variantes con `POST /api/products/{productId}/option-groups` y tarifa con `POST /api/businesses/{id}/delivery-rates`. El negocio puede cambiar la disponibilidad de grupos/opciones con sus rutas `.../availability`.
   Antes de aceptar pedidos con horario, el propietario configura su zona y semana en `PUT /api/businesses/{id}/opening-hours`; la consulta pública `GET` muestra horario y disponibilidad actual.
4. Cliente registra teléfono con `PUT /api/account/phone` y crea pedido con `POST /api/orders`, incluyendo un `commandId` GUID estable para reintentos:

   ```json
   {
     "businessId": "<guid>",
     "zoneId": "<guid>",
     "deliveryAddress": "Calle y número",
     "commandId": "<guid estable para reintentos>",
     "items": [{ "productId": "<guid>", "quantity": 2, "optionIds": ["<guid>"] }]
   }
   ```

   El servidor valida opciones y calcula importes a partir de precio base más diferencias de precio. La respuesta conserva los nombres/precios seleccionados, snapshot de transferencia y código de recepción para que el cliente lo guarde.
5. Cliente sube comprobante privado en `POST /api/orders/{orderId}/transfer-evidence` con multipart `file` y `commandId` GUID estable. Un reintento con los mismos datos no crea una segunda revisión.
6. Negocio revisa transferencia en `POST /api/businesses/{businessId}/orders/{orderId}/payment-review`, acepta/rechaza con `.../decision`, inicia preparación en `.../preparation` y marca listo en `.../ready`.
7. Para reparto propio, admin asigna rol con `PUT /api/admin/users/{courierId}/delivery-person`; negocio asigna con `POST .../assign-courier` y `commandId` GUID estable. Reintentar la asignación devuelve el mismo código de recogida; una segunda asignación distinta se rechaza. Para reparto externo, admin usa `PUT /api/admin/users/{courierId}/external-courier`, courier marca disponibilidad y zona, y negocio publica ofertas con remuneración/condiciones en `POST .../external-offers`.
8. Courier externo ve origen, destino, zona, condiciones y remuneración en `GET /api/courier/external/offers`; puede rechazar o aceptar. La aceptación asigna pedido y devuelve código de recogida al courier ganador. Si el negocio cancela ofertas abiertas se usa `POST .../external-offers/cancel`; si no hay candidatos la API devuelve `NoCouriersAvailable`.
9. Courier confirma recogida con `POST /api/orders/{orderId}/pickup` y el código. Esto registra por separado el pago que recibió del negocio.
10. Courier inicia recorrido con `POST /api/orders/{orderId}/delivery/start`, `contactChannel` `whatsapp` o `call`. El teléfono se devuelve solo al courier asignado para contacto manual.
11. Cliente entrega su código de recepción al courier; este confirma con `POST /api/orders/{orderId}/delivery/confirm`. Cliente y negocio consultan estado e historial en sus rutas GET autorizadas.
12. Si la confirmación se capturó sin internet, el courier sincroniza con `POST /api/orders/{orderId}/sync/delivery-confirmation` usando `commandId`, código y `clientCapturedAtUtc`. El backend valida el estado/código actuales antes de aceptar. Repetir exactamente el mismo comando devuelve la respuesta persistida y no duplica historial; reutilizar el ID con otro payload da 409.
13. Cliente cancela antes de enviar comprobante con `POST /api/orders/{orderId}/cancel`; negocio/admin cancela antes de asignar courier con la ruta equivalente bajo el negocio. Si se había enviado comprobante o aprobado el pago, se crea seguimiento manual de reembolso. El cliente consulta el estado en `GET /api/orders/{orderId}`; el negocio registra el resultado manual en `.../refund-review`.
14. Cliente, negocio o courier asignado informa una incidencia con `POST /api/orders/{orderId}/incidents`. Participantes autorizados consultan incidencias y el negocio/admin registra resolución con `POST .../incidents/{incidentId}/resolve`.
15. Admin consulta métricas agregadas del piloto con `GET /api/admin/metrics/pilot?fromUtc=<ISO-8601>&toUtc=<ISO-8601>`; el intervalo es UTC (inicio incluido, fin excluido) y acepta hasta 366 días. Sin parámetros devuelve los últimos 30 días.

## Verificaciones actuales

La suite actual registra 26 pruebas: 24 aprobadas en SQLite aislado/dominio y dos omitidas. Las omitidas son las carreras SQL Server de aceptar una oferta externa y asignar courier propio; ambas requieren `ConnectionStrings__SqlServerTest` apuntando a una base aislada `PoraquiIntegrationTests`. Build actual .NET 10: cero advertencias y errores. SQLite no sustituye la verificación de restricciones y concurrencia SQL Server.

## Límites

WhatsApp/llamada se hace fuera de la API; esta registra canal y entrega datos autorizados. Las tarifas se basan en zonas configuradas; no hay GPS ni distancias inventadas. OTP está implementado con Twilio Verify; falta envío real. SignalR entrega estado persistido al suscribirse de nuevo, pero no se ha validado reconexión desde cliente real. Falta ejecutar las dos carreras SQL Server aisladas y probar publicación MonsterASP.NET. La cancelación del negocio está disponible hasta antes de asignar courier.
