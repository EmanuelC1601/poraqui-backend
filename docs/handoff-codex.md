# Handoff Codex → OpenCode

## Base publicada

Pendiente de publicación inicial. Rama: `feature/clean-architecture-foundation`.

## Incluido

- Solución .NET 9 con proyectos Domain, Application, Infrastructure y Api.
- SQL Server/EF Core, contexto `AppDbContext` e Identity genérico por GUID, registrados al configurar conexión.
- Enum inicial de estados y contratos de consulta/operación para reparto. Son contratos sin implementación: no hay consultas, asignaciones, endpoints ni lógica simulada.
- Documentación de arquitectura, contratos y acuerdos.

## Verificación

Compilación y pruebas: pendientes de ejecutar antes de publicar.

## Esquema y actualización

Esta base no agrega tablas de negocio ni migraciones; el contexto solo aporta el esquema estándar de Identity. No se ha conectado ni modificado la BD compartida. Después de integrar el primer cambio de esquema, el Agente 1 documentará aquí el nombre de migración, comandos de actualización y datos funcionales afectados.

Para tomar la rama inicial: `git fetch origin` y `git switch --track origin/feature/clean-architecture-foundation`.
