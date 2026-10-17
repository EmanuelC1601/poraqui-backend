# Handoff de desarrollo

## Base integrada

`main` es la base de integración y apunta al merge commit `962e74a` (PR #3); PR #2 (`a38ed4e`) integró Identity/negocio y PR #1 (`e346995`) aportó arquitectura inicial y entidades/mapeos de comercio/pedido. La rama `feature/clean-architecture-foundation` se conserva como antecedente. El flujo actual tiene un único responsable; OpenCode ya no tiene entregas asignadas.

## Incremento en curso

PR #2 integró Identity API/negocio (`a38ed4e`); PR #3 integró catálogo básico y pedidos a precios del servidor (`962e74a`). `feature/order-lifecycle`, basada en ese `main`, implementa comprobante privado, revisión manual, decisión de negocio, preparación, courier interno, pago recibido al recoger, contacto autorizado, códigos e historial. Incluye recorrido API completo y prueba de decisión concurrente en SQLite. Rama local sin commit/PR. Se generó la migración EF revisable `InitialCommerceAndIdentity` y el script SQL idempotente; `has-pending-model-changes` reporta modelo sincronizado. No se aplicó. Bootstrap admin requiere `Bootstrap:AdminKey` privado.

## Verificaciones

PR #2 pasó build (0 advertencias/errores) y 10/10 pruebas aisladas; PR #3 pasó build y 11/11 pruebas aisladas. GitHub no tenía check runs configurados; ambos PRs quedaron integrados en main. En la rama actual `dotnet build Poraqui.sln` pasa sin advertencias/errores y `dotnet test Poraqui.sln` pasa 13/13 en SQLite en memoria, con comprobantes en directorio temporal aislado. `dotnet ef migrations has-pending-model-changes` confirma snapshot alineado y script generado desde código con CREATE de esquema nuevo, sin operaciones destructivas en Up. No hay conexión de la aplicación a SQL Server ni migración aplicada. El portal MonsterASP respondió a consulta de solo lectura y reportó cero tablas de usuario.

## Configuración SQL privada

La aplicación requiere `ConnectionStrings:DefaultConnection` (variable `ConnectionStrings__DefaultConnection`). Está ausente en la configuración privada local comprobada. Configurar sin compartir el valor:

El `UserSecretsId` local del proyecto ya está declarado; agrega la clave sin imprimirla ni compartirla:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
```

No usar valores del chat. La API no ejecuta migraciones al arrancar. Cuando esté configurada, primero probar conexión y revisar la migración generada; aplicarla solo si preserva datos y no requiere decisiones funcionales.

## Pendientes inmediatos

Opciones de producto y horarios; datos de transferencia del negocio; reparto externo; notificaciones SignalR, sincronización/idempotencia, incidencias/reembolsos, métricas y despliegue. El estado verificable está en `docs/project-status.md`; el recorrido API está en `docs/order-flow.md`.

## Actualización para la siguiente persona/entorno

```bash
git fetch origin
git switch main
git pull --ff-only
git switch -c feature/<nombre>
```

La rama de trabajo actual se basa en `main`. Al integrar cada PR: actualizar con `git switch main && git pull --ff-only`, y regenerar/revisar cualquier migración antes de aplicarla. Los siguientes incrementos deben actualizar este handoff y el ledger.
