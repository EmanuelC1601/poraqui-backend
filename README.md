# Poraqui Backend

API de la plataforma de comida local, construida en .NET 9 con Clean Architecture. `main` es la base de integración y el proyecto tiene un único responsable.

## Compilar y probar

```sh
dotnet restore Poraqui.sln
dotnet build Poraqui.sln
dotnet test Poraqui.sln
```

Las pruebas de API usan SQLite en memoria y archivos temporales. No validan concurrencia ni sintaxis/índices del servidor SQL.

## Ejecutar localmente

Guarda la conexión en User Secrets; no añadas valores privados a archivos versionados:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada de SQL Server>" --project src/Poraqui.Api
dotnet user-secrets set "Bootstrap:AdminKey" "<clave privada aleatoria>" --project src/Poraqui.Api
dotnet user-secrets set "PrivateFiles:Root" "<ruta privada fuera del web root>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

La API no ejecuta migraciones al iniciar. Revisa scripts SQL y aplica migraciones explícitamente solo tras reconciliar el historial y comprobar el destino. Para pruebas SQL Server, configura `ConnectionStrings:SqlServerTest` (variable de entorno `ConnectionStrings__SqlServerTest`) con el catálogo aislado `PoraquiIntegrationTests`; las pruebas de concurrencia se omiten si falta o apunta a otro catálogo. En Development, OpenAPI está en `/openapi/v1.json`; snapshot en [docs/openapi.json](docs/openapi.json). El hub autenticado está en `/hubs/orders`.

La verificación telefónica usa Twilio Verify. Configura en User Secrets o el gestor privado de secretos `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid` y `TwilioVerify:ApiKeySecret`. La API limita los endpoints de inicio y confirmación a cinco solicitudes por IP cada diez minutos. Nunca guardes esos valores en archivos versionados.

Consulta [arquitectura](docs/architecture.md), [contratos](docs/api-contracts.md), [estado](docs/project-status.md), [flujo reproducible](docs/order-flow.md) y [flujo de desarrollo](docs/team-work.md).

Preparación de hosting: [MonsterASP.NET](docs/deployment-monsterasp.md). La guía aún no representa una publicación realizada.
