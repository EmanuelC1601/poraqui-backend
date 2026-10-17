# Handoff de Codex

## Flujo de trabajo

Hay un único responsable. `main` es la base de integración. Crear ramas `feature/*` desde el `main` actualizado, mantener PRs acotados, correr restore/build/tests y revisar OpenAPI, contratos y SQL de migración antes de integrar. El mismo responsable es dueño de `AppDbContext`, migraciones y aplicación de cambios a la BD compartida. No hay trabajo esperado de OpenCode; esa coordinación queda como antecedente histórico.

## Estado de esta entrega

PR #1 ya estaba integrado en `main`; se revisó su contenido de arquitectura inicial, mapeos de comercio/pedidos y pruebas de dominio. El repositorio conservó cambios posteriores y PR #22–#28 integrados hasta `7216c23`.

En `feature/order-creation-idempotency` se agregó `commandId` a creación de pedido, huella del payload, replay de la respuesta con el mismo pedido/código, 409 si el ID se reutiliza con otra petición, persistencia protegida del código con ASP.NET Data Protection y migración SQL/EF revisable. Data Protection usa nombre de aplicación estable `Poraqui`; si se configura `DataProtection:KeysPath`, persiste llaves en esa ruta privada compartida. En hosting multiinstancia debe ser persistente/compartida.

## Verificación ejecutada

- `dotnet restore Poraqui.sln`: correcto.
- `dotnet build Poraqui.sln --no-restore`: correcto, 0 advertencias/errores.
- `dotnet test Poraqui.sln --no-build`: 21/21 correctas con SQLite en memoria y almacenamiento temporal.
- `dotnet ef migrations has-pending-model-changes`: no hay cambios sin migración.
- `git diff --check`: limpio.

No se aplicó migración ni se modificó SQL Server. No se repitió un ensayo de OpenAPI en este incremento. La idempotencia de creación tiene prueba de replay/conflicto en la ruta de API. Falta probar carrera SQL Server aislada.

## Próximos incrementos

1. Terminar/revisar PR de idempotencia de creación de pedidos y regenerar `docs/openapi.json`.
2. Idempotencia de carga de comprobante y asignación de courier.
3. Reconciliar todos los IDs de migración del servidor con una cadena revisable antes de aplicar ninguna.
4. Configurar `ConnectionStrings:SqlServerTest` hacia BD aislada, probar migraciones, restricciones y aceptación simultánea.
5. OTP real, métricas, reconexión del cliente y despliegue MonsterASP.NET.

Para correr local, guardar `ConnectionStrings:DefaultConnection`, `Bootstrap:AdminKey` y `PrivateFiles:Root` en User Secrets; nunca en Git. En Development OpenAPI está en `/openapi/v1.json`. El recorrido reproducible está en `docs/order-flow.md`.
