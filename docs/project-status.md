# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` contiene PR #1 y PR #2 (`a38ed4e`). Rama de trabajo: `feature/catalog-pricing`. Esta página diferencia código existente, pruebas ejecutadas y trabajo pendiente.

## Implementado y verificado

- Arquitectura .NET 9 compilable con proyectos Domain, Application, Infrastructure y Api; EF Core SQL Server e Identity con GUID.
- Modelos y mapeos iniciales EF para negocio, producto, zona, tarifa, pedido, líneas y revisión de transferencia.
- Identity API para registro/login cuando existe configuración privada de conexión; roles iniciales y policies.
- Primer administrador instalable con `Bootstrap:AdminKey` privado y transacción.
- Alta y configuración de negocio, listado público activo y activación administrativa.
- Autorización por rol y recurso para leer/configurar negocio; prueba integrada cubre denegación entre propietarios.
- PR #2: `dotnet build Poraqui.sln` (0 advertencias/errores), `dotnet test Poraqui.sln` (10/10 SQLite en memoria; API Identity/negocio y dominio/persistencia).
- En `feature/catalog-pricing` local: rutas CRUD básico de productos, zonas, tarifas; creación de pedido autenticado con totales calculados por productos y tarifa persistidos. Suite actual: build 0 warnings/errors y `dotnet test Poraqui.sln` 11/11 con SQLite aislado, incluyendo permiso entre propietarios, importes autoritativos y recorrido de pedido. Todavía no hay commit/PR para este incremento.
- API no migra automáticamente al iniciar. Sin migraciones generadas o aplicadas.

## Pendiente

- Revisión de migración inicial y restricciones sobre SQL Server; conectividad EF desde aplicación.
- Opciones de producto; disponibilidad tiene CRUD básico, falta validación por horario.
- Consulta y actualización del pedido; aceptación/rechazo del negocio, pagos manuales, evidencia privada y confirmación de revisión.
- Preparación explícita, reparto propio, asignación, pago al repartidor al recoger, códigos y recorrido/entrega.
- Reparto externo: disponibilidad, ofertas, aceptación/rechazo concurrentes, cancelación, vencimiento y ausencia de repartidores.
- SignalR autorizado, estado tras reconexión, idempotencia, sincronización offline validada, incidencias/reembolsos, métricas y despliegue MonsterASP.NET.
- OpenAPI validado y guía reproducible del flujo completo.

## Bloqueado

- Configuración privada `ConnectionStrings:DefaultConnection` ausente en la copia local. La variable equivalente es `ConnectionStrings__DefaultConnection`. El portal MonsterASP respondió a una consulta de solo lectura y reportó cero tablas de usuario, pero eso no verifica conexión EF. Sin secreto local no se ejecutan pruebas SQL Server ni se generan/aplican migraciones.
- No se usan credenciales copiadas del chat y no se realizaron cambios de esquema ni de datos compartidos.

## Fuera de alcance

GPS/distancias automáticas, transferencias bancarias automáticas y frontend.

## Integración vigente

- `main`: base de integración, contiene merge de PR #1 (`e346995`).
- `feature/functional-order-flow`: incremento de identidad/negocio en curso; aún sin commit ni PR.
- Ver el handoff en `docs/handoff-codex.md` y contratos vigentes/provisionales en `docs/api-contracts.md`.
