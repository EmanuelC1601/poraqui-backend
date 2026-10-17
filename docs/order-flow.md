# Recorrido reproducible de pedido

Este recorrido requiere `ConnectionStrings:DefaultConnection`, `Bootstrap:AdminKey` y `PrivateFiles:Root` privados. Para despliegue con más de una instancia, configura `DataProtection:KeysPath` a almacenamiento privado persistente y compartido entre instancias. No uses credenciales publicadas en conversaciones.

## Ejecutar

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
dotnet user-secrets set "Bootstrap:AdminKey" "<clave privada aleatoria de al menos 32 caracteres>" --project src/Poraqui.Api
dotnet user-secrets set "PrivateFiles:Root" "<ruta privada fuera del web root>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

El `UserSecretsId` ya está configurado. La API no migra al iniciar. Revisa scripts SQL y concilia `__EFMigrationsHistory` antes de aplicar migraciones con `dotnet tool run dotnet-ef database update --project src/Poraqui.Infrastructure --startup-project src/Poraqui.Api`. En Development se sirve OpenAPI en `/openapi/v1.json`.
