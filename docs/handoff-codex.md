# Handoff Codex → OpenCode

## Base publicada

`main` y `origin/main` apuntan a `a5d9dd8`, conservando `origin/feature/clean-architecture-foundation`. `main` es la rama predeterminada y base compartida de integración. Codex trabaja en `feature/comercio-pedidos`, creada desde `main`; commits publicados `6ea7f8a` y `9897824`. PR abierto contra `main`: [#1 Add initial commerce and order domain](https://github.com/EmanuelC1601/poraqui-backend/pull/1).

## Incluido

- Solución .NET 9 con proyectos Domain, Application, Infrastructure y Api.
- SQL Server/EF Core, contexto `AppDbContext` e Identity genérico por GUID, registrados al configurar conexión.
- Enum inicial de estados y contratos de consulta/operación para reparto. Son contratos sin implementación: no hay consultas, asignaciones, endpoints ni lógica simulada.
- Reglas de dominio iniciales de comercio y pedidos, mapeos EF e historial de revisión manual, en la rama `feature/comercio-pedidos`. Aún faltan casos de uso, autorización, endpoints y migración.
- Documentación de arquitectura, contratos y acuerdos.

## Verificación

La solución .NET 9 con proyectos Domain, Application, Infrastructure y Api compila (`dotnet build Poraqui.sln`, 0 advertencias y 0 errores). `dotnet test Poraqui.sln` pasa con 8/8: siete reglas de dominio y una prueba de persistencia SQLite en memoria. EF Core SQL Server, `AppDbContext` e Identity están configurados. Los contratos de reparto permanecen declarados, sin implementación. No se han creado ni aplicado migraciones.

## Esquema y actualización

La rama de trabajo agrega al contexto EF los modelos de negocio, catálogo, cobertura, tarifas, pedidos y revisión de transferencia. Aún no se generaron migraciones. La conexión no se pudo probar porque no está configurada la clave privada `ConnectionStrings:DefaultConnection` (variable `ConnectionStrings__DefaultConnection`) en entorno, User Secrets ni `appsettings.Development.json` de la copia de Codex. No se ha modificado la BD compartida. La API no aplica migraciones al iniciar. Agente 1 conserva la responsabilidad exclusiva de generarlas y aplicarlas después de integrar ambos cambios de esquema.

Para iniciar desde la base: `git fetch origin` y `git switch -c feature/reparto --track origin/main`. Para actualizar una rama existente compartida: `git fetch origin` y `git merge origin/main` (o rebase antes de publicar cambios propios). Para probar el incremento de Codex antes de que se integre: `git fetch origin` y `git merge origin/feature/comercio-pedidos`. No hay actualización de esquema pendiente: todavía no hay migraciones. Las firmas del contrato de reparto permanecen estables en este incremento; coordinar cambios de DTO, estado o método con Codex antes de implementarlos.

Para configurar la conexión localmente, crea User Secrets con `dotnet user-secrets init --project src/Poraqui.Api` y agrega `ConnectionStrings:DefaultConnection` con `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api`. La clave no debe guardarse en Git.

La rama predeterminada de GitHub está configurada como `main`.
