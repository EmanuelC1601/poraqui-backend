# Estado y handoff de desarrollo

## Base publicada

El trabajo de dos agentes fue sustituido por un flujo de responsable único. Históricamente, OpenCode iba a realizar reparto; no hay entregas pendientes de OpenCode. `main` y `origin/main` parten de `a5d9dd8`. La rama `feature/comercio-pedidos` conserva el incremento inicial y el PR #1 está bajo revisión; sus hallazgos se corrigen antes de integrar.

## Incluido

- Solución .NET 9 con proyectos Domain, Application, Infrastructure y Api.
- SQL Server/EF Core, contexto `AppDbContext` e Identity genérico por GUID, registrados al configurar conexión.
- Enum inicial de estados y contratos de consulta/operación para reparto. Son provisionales, sin consultas, asignaciones, endpoints ni lógica de reparto.
- Reglas de dominio iniciales de comercio y pedidos, mapeos EF e historial de revisión manual, en la rama `feature/comercio-pedidos`. Aún faltan casos de uso, autorización, endpoints y migración.
- Documentación de arquitectura, contratos y acuerdos.

## Verificación

El último incremento revisado compila (`dotnet build Poraqui.sln`, 0 advertencias y errores) y `dotnet test Poraqui.sln` pasa 8/8 (reglas de dominio y persistencia SQLite en memoria). No hay check runs de CI en el PR. EF Core SQL Server, `AppDbContext` e Identity base están configurados. No se han creado ni aplicado migraciones.

## Esquema y actualización

La rama agrega modelos de negocio, catálogo, zonas, tarifas, pedidos y revisión de transferencias a `AppDbContext`. El portal SQL Server confirmó que `db74183` responde y tiene 0 tablas de usuario, usando una consulta de solo lectura. La configuración privada local sigue sin `ConnectionStrings:DefaultConnection` (variable `ConnectionStrings__DefaultConnection`), así que la conexión desde la aplicación y las migraciones de EF aún no se probaron. No se modificó la BD. La API no aplica migraciones al iniciar. El único responsable genera/revisa migraciones y solo las aplica tras validar que no eliminan datos.

Para comenzar un incremento: `git fetch origin`, `git switch main`, `git pull --ff-only`, `git switch -c feature/<nombre>`. Integrar solo mediante PR y después actualizar con `git switch main; git pull --ff-only`. La rama base aún no tiene migraciones de negocio. El recorrido funcional y reparto siguen pendientes según `docs/project-status.md`.

Para configurar la conexión localmente, crea User Secrets con `dotnet user-secrets init --project src/Poraqui.Api` y agrega `ConnectionStrings:DefaultConnection` con `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api`. La clave no debe guardarse en Git.

`main` es la rama predeterminada de GitHub.
