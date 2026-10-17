# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` integra PR #1–#6 (`d39d265`). PR #7–#13 están integrados en ramas apiladas, todavía fuera de `main`: PR #10 quedó en `feature/catalog-options` (`fc68274`), PR #11/#12 en `feature/business-hours` (`efa53d9`) y PR #13 en `feature/order-cancellations-refunds-incidents` (`634590c`). PR #14 está abierto desde `feature/phone-verification`; esa rama también contiene los PR #15–#18 ya integrados dentro de la pila. PR #19–#21 siguen en ramas posteriores. Hay un solo responsable; OpenCode ya no participa.

## Implementado en el código disponible

- Clean Architecture, ASP.NET Core Web API, EF Core SQL Server e Identity con roles/permisos por negocio y recurso.
- Registro/configuración del negocio, catálogo, opciones, disponibilidad, zonas, tarifas y pedidos con importes calculados en servidor.
- Datos de transferencia por negocio y snapshot por pedido; comprobante privado y revisión manual; aceptación/rechazo del negocio, preparación explícita y auditoría.
- Courier propio y externo: oferta con origen/destino/condiciones/remuneración, disponibilidad, expiración, rechazo/cancelación y asignación única; pago del courier registrado al recoger, códigos de recogida/entrega, contacto autorizado y estados/historial.
- Horarios locales; cancelaciones, incidencias, reembolsos manuales y métricas protegidas; SignalR autorizado con snapshot actual; confirmación offline validada e idempotencia de varios comandos.
- Verificación de teléfono con Twilio Verify y límite de solicitudes; idempotencia durable de reportes de incidencias.

## Estado de integración y pendientes

| Módulo | Estado | Pendiente verificable |
|---|---|---|
| PR #1–#6, incluido flujo comercio/pedido y reparto externo | Integrado en `main` | Persistencia de comprobantes en hosting y prueba SQL Server de concurrencia. |
| PR #7–#13, sync offline, SignalR, opciones, horarios, cancelación/métricas y reintentos de recogida/entrega | Integrados en ramas de trabajo | Llevar la pila a `main`; probar SQL Server aislado y reconexión con cliente real. |
| PR #14, SMS OTP, junto con PR #15–#18 apilados | En `feature/phone-verification`; PR #14 abierto | Configurar Twilio privado y hacer envío SMS real. PR #18 tiene migración aplicada, faltan carreras SQL Server para comandos duplicados. |
| PR #19–#21, idempotencia de creación de pedidos, comprobantes y asignación | Ramas apiladas posteriores | Revisar e integrar en orden; verificar concurrencia y reintentos. |
| MonsterASP.NET | Pendiente | Variables privadas, ruta persistente de archivos, WebSockets y recorrido publicado. |
| GPS/distancias automáticas, transferencias bancarias automáticas y frontend | Fuera de alcance | Se usan zonas y tarifas configuradas. |

## Verificación de la rama actual

- Rama: `feature/phone-verification`; código incluye PR #14 y PR #15–#18 integrados en esta pila.
- `dotnet restore Poraqui.sln`, `dotnet build Poraqui.sln --no-restore`: correctos; build 0 advertencias y 0 errores, SDK/runtime .NET 10.
- `dotnet test Poraqui.sln --no-build --no-restore`: 24 aprobadas, 1 omitida. La omitida es la prueba de aceptación concurrente que requiere `ConnectionStrings__SqlServerTest` con una base aislada llamada `PoraquiIntegrationTests`. El resto usa SQLite local aislada o dominio sin BD.
- `git diff --check`: limpio antes del ajuste documental.
- EF Core conectó usando `ConnectionStrings:DefaultConnection` privada: muestra las nueve migraciones registradas hasta `IncidentCommandIdempotency`; `migrations has-pending-model-changes` no encuentra diferencias. Esta revisión no aplicó migraciones ni ejecutó la carrera SQL.
- La clave `ConnectionStrings:SqlServerTest` no está configurada. No dirigir pruebas con limpieza a la base compartida.

Para otra copia local, configura `ConnectionStrings:DefaultConnection` con User Secrets de `src/Poraqui.Api`; no compartas la cadena ni la guardes en Git. Para pruebas SQL Server destructivas o concurrentes, configura además `ConnectionStrings:SqlServerTest` con una instancia/base separada `PoraquiIntegrationTests`.
