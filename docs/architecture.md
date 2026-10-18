# Arquitectura

La solución usa .NET 10 LTS y Clean Architecture. `main` es la rama predeterminada y base de integración. El backend tiene un responsable único; cada incremento se desarrolla en una rama `feature/*` y se integra mediante PR. Proyectos en `src/`:

- **Poraqui.Domain**: conceptos y reglas del dominio; no depende de otros proyectos.
- **Poraqui.Application**: casos de uso y contratos; depende de Domain.
- **Poraqui.Infrastructure**: EF Core, SQL Server, Identity y adaptadores; depende de Application y Domain.
- **Poraqui.Api**: composición HTTP y configuración; depende de Application e Infrastructure.

La dependencia de ejecución apunta hacia el dominio. La API registra persistencia e Identity solo cuando existe `ConnectionStrings:DefaultConnection`, para permitir compilar y arrancar sin credenciales. Configura el secreto en variables de entorno (`ConnectionStrings__DefaultConnection`) o en User Secrets; no guardes credenciales en el repositorio. El 17 de octubre de 2026 esta clave estaba en User Secrets y permitió consultar en modo de lectura el historial de migraciones SQL Server.

El contexto EF compartido es `AppDbContext` en Infrastructure. El responsable del backend genera y revisa migraciones, y las aplica a la BD de desarrollo tras verificar que son aditivas y preservan los datos. No usar `EnsureDeleted`, reinicios ni migraciones destructivas en esa BD. La API no ejecuta migraciones automáticamente al iniciar.

EF tooling se fija en `.config/dotnet-tools.json`; ejecutar `dotnet tool restore` para usarlo. La factoría de diseño permite crear migraciones sin conexión privada usando una BD local de diseño que nunca se conecta al servidor compartido. La aplicación de migraciones debe usar `ConnectionStrings:DefaultConnection` privada configurada. Comprobantes de transferencia quedan en almacenamiento privado; configura `PrivateFiles:Root` a una ruta persistente fuera de `wwwroot` para el despliegue.

## Publicación

MonsterASP.NET debe ejecutar `src/Poraqui.Api/Poraqui.Api.csproj` con runtime ASP.NET Core compatible con .NET 10 y recibir `ConnectionStrings__DefaultConnection` desde configuración segura del hosting. Sigue [la guía de despliegue](deployment-monsterasp.md) para runtime, variables privadas, almacenamiento y publicación. Aún no se ha publicado.
