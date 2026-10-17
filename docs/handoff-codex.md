# Handoff de Codex

## Flujo de trabajo

Hay un único responsable. `main` es la base de integración. Crear ramas `feature/*` desde el `main` actualizado, mantener PRs acotados, correr restore/build/tests y revisar OpenAPI, contratos y SQL de migración antes de integrar. El mismo responsable es dueño de `AppDbContext`, migraciones y aplicación de cambios a la BD compartida. No hay trabajo esperado de OpenCode; esa coordinación queda como antecedente histórico.

## Estado de esta entrega

PR #1 ya estaba integrado en `main`; se revisó su contenido de arquitectura inicial, mapeos de comercio/pedidos y pruebas de dominio. El repositorio conservó cambios posteriores y PR #22–#30 integrados hasta `2a15ea2`.

En `feature/order-creation-idempotency` se agregó `commandId` a creación de pedido, huella del payload, replay de la respuesta con el mismo pedido/código, 409 si el ID se reutiliza con otra petición, persistencia protegida del código con ASP.NET Data Protection y migración SQL/EF revisable. Data Protection usa nombre de aplicación estable `Poraqui`; si se configura `DataProtection:KeysPath`, persiste llaves en esa ruta privada compartida. En hosting multiinstancia debe ser persistente/compartida.

PR #29 integró idempotencia de creación de pedido. La rama activa `feature/transfer-evidence-idempotency` requiere `commandId` multipart y guarda hash del contenido con la revisión; replay no duplica archivo ni historial y contenido/orden distinto responde 409. Migración `TransferEvidenceCommandIdempotency` es aditiva y requiere revisión antes de aplicar. La rama pasó build sin advertencias, 21/21 pruebas SQLite y OpenAPI local HTTP 200.

PR #30 integró idempotencia para comprobantes. La rama activa `feature/courier-assignment-idempotency-main` requiere `commandId` al asignar repartidor propio, persiste hash de solicitud y código de recogida protegido; reintento exacto devuelve el mismo código; clave/payload discordantes o asignación duplicada producen 409. La migración `CourierAssignmentCommandIdempotency` es aditiva.

La rama activa pasó restore, build sin advertencias y 21/21 pruebas SQLite; agregué un subrecorrido propio de asignación con replay, payload alterado y clave nueva para pedido asignado. No cubre simultaneidad de SQL Server. OpenAPI debe regenerarse antes de abrir el PR.

## Verificación ejecutada

- `dotnet restore Poraqui.sln`: correcto.
- `dotnet build Poraqui.sln --no-restore`: correcto, 0 advertencias/errores.
- `dotnet test Poraqui.sln --no-build`: 21/21 correctas con SQLite en memoria y almacenamiento temporal.
- `dotnet ef migrations has-pending-model-changes`: no hay cambios sin migración.
- `git diff --check`: limpio.

PR #29 quedó integrado. Su migración no se aplicó por este cambio; no se modificó SQL Server. OpenAPI local respondió HTTP 200 y se actualizó el snapshot. La prueba de API cubre replay/conflicto de creación. Falta probar carrera SQL Server aislada.

## Próximos incrementos

1. Verificar e integrar idempotencia de asignación, regenerar OpenAPI.
2. Reconciliar la cadena histórica de migraciones antes de su aplicación.
3. Reconciliar todos los IDs de migración del servidor con una cadena revisable antes de aplicar ninguna.
4. Configurar `ConnectionStrings:SqlServerTest` hacia BD aislada, probar migraciones, restricciones y aceptación simultánea.
5. OTP real, métricas, reconexión del cliente y despliegue MonsterASP.NET.

Para correr local, guardar `ConnectionStrings:DefaultConnection`, `Bootstrap:AdminKey` y `PrivateFiles:Root` en User Secrets; nunca en Git. En Development OpenAPI está en `/openapi/v1.json`. El recorrido reproducible está en `docs/order-flow.md`.
