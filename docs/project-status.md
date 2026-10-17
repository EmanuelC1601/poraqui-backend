# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` es la base de integración y contiene PR #1–#5 hasta `d677167`. El incremento de ofertas externas está en `feature/external-courier-offers`, basado en ese commit. Este registro separa implementación, verificación, pendientes y bloqueos.

## Implementado

- Solución .NET 9 compilable con Domain, Application, Infrastructure y Api; EF Core SQL Server e Identity con GUID.
- Identity API, roles y policies; alta de negocio y autorización por propietario/recurso.
- Catálogo básico, zonas, tarifas y pedido con precios y tarifa calculados por el servidor.
- Configuración privada de transferencia del negocio y snapshot por pedido; creación de pedido requiere los datos configurados.
- Comprobante privado, revisión manual, decisión del negocio, preparación explícita, asignación de repartidor propio, códigos de recogida/recepción, pago al courier registrado aparte, contacto autorizado y consulta de estados/historial.
- Reparto externo en esta rama: alta de rol/perfil por admin, disponibilidad por zona, publicación a repartidores disponibles, oferta con origen/destino/condiciones/remuneración, rechazo, cancelación, expiración al consultar o aceptar, respuesta `NoCouriersAvailable` y aceptación con asignación atómica respaldada por `Order.Version`, token de versión de oferta e índice único filtrado por pedido aceptado.
- La migración `ExternalCourierOffers` crea tablas e índices nuevos; el script SQL revisable está en `docs/sql/20261017191548_ExternalCourierOffers.sql`. `Up` no modifica ni elimina tablas existentes. No se ha aplicado.
- No hay migración automática al iniciar la API.

## Verificaciones

- `dotnet build Poraqui.sln`: debe pasar sin advertencias ni errores antes de integrar este incremento.
- `dotnet test Poraqui.sln`: 14 pruebas con SQLite en memoria y almacenamiento temporal aislado; incluye recorrido desde pago manual hasta entrega por oferta externa, rechazo, cancelación, sin repartidores disponibles y una aceptación posterior.
- `dotnet ef migrations has-pending-model-changes`: modelo alineado con la migración generada.
- SQL Server: todavía no verificado desde EF; SQLite no demuestra compatibilidad de índices/restricciones ni concurrencia de aceptación simultánea en SQL Server.

## Pendiente

- Ejecutar y revisar el caso de aceptación simultánea en SQL Server configurado; el test existente cubre una única aceptación y el modelo usa token optimista e índice único como protección concurrente.
- Opciones de producto; horarios y disponibilidad de negocio; verificación de teléfono.
- Cancelación de pedidos e incidencias con seguimiento de reembolso manual completo.
- SignalR con destinatarios autorizados y persistencia previa a notificación; lectura del estado actual al reconectar.
- Idempotencia con clave de operación y sincronización offline con validación de entrega al recibir acciones.
- Métricas del piloto y validación de despliegue en MonsterASP.NET.
- OpenAPI se sirve en Development; falta una sesión SQL configurada y un recorrido contra SQL Server.

## Bloqueado

- Falta `ConnectionStrings:DefaultConnection` privada en User Secrets/variables de entorno. Configúrala con `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api`; no imprimas ni subas el valor.
- Sin esa clave no se verifica conexión EF, no se revisa esquema vivo y no se aplica ninguna migración. No se cambió el esquema ni se limpiaron datos compartidos.

## Fuera de alcance

GPS y distancias calculadas, transferencias bancarias automáticas y frontend.

## Flujo Git

Un solo responsable desarrolla ramas `feature/*`, compila, prueba y revisa migraciones antes de abrir PR. La integración se hace a `main` cuando las verificaciones pasan. Los merges confirmados son PR #1 `e346995`, PR #2 `a38ed4e`, PR #3 `962e74a`, PR #4 `e9c4435` y PR #5 `d677167`.
