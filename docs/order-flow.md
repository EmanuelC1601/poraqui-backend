# Recorrido reproducible de pedido

## Verificación automatizada aislada

La prueba de integración que recorre el backend completo se ejecuta con:

```powershell
dotnet test tests/Poraqui.Domain.Tests/Poraqui.Domain.Tests.csproj --filter "FullyQualifiedName~Poraqui.Domain.Tests.BusinessApiTests.Manual_payment_and_internal_delivery_flow_preserves_server_pricing_and_history"
```

`ApiTestHost` crea una base SQLite en memoria por prueba y un directorio temporal para comprobantes privados, que se limpia al terminar. La verificación no conecta a SQL Server, no envía SMS real ni contacta al cliente por WhatsApp. El proveedor de verificación telefónica es de prueba; el canal WhatsApp/llamada devuelve datos de contacto autorizados para que el repartidor contacte al cliente.

La ruta principal con persistencia SQL Server real se verifica aparte con `SqlServerOrderFlowApiTests.Sql_server_api_flow_creates_reviews_prepares_assigns_and_delivers_one_order`. Configura `ConnectionStrings:SqlServerTest` en User Secrets con una base vacía/aislada y expón solo esa clave al proceso de pruebas como `ConnectionStrings__SqlServerTest`. El test aplica migraciones revisables únicamente a esa base, ejecuta Identity, creación del negocio y catálogo, precios de servidor, comprobante privado, revisión manual, preparación, asignación interna, pago al courier al recoger, contacto autorizado y entrega con código. Limpia sus filas identificadas al terminar. No usa la BD compartida, Twilio real ni mensajería de WhatsApp.

El recorrido de reparto externo con proveedor SQL Server se verifica con `SqlServerExternalDeliveryFlowApiTests.Sql_server_api_flow_dispatches_accepts_and_delivers_one_external_order`. Comprueba zona de disponibilidad, publicación de oferta, origen, destino, remuneración y condiciones visibles antes de aceptar, aceptación repetible, código de recogida, pago al recoger registrado por el negocio, llamada autorizada y confirmación de entrega sincronizada sin conexión. Comprueba que el servidor rechace un código inválido, que el mismo comando se pueda reintentar sin duplicar la entrega y que reutilizarlo con otros datos produzca conflicto. Limpia sus registros identificados en la misma base aislada. Expiración, cancelación, rechazo, ausencia de repartidores y SignalR tienen además cobertura de ruta sobre SQLite; la aceptación simultánea SQL Server se prueba separadamente.

El mismo recorrido de API cubre también cancelación por el cliente después de subir comprobante y antes de la aceptación del negocio. La respuesta queda en `Cancelled` con reembolso manual `ReviewRequired`; repetir la cancelación mantiene un único caso. La regla de dominio también cubre cancelación pendiente de pago, pago aprobado aún no aceptado, y rechazo al intentar cancelar después de que el negocio aceptó.

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

El recorrido SQLite cubre también reparto externo, SignalR, incidencias, reembolso manual y métricas; dos pruebas API SQL Server cubren los recorridos internos y externos con persistencia real, incluida la confirmación offline validada en el servidor e idempotente. Las pruebas SQL Server de concurrencia cubren creación idempotente y las asignaciones/aceptaciones de couriers. Ninguna prueba equivale a una publicación en MonsterASP.NET o a una entrega Twilio/WhatsApp real. La suite local actual omite las cinco pruebas SQL Server cuando falta `ConnectionStrings:SqlServerTest`; consulta el estado vigente en `project-status.md`.

## Ejecución manual de la API

La API ejecutada manualmente requiere `ConnectionStrings:DefaultConnection`, `Bootstrap:AdminKey` y `PrivateFiles:Root` privados. Las pruebas SQLite no requieren estos valores; las pruebas SQL Server requieren además `ConnectionStrings:SqlServerTest` aislada. Para despliegue con más de una instancia, configura `DataProtection:KeysPath` a almacenamiento privado persistente y compartido entre instancias. No uses credenciales publicadas en conversaciones.

## Ejecutar

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
dotnet user-secrets set "Bootstrap:AdminKey" "<clave privada aleatoria de al menos 32 caracteres>" --project src/Poraqui.Api
dotnet user-secrets set "PrivateFiles:Root" "<ruta privada fuera del web root>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

El `UserSecretsId` ya está configurado. La API no migra al iniciar. Revisa scripts SQL y concilia `__EFMigrationsHistory` antes de aplicar migraciones con `dotnet tool run dotnet-ef database update --project src/Poraqui.Infrastructure --startup-project src/Poraqui.Api`. En Development se sirve OpenAPI en `/openapi/v1.json`.
