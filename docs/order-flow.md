# Recorrido reproducible de pedido

## Verificación automatizada aislada

La prueba de integración que recorre el backend completo se ejecuta con:

```powershell
dotnet test tests/Poraqui.Domain.Tests/Poraqui.Domain.Tests.csproj --filter "FullyQualifiedName~Poraqui.Domain.Tests.BusinessApiTests.Manual_payment_and_internal_delivery_flow_preserves_server_pricing_and_history"
```

`ApiTestHost` crea una base SQLite en memoria por prueba y un directorio temporal para comprobantes privados, que se limpia al terminar. La verificación no conecta a SQL Server, no envía SMS real ni contacta al cliente por WhatsApp. El proveedor de verificación telefónica es de prueba; el canal WhatsApp/llamada devuelve datos de contacto autorizados para que el repartidor contacte al cliente.

La prueba recorre estos pasos y valida sus respuestas:

1. Crear al administrador inicial, registrar negocio y cliente, y configurar el negocio como activo.
2. Crear zona, productos, grupos/opciones, disponibilidad, tarifa y horario; consultar el catálogo público.
3. Verificar teléfono con el proveedor falso y probar que un precio enviado por el cliente se rechaza.
4. Crear el pedido con selección válida. El servidor calcula productos, opciones y envío; comprueba total `94.75`, código de entrega, reintento idempotente y conflicto si se reutiliza el comando con otros datos.
5. Subir comprobante privado, reintentar la carga y comprobar el conflicto con contenido diferente. El negocio autorizado puede descargarlo; otro negocio recibe `403`.
6. Revisar manualmente el pago, aceptar el pedido, iniciar preparación y marcarlo listo.
7. En la ruta de repartidor propio, asignar un repartidor, registrar el pago que el negocio le entrega al recoger, validar el código de recogida, devolver los datos de contacto y confirmar la entrega con código. También prueba repetición y conflicto de comandos.
8. En la ruta de reparto externo, activar disponibilidad por zona, publicar ofertas con origen, destino, condiciones y remuneración, rechazar una oferta y aceptar otra. Comprueba cancelación de ofertas, ausencia de repartidores, aceptación idempotente, exclusión de un segundo ganador y eventos SignalR dirigidos a los repartidores.
9. Validar recogida, inicio del recorrido y contacto autorizado por WhatsApp; confirmar entrega directa o validar una confirmación capturada sin conexión antes de aceptarla en el servidor. Consulta estado, historial, incidentes, reembolso manual y métricas.
10. Comprobar que SignalR entrega cambios persistidos y devuelve el estado actual al reconectar; el cliente y el negocio ajeno no obtienen datos privados del pedido.

La prueba es un recorrido de integración amplio y reproducible, no demuestra concurrencia real de SQL Server ni sustituye pruebas con el proveedor externo de SMS. Las pruebas de aceptación simultánea sobre SQL Server requieren `ConnectionStrings:SqlServerTest` apuntando a una base aislada.

Este recorrido requiere `ConnectionStrings:DefaultConnection`, `Bootstrap:AdminKey` y `PrivateFiles:Root` privados. Para despliegue con más de una instancia, configura `DataProtection:KeysPath` a almacenamiento privado persistente y compartido entre instancias. No uses credenciales publicadas en conversaciones.

## Ejecutar

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
dotnet user-secrets set "Bootstrap:AdminKey" "<clave privada aleatoria de al menos 32 caracteres>" --project src/Poraqui.Api
dotnet user-secrets set "PrivateFiles:Root" "<ruta privada fuera del web root>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

El `UserSecretsId` ya está configurado. La API no migra al iniciar. Revisa scripts SQL y concilia `__EFMigrationsHistory` antes de aplicar migraciones con `dotnet tool run dotnet-ef database update --project src/Poraqui.Infrastructure --startup-project src/Poraqui.Api`. En Development se sirve OpenAPI en `/openapi/v1.json`.
