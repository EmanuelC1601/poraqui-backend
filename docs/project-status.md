# Estado verificable del proyecto

Actualizado: 2026-10-17. El PR #1 está bajo revisión. Esta lista distingue reglas de dominio existentes de flujos funcionales expuestos y probados.

## Implementado y verificado

- Solución .NET 9 compilable con Domain, Application, Infrastructure y Api.
- EF Core SQL Server, Identity base y configuración condicional de `AppDbContext`; no se aplican migraciones al iniciar.
- Entidades/reglas iniciales de negocio, producto, zona, tarifa, pedido, líneas y revisión manual de transferencia.
- Cálculo de importes usando valores proporcionados al dominio; todavía no hay un caso de uso que cargue catálogo y tarifa del servidor.
- Mapeos EF de los modelos de comercio y pedidos.
- 8 pruebas unitarias/regresión con SQLite en memoria aislado. La consulta en el portal SQL confirmó conexión del proveedor y 0 tablas de usuario antes de migrar.

## Pendiente

- Login/registro operativo, asignación de roles y permisos por propietario/recurso.
- Casos de uso, validación/autorización, endpoints HTTP y OpenAPI para todo el negocio.
- Opciones de producto, horarios, disponibilidad por negocio, zonas/tarifas administrables y cálculo autoritativo desde base de datos.
- Transferencia: datos de cobro, comprobante privado, revisión autorizada, idempotencia y confirmación de pedido.
- Recorrido interno: aceptación/rechazo del negocio, preparación explícita, repartidores propios, pago al recoger, código de recogida, viaje, contacto consentido, código/confirmación de entrega, estado e historial.
- Reparto externo: disponibilidad, oferta visible con origen/destino/condiciones/remuneración, aceptación atómica, rechazo, expiración, cancelación y ausencia de repartidores.
- SignalR con destinatarios autorizados y eventos después de persistir; sincronización, operaciones idempotentes y validación antes de aceptar entregas offline.
- Incidencias, reembolsos manuales, métricas del piloto y despliegue MonsterASP.NET.
- Migración inicial revisable y validación de restricciones/concurrencia con SQL Server desde la aplicación.

## Bloqueado

- Conexión desde la aplicación y migraciones EF bloqueadas hasta que esté configurado el secreto `ConnectionStrings:DefaultConnection` en User Secrets de `src/Poraqui.Api` o la variable `ConnectionStrings__DefaultConnection`. El portal web permite consultas, pero no sustituye la cadena privada de la aplicación.

## No incluido por alcance

- GPS/distancias automáticas, transferencias bancarias automáticas y frontend.
