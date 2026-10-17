# Poraqui Backend

API de la plataforma de comida local, construida en .NET 10 LTS con Clean Architecture.

## Compilar

```sh
dotnet build Poraqui.sln
dotnet test Poraqui.sln
```

La solución apunta a `net10.0` y utiliza EF Core 10. Usa el SDK .NET 10 para compilar, ejecutar pruebas y restaurar `dotnet-ef`.

## Ejecutar localmente

Configura la conexión en User Secrets; no guardes valores privados en archivos versionados:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada de SQL Server>" --project src/Poraqui.Api
dotnet user-secrets set "TwilioVerify:ServiceSid" "<SID del servicio Verify>" --project src/Poraqui.Api
dotnet user-secrets set "TwilioVerify:ApiKeySid" "<SID de API Key>" --project src/Poraqui.Api
dotnet user-secrets set "TwilioVerify:ApiKeySecret" "<secreto de API Key>" --project src/Poraqui.Api
dotnet run --project src/Poraqui.Api
```

Configura los tres valores Twilio para que los endpoints de verificación telefónica envíen y validen SMS OTP. En despliegue, configura los mismos nombres como variables privadas de entorno (`TwilioVerify__ServiceSid`, `TwilioVerify__ApiKeySid`, `TwilioVerify__ApiKeySecret`) en MonsterASP.NET. Nunca guardes estos valores en `appsettings*.json` versionados.

La API no aplica migraciones al iniciar. Revisa los scripts y aplica las migraciones de forma explícita después de validar el esquema de destino. En entorno Development, OpenAPI se sirve en `/openapi/v1.json`. El hub autenticado de estados de pedido está en `/hubs/orders`; consulta [contratos](docs/api-contracts.md) para suscribirse y recuperar estado tras reconectar.

Consulta [arquitectura](docs/architecture.md), [contratos](docs/api-contracts.md) y [acuerdos del equipo](docs/team-work.md).

## Despliegue en MonsterASP.NET

Publica la API con `dotnet publish src/Poraqui.Api/Poraqui.Api.csproj -c Release -o ./publish` y despliega el contenido de `publish` en el sitio ASP.NET Core. En el panel del sitio, configura `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection` con la cadena privada de la base del mismo hosting y los valores privados de Twilio. Usa la sección Environment Variables para evitar incluir secretos en el repositorio. Configura también `PrivateFiles__Root` en una ruta persistente que no sea servida como contenido web para almacenar comprobantes. Configura `DataProtection__KeysPath` como carpeta persistente, privada y compartida por las instancias de la API; así se pueden descifrar los códigos de recepción guardados para reintentos de creación. Configura WebSockets para SignalR.

La API no ejecuta migraciones al arrancar. Revisa los scripts SQL de la versión que despliegas y aplica las migraciones de forma explícita en la base elegida. OpenAPI se publica en `/openapi/v1.json` en Development; no habilites Development para un sitio público.

## Recorrido reproducible del pedido

El recorrido automatizado de comercio, pago manual y repartidor propio se puede repetir con:

```powershell
dotnet test tests/Poraqui.Domain.Tests --filter FullyQualifiedName~Manual_payment_and_internal_delivery_flow_preserves_server_pricing_and_history
```

La prueba ejecuta la API con TestServer y SQLite temporal. Registra clientes y negocios, configura transferencia, catálogo, zona y tarifa, crea un pedido cuyo precio calcula el servidor, carga un comprobante de prueba, revisa el pago, acepta y prepara el pedido, asigna un repartidor, registra el pago del negocio al recoger, inicia el recorrido por WhatsApp y confirma la entrega con el código. Verifica permisos cruzados, importes, códigos, historial y estado final. No utiliza la BD de desarrollo ni envía SMS reales.

La secuencia HTTP equivalente está documentada en [contratos API](docs/api-contracts.md). OpenAPI lista actualmente 61 rutas bajo `/openapi/v1.json` en Development.

## Prueba de concurrencia SQL Server

La aceptación simultánea de ofertas tiene una prueba optativa que aplica las migraciones y crea/elimina únicamente filas de prueba en una base aislada llamada `PoraquiIntegrationTests`. Configura `ConnectionStrings__SqlServerTest` en la sesión local de PowerShell con la cadena privada de esa base y ejecuta:

```powershell
$env:ConnectionStrings__SqlServerTest = "<cadena privada de PoraquiIntegrationTests>"
dotnet test tests/Poraqui.Domain.Tests --filter FullyQualifiedName~SqlServerExternalOfferConcurrencyTests
```

La prueba no se ejecuta si la variable falta, y se omite si el nombre de la base no coincide exactamente con `PoraquiIntegrationTests`. Nunca apuntes esta cadena a la base compartida de desarrollo. Las migraciones de la API tampoco se aplican al iniciar.
