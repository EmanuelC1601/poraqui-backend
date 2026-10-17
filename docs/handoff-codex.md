# Handoff Codex → OpenCode

## Base publicada

Publicada en `origin/feature/clean-architecture-foundation` (commit `e65bca3`). El remoto estaba vacío al comenzar y todavía no hay una rama base para abrir un PR; cuando exista `main`/`master`, se puede crear el PR contra ella.

## Incluido

- Solución .NET 9 con proyectos Domain, Application, Infrastructure y Api.
- SQL Server/EF Core, contexto `AppDbContext` e Identity genérico por GUID, registrados al configurar conexión.
- Enum inicial de estados y contratos de consulta/operación para reparto. Son contratos sin implementación: no hay consultas, asignaciones, endpoints ni lógica simulada.
- Documentación de arquitectura, contratos y acuerdos.

## Verificación

`dotnet build Poraqui.sln`: aprobado, 0 advertencias y 0 errores. No hay suite de pruebas todavía. `git diff --check`: aprobado.

## Esquema y actualización

Esta base no agrega tablas de negocio ni migraciones; el contexto solo aporta el esquema estándar de Identity. No se ha conectado ni modificado la BD compartida. Después de integrar el primer cambio de esquema, el Agente 1 documentará aquí el nombre de migración, comandos de actualización y datos funcionales afectados.

Para tomar la rama inicial: `git fetch origin` y `git switch --track origin/feature/clean-architecture-foundation`. No se requiere actualizar esquema: no se creó migración y no se modificó la BD compartida.
