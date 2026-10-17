# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` integra PR #1–#6. PR #7–#12 se han integrado en ramas de trabajo apiladas, todavía fuera de `main`. PR #10 se integró en `feature/catalog-options` (`fc68274`) y PR #11, que incluye PR #12, en `feature/business-hours` (`efa53d9`). PR #13–#14 y PR #18–#21 siguen en sus pilas de ramas, fuera de `main`. El equipo trabaja con un único responsable; OpenCode ya no participa.

## Implementado

- En `main` (PR #1–#6): Clean Architecture compilable; Identity, roles y autorización por recurso; alta/configuración de comercio, catálogo básico, zonas/tarifas y pedidos calculados por el servidor; transferencia manual, comprobante privado, revisión, preparación, repartidor propio, códigos, estados e historial; reparto externo con ofertas, disponibilidad, expiración, cancelación y aceptación exclusiva.
- En ramas apiladas #7–#10: sincronización offline de entrega con validación/idempotencia; SignalR con audiencias autorizadas y snapshot; opciones/precios del catálogo con snapshots; horarios y zona horaria local.
- En ramas apiladas: cancelación, auditoría, reembolsos, incidencias y métricas (PR #11–#12); PR #13 agrega reintento seguro de confirmaciones de recogida/entrega con código correcto.
- La API no migra al iniciar. No se borraron ni recrearon datos compartidos.

## Pendiente o bloqueado

| Función | Estado verificable |
|---|---|
| Integrar PR #7–#12 hasta `main` | Pendiente; #7–#12 están integrados en ramas apiladas, fuera de `main`. |
| OTP de teléfono | PR #14 en pila; proveedor/configuración real pendientes. |
| Reintentos/idempotencia durable para comandos y altas | PR #13 añade repetición segura de recogida/entrega. OTP/idempotencia durable y asignación siguen en PR #14 y #18–#21; verificar e integrar en orden. |
| Revisión SQL Server de restricciones y aceptación/asignación concurrente | Bloqueada para pruebas automatizadas aisladas por falta de `ConnectionStrings:SqlServerTest`. La conexión privada `DefaultConnection` sí permite consultar historial. |
| Prueba de reconexión SignalR con cliente real | Pendiente; el hub devuelve snapshot persistido al suscribirse. |
| Despliegue MonsterASP.NET, almacenamiento privado persistente y recorrido publicado | Pendiente. |
| GPS/distancias automáticas, transferencias bancarias automáticas y frontend | Fuera de alcance. |

## Verificación de este incremento

- La pila que contiene PR #11–#13 está en revisión; PR #13 es `feature/delivery-command-retries`.
- `dotnet build Poraqui.sln --no-restore`: correcto, 0 advertencias y 0 errores; requiere `DOTNET_ROLL_FORWARD=Major` en este host que tiene runtime .NET 10 para ejecutar tests net9.
- `dotnet test Poraqui.sln --no-build --no-restore`: 19/19 con SQLite aislada (incluye una prueba que inspecciona traducción SQL Server, sin conexión SQL).
- `git diff --check`: pendiente tras esta actualización documental.
- EF Core contra la configuración privada `ConnectionStrings:DefaultConnection` lista las migraciones hasta `OrderCancellationAuditConstraints` y `has-pending-model-changes` confirma que el modelo coincide. Esto confirma conectividad e historial, no ejecuta una prueba de bloqueo/concurrencia. No se aplicó una migración en esta sesión.
- `ConnectionStrings:SqlServerTest` no está configurada; no ejecutar pruebas con borrado/limpieza en la BD compartida.

Para configurar una copia local, establece `ConnectionStrings:DefaultConnection` con User Secrets del proyecto `src/Poraqui.Api`. Para pruebas SQL aisladas, configura además `ConnectionStrings:SqlServerTest` apuntando a una base separada. Nunca incluyas los valores en Git o logs.
