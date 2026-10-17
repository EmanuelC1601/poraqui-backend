# Handoff de desarrollo

## Base integrada

`main` es la base de integración y apunta al merge commit `a38ed4e` (PR #2); PR #1 (`e346995`) aportó la arquitectura inicial, entidades de comercio/pedido y mapeos EF. La rama `feature/clean-architecture-foundation` se conserva como antecedente. El flujo actual tiene un único responsable; OpenCode ya no tiene entregas asignadas.

## Incremento en curso

PR #2 integró Identity API, roles/policies y registro/configuración/consulta pública de negocios. Los propietarios solo pueden consultar y modificar sus negocios; el test de API cubre denegación entre propietarios. El bootstrap del primer administrador requiere `Bootstrap:AdminKey` privado. `feature/catalog-pricing`, basada en el nuevo `main`, agrega CRUD básico de productos/zonas/tarifas y creación de pedido con precios calculados desde DB. El incremento está en implementación local, aún sin commit/PR. No hay migraciones creadas ni aplicadas.

## Verificaciones

El PR #2 pasó `dotnet build Poraqui.sln` (0 advertencias/errores) y `dotnet test Poraqui.sln` (10/10, SQLite en memoria aislado); GitHub reportó cero check runs y el PR se integró como `a38ed4e`. En la rama actual `dotnet build Poraqui.sln` pasa (0 advertencias/errores) y `dotnet test Poraqui.sln` pasa 11/11 (SQLite aislado), incluyendo precios autoritativos y permiso por recurso de catálogo. Estas pruebas no sustituyen SQL Server. No hay migraciones creadas ni aplicadas. El portal MonsterASP respondió a consulta de solo lectura y reportó cero tablas de usuario.

## Configuración SQL privada

La aplicación requiere `ConnectionStrings:DefaultConnection` (variable `ConnectionStrings__DefaultConnection`). Está ausente en la configuración privada local comprobada. Configurar sin compartir el valor:

```powershell
dotnet user-secrets init --project src/Poraqui.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
```

No usar valores del chat. La API no ejecuta migraciones al arrancar. Cuando esté configurada, primero probar conexión y revisar la migración generada; aplicarla solo si preserva datos y no requiere decisiones funcionales.

## Pendientes inmediatos

Opciones de producto y horarios; consulta/historial de pedidos y transiciones; aceptación/rechazo del negocio; revisión manual de transferencias y evidencia privada; reparto propio y externo; notificaciones, sincronización, incidencias, métricas, despliegue y recorrido reproducible. El ledger verificable está en `docs/project-status.md`.

## Actualización para la siguiente persona/entorno

```bash
git fetch origin
git switch main
git pull --ff-only
git switch -c feature/<nombre>
```

La rama de trabajo actual se basa en `main`. Al integrar cada PR: actualizar con `git switch main && git pull --ff-only`, y regenerar/revisar cualquier migración antes de aplicarla. Los siguientes incrementos deben actualizar este handoff y el ledger.
