# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` contiene PR #1, PR #2 (`a38ed4e`) y PR #3 (`962e74a`). Rama de trabajo: `feature/order-lifecycle`. Esta página diferencia código existente, pruebas ejecutadas y trabajo pendiente.

## Implementado y verificado

- Arquitectura .NET 9 compilable con proyectos Domain, Application, Infrastructure y Api; EF Core SQL Server e Identity con GUID.
- Modelos y mapeos iniciales EF para negocio, producto, zona, tarifa, pedido, líneas y revisión de transferencia.
- Identity API para registro/login cuando existe configuración privada de conexión; roles iniciales y policies.
- Primer administrador instalable con `Bootstrap:AdminKey` privado y transacción.
- Alta y configuración de negocio, listado público activo y activación administrativa.
- Autorización por rol y recurso para leer/configurar negocio; prueba integrada cubre denegación entre propietarios.
- PR #2: `dotnet build Poraqui.sln` (0 advertencias/errores), `dotnet test Poraqui.sln` (10/10 SQLite en memoria; API Identity/negocio y dominio/persistencia).
- En `feature/catalog-pricing` local: rutas CRUD básico de productos, zonas, tarifas; creación de pedido autenticado con totales calculados por productos y tarifa persistidos. Suite actual: build 0 warnings/errors y `dotnet test Poraqui.sln` 11/11 con SQLite aislado, incluyendo permiso entre propietarios, importes autoritativos y recorrido de pedido. Todavía no hay commit/PR para este incremento.
- En `feature/order-lifecycle` local: evidencia PDF/JPEG/PNG privada, revisión manual, aceptación/rechazo del negocio, inicio/fin de preparación, asignación de courier interno, códigos de seis dígitos persistidos como hash, confirmación separada del pago al courier al recoger, inicio de viaje/contacto autorizado y entrega. Incluye historial append-only y token optimista de concurrencia.
- Repartidor propio consulta solo asignaciones propias con origen, destino y remuneración. Teléfono de cliente puede actualizarse por cuenta y solo lo recibe el courier asignado al iniciar recorrido.
- La prueba API integral recorre creación a entrega en SQLite aislado/directorio temporal; una prueba con dos contextos comprueba conflicto ante decisión concurrente. Suite actual pasa 13/13. La rama está todavía sin commit/PR.
- Migración inicial revisable `InitialCommerceAndIdentity`, snapshot EF y script SQL Server idempotente creados. `dotnet ef migrations has-pending-model-changes` confirma que snapshot coincide. La operación `Up` solo crea tablas/índices.
- API no migra automáticamente al iniciar. La migración inicial está generada pero no aplicada.

## Pendiente

- Aplicar migración revisada y verificar restricciones/concurrencia en SQL Server desde la aplicación.
- Opciones de producto; disponibilidad tiene CRUD básico, falta validación por horario.
- Consulta y actualización del pedido; aceptación/rechazo del negocio, pagos manuales, evidencia privada y confirmación de revisión.
- Reparto externo: disponibilidad, ofertas, aceptación/rechazo concurrentes, cancelación, vencimiento y ausencia de repartidores.
- Datos de transferencia del negocio, opciones de productos y disponibilidad por horario; flujo de cancelaciones/reembolsos.
- Verificación de teléfono del cliente antes del contacto.
- SignalR autorizado, estado tras reconexión, idempotencia, sincronización offline validada, incidencias/reembolsos, métricas y despliegue MonsterASP.NET.
- Verificar esquema y concurrencia en SQL Server; OpenAPI y recorrido escritos, aún requieren validación de servicio y despliegue.

## Bloqueado

- Configuración privada `ConnectionStrings:DefaultConnection` ausente en la copia local. La variable equivalente es `ConnectionStrings__DefaultConnection`. El portal MonsterASP respondió a una consulta de solo lectura y reportó cero tablas de usuario, pero eso no verifica conexión EF. Sin secreto local no se ejecutan pruebas SQL Server ni se aplica la migración ya generada.
- No se usan credenciales copiadas del chat y no se realizaron cambios de esquema ni de datos compartidos.

## Fuera de alcance

GPS/distancias automáticas, transferencias bancarias automáticas y frontend.

## Integración vigente

- `main`: base de integración, contiene merge de PR #1 (`e346995`).
- `main`: PR #2 `a38ed4e`, PR #3 `962e74a`.
- `feature/order-lifecycle`: flujo de pedido de extremo a extremo todavía local; sin commit ni PR.
- Ver el handoff en `docs/handoff-codex.md` y contratos vigentes/provisionales en `docs/api-contracts.md`.
