# Estado y entrega de Codex

## Flujo de trabajo

El backend tiene un único responsable. `main` es la base de integración; cada incremento parte de `main` actualizado en una rama `feature/*`, se verifica y se integra mediante PR. Contratos, `AppDbContext` y migraciones tienen un solo propietario. La asignación anterior a OpenCode queda como antecedente en `docs/team-work.md`.

## Cambios integrados

PR #1 y PR #22–#50 están integrados. El código incluye identidad y permisos por rol/recurso, comercio y catálogo, pedidos con precios del servidor, comprobantes privados y revisión manual, estados de preparación, reparto interno y externo, ofertas, expiración/cancelación, códigos, contacto autorizado, historial, idempotencia, sync offline, SignalR, incidencias, reembolsos manuales, OTP y métricas piloto. La prueba amplia usa SQLite aislado; no acredita concurrencia SQL Server ni servicios externos reales. Ver [ledger](project-status.md) y [recorrido reproducible](order-flow.md).

Las 13 migraciones integradas están aplicadas en la BD compartida de desarrollo. `dotnet-ef migrations list` se ejecutó el 2026-10-17 con `ConnectionStrings:DefaultConnection` de User Secrets y mostró todas las migraciones sin pendientes. No se aplica migración desde el arranque de la API.

PR #49 documenta el test integral reproducible y sus límites. PR #50 distingue su configuración aislada de la configuración privada requerida para ejecutar la API manualmente. PR #52 (`edac6bf`) registra la integración de .NET 10 y mantiene explícitos los pendientes.

## Último incremento integrado: PR #51

PR #51 se integró en `main` como merge `5078005` (commit de trabajo `05d5410`). Actualiza proyectos y paquetes de .NET/EF Core a .NET 10 LTS, SQL Client 7.1.1 y PKCS 10.0.13. No cambia contratos API ni el modelo EF y no genera migraciones.

Validación local con SDK 10.0.402:

- `dotnet build Poraqui.sln --no-restore`: 0 advertencias, 0 errores.
- `dotnet test Poraqui.sln --no-restore`: 31 pasaron; 2 pruebas de concurrencia SQL omitidas por falta de `ConnectionStrings__SqlServerTest`.
- `dotnet list Poraqui.sln package --vulnerable --include-transitive`: ningún paquete vulnerable.
- `dotnet-ef migrations has-pending-model-changes`: no hay cambios de modelo.
- `dotnet-ef migrations list`: 13 migraciones aplicadas, ninguna pendiente (consulta de solo lectura).

Revalidación tras PR #52 con el SDK local 10.0.402: build con 0 advertencias/errores; 31 tests pasaron y 2 carreras SQL se omitieron; listado de migraciones de desarrollo sin pendientes. La prueba SQL requiere que `ConnectionStrings__SqlServerTest` exista en el entorno del proceso de test; `ConnectionStrings:DefaultConnection` se lee de User Secrets. Este detalle evita confundir el valor privado persistido con la variable de entorno que consumen las pruebas.

## Pendientes y bloqueos

1. Configurar `ConnectionStrings__SqlServerTest` en el entorno local del proceso de test con la conexión Remote de una base **aislada** y habilitar Remote solo en esa base; después ejecutar las pruebas de aceptación/asignación simultáneas. El test carga además `ConnectionStrings:DefaultConnection` de User Secrets para proteger la BD compartida. No usar la BD compartida como destino de pruebas.
2. Publicar en un sitio MonsterASP.NET con sus variables privadas y almacenamiento persistente; validar health, SignalR y comprobante privado.
3. Configurar secretos Twilio en privado y probar el envío OTP con un teléfono autorizado.

No se publican secretos ni se toman credenciales del chat. El estado verificable vigente está en `docs/project-status.md`.
