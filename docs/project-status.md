# Estado verificable del proyecto

Actualizado: 2026-10-17. La rama de trabajo es `feature/functional-order-flow`, basada en `main` (`e346995`). El PR #1 está integrado. Esta página diferencia código existente, pruebas ejecutadas y trabajo pendiente.

## Implementado y verificado

- Arquitectura .NET 9 compilable con proyectos Domain, Application, Infrastructure y Api; EF Core SQL Server e Identity con GUID.
- Modelos y mapeos iniciales EF para negocio, producto, zona, tarifa, pedido, líneas y revisión de transferencia.
- Identity API para registro/login cuando existe configuración privada de conexión; roles iniciales y policies.
- Primer administrador instalable con `Bootstrap:AdminKey` privado y transacción.
- Alta y configuración de negocio, listado público activo y activación administrativa.
- Autorización por rol y recurso para leer/configurar negocio; prueba integrada cubre denegación entre propietarios.
- Suite de pruebas compila solución y pasa 10/10 en SQLite en memoria. Incluye API Identity/negocio y pruebas de dominio/persistencia.
- API no migra automáticamente al iniciar. Sin migraciones generadas o aplicadas.

## Pendiente

- Revisión de migración inicial y restricciones sobre SQL Server; conectividad EF desde aplicación.
- Catálogo con opciones y disponibilidad, zonas/tarifas administrables y precio calculado en servidor.
- Creación de pedido, aceptación/rechazo del negocio, pagos manuales, evidencia privada y confirmación de revisión.
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
