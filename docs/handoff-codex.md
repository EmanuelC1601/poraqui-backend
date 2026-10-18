# Estado y entrega de Codex

## Flujo de trabajo

El backend tiene un único responsable. `main` es la base de integración; cada incremento parte de `main` actualizado en una rama `feature/*`, se verifica y se integra mediante PR. Contratos, `AppDbContext` y migraciones tienen un solo propietario. La asignación anterior a OpenCode queda como antecedente en `docs/team-work.md`.

## Cambios integrados

PR #1 y PR #22–#50 están integrados. El código incluye identidad y permisos por rol/recurso, comercio y catálogo, pedidos con precios del servidor, comprobantes privados y revisión manual, estados de preparación, reparto interno y externo, ofertas, expiración/cancelación, códigos, contacto autorizado, historial, idempotencia, sync offline, SignalR, incidencias, reembolsos manuales, OTP y métricas piloto. La prueba amplia usa SQLite aislado; no acredita concurrencia SQL Server ni servicios externos reales. Ver [ledger](project-status.md) y [recorrido reproducible](order-flow.md).

Las 13 migraciones integradas están aplicadas en la BD compartida de desarrollo. `dotnet-ef migrations list` se ejecutó el 2026-10-17 con `ConnectionStrings:DefaultConnection` de User Secrets y mostró todas las migraciones sin pendientes. No se aplica migración desde el arranque de la API.

PR #49 documenta el test integral reproducible y sus límites. PR #50 distingue su configuración aislada de la configuración privada requerida para ejecutar la API manualmente. PR #52 (`edac6bf`) registra la integración de .NET 10 y mantiene explícitos los pendientes.

## Último incremento de código: PR #51 (los PR #52 y #53 actualizaron documentación y estado)

PR #51 se integró en `main` como merge `5078005` (commit de trabajo `05d5410`). Actualiza proyectos y paquetes de .NET/EF Core a .NET 10 LTS, SQL Client 7.1.1 y PKCS 10.0.13. No cambia contratos API ni el modelo EF y no genera migraciones.

Validación local con SDK 10.0.402:

- `dotnet build Poraqui.sln --no-restore`: 0 advertencias, 0 errores.
- `dotnet test Poraqui.sln --no-restore`: 31 pasaron; 2 pruebas de concurrencia SQL omitidas por falta de `ConnectionStrings__SqlServerTest`.
- `dotnet list Poraqui.sln package --vulnerable --include-transitive`: ningún paquete vulnerable.
- `dotnet-ef migrations has-pending-model-changes`: no hay cambios de modelo.
- `dotnet-ef migrations list`: 13 migraciones aplicadas, ninguna pendiente (consulta de solo lectura).

Revalidación tras PR #52 con el SDK local 10.0.402: build con 0 advertencias/errores; 31 tests pasaron y 2 carreras SQL se omitieron; listado de migraciones de desarrollo sin pendientes. La prueba SQL requiere que `ConnectionStrings__SqlServerTest` exista en el entorno del proceso de test; `ConnectionStrings:DefaultConnection` se lee de User Secrets. Una consulta directa de solo lectura al esquema SQL Server confirmó 27 tablas, 43 claves foráneas, 40 índices únicos, clave primaria para cada tabla, índice filtrado de una oferta aceptada por pedido y las dos columnas de versión; no hay restricciones `CHECK`. Esto confirma el esquema persistido, no el resultado de carreras simultáneas.

## Pendientes y bloqueos

1. Configurar `ConnectionStrings__SqlServerTest` en el entorno local del proceso de test con la conexión Remote de una base **aislada** y habilitar Remote solo en esa base; después ejecutar las pruebas de aceptación/asignación simultáneas. El test carga además `ConnectionStrings:DefaultConnection` de User Secrets para proteger la BD compartida. No usar la BD compartida como destino de pruebas.
2. Publicar en un sitio MonsterASP.NET con sus variables privadas y almacenamiento persistente; validar health, SignalR y comprobante privado.
3. Configurar secretos Twilio en privado y probar el envío OTP con un teléfono autorizado.

No se publican secretos ni se toman credenciales del chat. El estado verificable vigente está en `docs/project-status.md`.

## Actualización posterior: PR #56

La corrección de precisión del contrato de carga de comprobante se integró en `main` como merge `8f637d6` (commit `fbe5803`). El snapshot OpenAPI ahora coincide con la salida de ejecución para las 61 rutas y declara la carga multipart y sus respuestas reales. Una prueba de contrato valida ese endpoint. Antes de integrar, SDK .NET 10.0.402 compiló con 0 advertencias/errores y ejecutó 32 pruebas aprobadas; las 2 pruebas de concurrencia SQL permanecieron omitidas al faltar la cadena de una base aislada. La consulta de esquema más reciente usó `ConnectionStrings:DefaultConnection` privada en modo solo lectura y confirmó 13 migraciones aplicadas, sin aplicar migraciones nuevas. No hay otro agente del que dependa el trabajo.
