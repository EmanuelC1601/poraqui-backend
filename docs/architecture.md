# Arquitectura

La solución usa .NET 9 y Clean Architecture. `main` es la rama predeterminada y base de integración. El backend tiene un responsable único; cada incremento se desarrolla en una rama `feature/*` y se integra mediante PR. Proyectos en `src/`:

- **Poraqui.Domain**: conceptos y reglas del dominio; no depende de otros proyectos.
- **Poraqui.Application**: casos de uso y contratos; depende de Domain.
- **Poraqui.Infrastructure**: EF Core, SQL Server, Identity y adaptadores; depende de Application y Domain.
- **Poraqui.Api**: composición HTTP y configuración; depende de Application e Infrastructure.

La dependencia de ejecución apunta hacia el dominio. La API registra persistencia e Identity solo cuando existe `ConnectionStrings:DefaultConnection`, para permitir compilar y arrancar sin credenciales. Configura el secreto en variables de entorno (`ConnectionStrings__DefaultConnection`) o en secretos de usuario; no guardes credenciales en el repositorio. La conexión privada no estaba disponible en la copia de Codex al 17 de octubre de 2026; se requiere `ConnectionStrings:DefaultConnection` para comprobarla.

El contexto EF compartido es `AppDbContext` en Infrastructure. El responsable del backend genera y revisa migraciones, y las aplica a la BD de desarrollo tras verificar que son aditivas y preservan los datos. No usar `EnsureDeleted`, reinicios ni migraciones destructivas en esa BD. La API no ejecuta migraciones automáticamente al iniciar.

## Publicación

MonsterASP.NET debe ejecutar `src/Poraqui.Api/Poraqui.Api.csproj` con runtime .NET 9 y recibir `ConnectionStrings__DefaultConnection` desde configuración segura del hosting. La cadena para SQL Server debe tener cifrado TLS habilitado y confiar solo en el certificado configurado por el proveedor.
