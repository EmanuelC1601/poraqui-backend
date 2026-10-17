# Arquitectura

La solución usa .NET 9 y Clean Architecture. Proyectos en `src/`:

- **Poraqui.Domain**: conceptos y reglas del dominio; no depende de otros proyectos.
- **Poraqui.Application**: casos de uso y contratos; depende de Domain.
- **Poraqui.Infrastructure**: EF Core, SQL Server, Identity y adaptadores; depende de Application y Domain.
- **Poraqui.Api**: composición HTTP y configuración; depende de Application e Infrastructure.

La dependencia de ejecución apunta hacia el dominio. La API registra persistencia e Identity solo cuando existe `ConnectionStrings:DefaultConnection`, para permitir compilar y arrancar sin credenciales. Configura el secreto en variables de entorno (`ConnectionStrings__DefaultConnection`) o en secretos de usuario; no guardes credenciales en el repositorio.

El contexto EF compartido es `AppDbContext` en Infrastructure. El Agente 1 es el único propietario de migraciones y de aplicarlas a la BD de desarrollo, después de integrar los cambios de ambos agentes. No usar `EnsureDeleted`, reinicios ni migraciones destructivas en esa BD.

## Publicación

MonsterASP.NET debe ejecutar `src/Poraqui.Api/Poraqui.Api.csproj` con runtime .NET 9 y recibir `ConnectionStrings__DefaultConnection` desde configuración segura del hosting. La cadena para SQL Server debe tener cifrado TLS habilitado y confiar solo en el certificado configurado por el proveedor.
