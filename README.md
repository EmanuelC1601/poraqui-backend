# Poraqui Backend

API de la plataforma de comida local, construida en .NET 9 con Clean Architecture.

## Compilar

```sh
dotnet build Poraqui.sln
dotnet test Poraqui.sln
```

## Ejecutar localmente

Configura la conexión en User Secrets; no guardes valores privados en archivos versionados:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada de SQL Server>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

La API no aplica migraciones al iniciar. Revisa los scripts y aplica las migraciones de forma explícita después de validar el esquema de destino. En entorno Development, OpenAPI se sirve en `/openapi/v1.json`. El hub autenticado de estados de pedido está en `/hubs/orders`; consulta [contratos](docs/api-contracts.md) para suscribirse y recuperar estado tras reconectar.

Consulta [arquitectura](docs/architecture.md), [contratos](docs/api-contracts.md) y [acuerdos del equipo](docs/team-work.md).
