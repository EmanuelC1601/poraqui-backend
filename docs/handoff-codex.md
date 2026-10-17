# Handoff de desarrollo

## Base integrada

`main` es la base de integración y apunta al merge commit `e346995` (PR #1). El PR incorporó la arquitectura inicial, entidades de comercio/pedido y mapeos EF. La rama `feature/clean-architecture-foundation` se conserva como antecedente. El flujo actual tiene un único responsable; OpenCode ya no tiene entregas asignadas.

## Incremento en curso

La rama `feature/functional-order-flow` añade Identity API, roles/policies y registro/configuración/consulta pública de negocios. Los propietarios solo pueden consultar y modificar sus negocios; el test de API cubre denegación entre propietarios. El bootstrap del primer administrador requiere `Bootstrap:AdminKey`. No hay migraciones creadas ni aplicadas. Este incremento sigue local y no tiene commit ni PR publicados.

## Verificaciones

`dotnet test Poraqui.sln` compila los cuatro proyectos y pasa 10/10 pruebas usando SQLite en memoria y una base aislada por proceso de test. Estas pruebas no sustituyen la validación de SQL Server. No se han ejecutado pruebas de migración ni migraciones. Una consulta de solo lectura en el portal MonsterASP confirmó que la BD de desarrollo respondía y no contenía tablas de usuario; no modifica datos.

## Configuración SQL privada

La aplicación requiere `ConnectionStrings:DefaultConnection` (variable `ConnectionStrings__DefaultConnection`). Está ausente en la configuración privada local comprobada. Configurar sin compartir el valor:

```powershell
dotnet user-secrets init --project src/Poraqui.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
```

No usar valores del chat. La API no ejecuta migraciones al arrancar. Cuando esté configurada, primero probar conexión y revisar la migración generada; aplicarla solo si preserva datos y no requiere decisiones funcionales.

## Pendientes inmediatos

Catálogo/variantes/disponibilidad, zonas y tarifas con permisos; creación de pedido con precios del servidor; aceptación/rechazo y estados; revisión manual de transferencias; reparto propio y externo; notificaciones, sincronización, incidencias, métricas, despliegue y recorrido reproducible. El ledger verificable está en `docs/project-status.md`.

## Actualización para la siguiente persona/entorno

```bash
git fetch origin
git switch main
git pull --ff-only
git switch -c feature/<nombre>
```

La rama de trabajo actual se basa en `main`. Al integrar cada PR: actualizar con `git switch main && git pull --ff-only`, y regenerar/revisar cualquier migración antes de aplicarla. Los siguientes incrementos deben actualizar este handoff y el ledger.
