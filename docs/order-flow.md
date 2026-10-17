# Recorrido reproducible de pedido

Este recorrido documenta el flujo actualmente implementado. Requiere una BD de desarrollo aislada/aprobada, un `ConnectionStrings:DefaultConnection` privado, `Bootstrap:AdminKey` privado y almacenamiento privado persistente para comprobantes. No usar el password ni la cadena publicados en el chat.

## Ejecutar

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
dotnet user-secrets set "Bootstrap:AdminKey" "<clave privada aleatoria de al menos 32 caracteres>" --project src/Poraqui.Api
dotnet user-secrets set "PrivateFiles:Root" "<ruta privada fuera del web root>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

El `UserSecretsId` ya está configurado en el proyecto. La migración inicial revisable es `InitialCommerceAndIdentity`. La API no migra al iniciar. Para SQL Server, tras configurar conexión y volver a comprobar que la BD conserva su esquema/datos esperados, revisar y aplicar explícitamente con `dotnet tool restore` y `dotnet tool run dotnet-ef database update --project src/Poraqui.Infrastructure --startup-project src/Poraqui.Api`. Hasta configurar la clave privada, no se puede verificar conexión EF ni aplicar esta migración. En Development se publica OpenAPI en `/openapi/v1.json`.

## Secuencia

1. Crear primer administrador con `POST /api/auth/bootstrap-admin` usando `Bootstrap:AdminKey`; luego iniciar sesión en `POST /api/auth/login?useCookies=false` para obtener bearer token.
2. Crear cuenta de negocio con `POST /api/auth/register`, iniciar sesión y registrar negocio con `POST /api/businesses`. El admin activa el negocio con `PUT /api/businesses/{id}/active`.
3. El admin crea una zona con `POST /api/delivery-zones`. El negocio crea productos mediante `POST /api/businesses/{id}/products` y una tarifa con `POST /api/businesses/{id}/delivery-rates`.
4. La cuenta cliente registra teléfono en `PUT /api/account/phone` y crea pedido con `POST /api/orders`:

   ```json
   {
     "businessId": "<guid>",
     "zoneId": "<guid>",
     "deliveryAddress": "Calle y número",
     "items": [{ "productId": "<guid>", "quantity": 2 }]
   }
   ```

   El resultado contiene importes calculados por servidor y un código de recepción para que el cliente lo conserve. No mandar un precio como fuente de cálculo.
5. El cliente sube su comprobante con `POST /api/orders/{orderId}/transfer-evidence` como `multipart/form-data` con campo `file`.
6. El propietario revisa el pago en `POST /api/businesses/{businessId}/orders/{orderId}/payment-review`, acepta/rechaza en `.../decision`, y, si acepta, inicia `.../preparation` y luego marca `.../ready`.
7. El admin asigna rol de repartidor propio con `PUT /api/admin/users/{courierId}/delivery-person`. El negocio registra la asignación con `POST /api/businesses/{businessId}/orders/{orderId}/assign-courier`, incluyendo `courierUserId` y `courierPaymentAmount`. El negocio recibe el código de recogida y se lo entrega al repartidor.
8. El repartidor confirma recogida mediante `POST /api/orders/{orderId}/pickup` con el código. Esa acción registra por separado que recibió el pago del negocio.
9. El repartidor llama `POST /api/orders/{orderId}/delivery/start` con `contactChannel` igual a `whatsapp` o `call`. El API autoriza el acceso y devuelve el teléfono del cliente para el contacto manual.
10. Al recibir el pedido, el cliente comunica su código al repartidor, quien confirma con `POST /api/orders/{orderId}/delivery/confirm`. Cliente y negocio consultan el estado e historial con sus rutas GET autorizadas.

## Qué valida hoy

`dotnet test Poraqui.sln` incluye un recorrido de API de pedido con comprobante privado, revisión manual, decisión del negocio, preparación, asignación de repartidor propio, códigos correctos/incorrectos, pago recibido por courier, contacto y estado/historial final. Se ejecuta contra SQLite en memoria y directorio temporal de comprobantes. Esto no sustituye validaciones de migración, índices ni concurrencia en SQL Server.

## Límites actuales

El negocio entrega manualmente el código de recogida al repartidor y cliente conserva el código de recepción desde la creación del pedido. El contacto WhatsApp/llamada lo realiza el repartidor fuera del API; el endpoint solo entrega datos autorizados y registra el canal seleccionado. Las tarifas siguen siendo zonas fijas; no hay GPS. Catálogo de opciones, estados/horarios comerciales, cancelaciones/reembolsos, notificaciones SignalR y reparto externo siguen pendientes.
