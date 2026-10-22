# Despliegue en MonsterASP.NET

Estado: el artefacto IIS vigente se generó para `main` en el run [39649314179](https://github.com/EmanuelC1601/poraqui-backend/actions/runs/39649314179), para el commit `7307f5a0323c3892353ba1f69c96e8015aff8dc6`: `poraqui-api-iis-7307f5a0323c3892353ba1f69c96e8015aff8dc6` (5,686,800 bytes; vence 2026-11-04). El paquete no prueba un despliegue; la API aún no se ha publicado en un sitio MonsterASP.NET. El proyecto apunta a .NET 10 LTS. La documentación actual del proveedor admite versiones modernas de ASP.NET Core; selecciona el runtime moderno compatible con .NET 10 en Scripting antes de publicar. [Soporte de tecnologías](https://help.monsterasp.net/getting-started/first-steps/supported-technologies) · [Selección de runtime](https://help.monsterasp.net/websites/configuration/scripting).

## Preparación del sitio

1. Crea o selecciona el sitio web y la base SQL Server desde el panel de MonsterASP.NET. Para producción, usa una base de datos destino independiente de la BD compartida de desarrollo y confirma que el sitio y la base pertenecen al mismo plan/entorno.
2. Usa Git Deploy con tipo **Build .NET app** y selecciona `src/Poraqui.Api/Poraqui.Api.csproj`, o publica la carpeta de salida de ese proyecto con Web Deploy/FTP. No elijas **Static** para la API.
3. Habilita `ASPNETCORE_ENVIRONMENT=Production` en **Websites → sitio → Scripting → Environment variables**.

El workflow `.github/workflows/ci.yml` fija `ubuntu-24.04`, restaura, compila en Release y ejecuta la suite; las pruebas SQL Server se omiten en CI si no se configura un catálogo aislado privado. PR #86 integró la publicación y carga de un paquete IIS en cada `push` exitoso a `main`, con retención de 14 días. Para descargarlo, abre la ejecución de `backend-ci`, selecciona el artefacto al final de su resumen y descomprímelo para publicarlo con Web Deploy/FTP. El paquete no contiene configuración privada. La alternativa es Git Deploy de MonsterASP.NET con tipo **Build .NET app**, el repositorio público, rama `main` y el proyecto `src/Poraqui.Api/Poraqui.Api.csproj`.

MonsterASP.NET corre ASP.NET Core detrás de IIS y permite WebSockets/SignalR. El hosting `InProcess` es el recomendado; no configures arranque como EXE combinado con `OutOfProcess`.

## Variables privadas del sitio

En **Scripting → Environment variables**, agrega estos nombres y valores obtenidos de los servicios correspondientes. El panel aplica los cambios al reiniciar la aplicación. No guardes valores secretos en archivos `appsettings*.json`, `web.config` versionado, archivos `.publishSettings` ni en Git.

| Nombre | Propósito |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena **Local** de SQL Server para el sitio alojado. MonsterASP.NET distingue las cadenas Local y Remote; la Local es para sitios alojados en su misma infraestructura. |
| `Bootstrap__AdminKey` | Clave aleatoria privada de al menos 32 caracteres para crear el primer PlatformAdmin. Después de `POST /api/auth/bootstrap-admin`, elimina esta variable y reinicia la aplicación; el endpoint responderá 404 cuando no esté configurada y también rechaza administradores adicionales cuando ya existe uno. |
| `PrivateFiles__Root` | Ruta persistente y escribible por el proceso IIS, fuera de `wwwroot`, para comprobantes privados. Confirma con el hosting una ruta absoluta admitida por el plan y los permisos del Application Pool. |
| `DataProtection__KeysPath` | Obligatoria en Production: ruta persistente privada fuera de `wwwroot` para conservar códigos protegidos entre reinicios. Con varias instancias debe ser almacenamiento persistente compartido. |
| `TwilioVerify__ServiceSid` | Servicio Twilio Verify para SMS de confirmación telefónica. |
| `TwilioVerify__ApiKeySid` | Identificador de API key de Twilio Verify. |
| `TwilioVerify__ApiKeySecret` | Secreto de API key de Twilio Verify. |

ASP.NET Core interpreta `__` como separador de secciones (`ConnectionStrings__DefaultConnection` equivale a `ConnectionStrings:DefaultConnection`). Usa valores secretos guardados desde el panel del sitio. La cadena de la aplicación alojada debe ser la cadena Local del panel; la cadena pública del servidor es para conexiones remotas y requiere habilitar acceso remoto.

En `Production`, cuando hay conexión SQL configurada, la API exige `PrivateFiles__Root` y `DataProtection__KeysPath`, rechaza rutas dentro de `wwwroot` y crea/verifica ambos directorios al arrancar. Si falta una ruta o IIS no puede escribirla, el proceso falla al iniciar en vez de guardar comprobantes o claves en directorios locales no persistentes. La clave persistente protege el replay de códigos de recogida; con varias instancias comparte la ruta de claves. Esta validación no demuestra que el plan conserve las rutas tras reinicios; verifícalo con MonsterASP antes de recibir pedidos reales.

Para configuración local de desarrollo, guarda `ConnectionStrings:DefaultConnection` en User Secrets (no `appsettings*.json`):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena Local de la BD de desarrollo>" --project src/Poraqui.Api
```

Para pruebas mutables SQL, configura `ConnectionStrings:SqlServerTest` con la cadena de la BD aislada y ejecútalas contra esa base. No uses la cadena de test como `DefaultConnection`.

## Migraciones y activación

La API no aplica migraciones al iniciar. Antes de usar una base de producción, el responsable de migraciones debe revisar el destino, conciliar `__EFMigrationsHistory`, generar scripts SQL revisables, verificar que sean aditivos y preserven datos, y aplicarlos explícitamente. No confundas la base de desarrollo con la base destino de producción.

No ejecutes pruebas que inserten y limpien datos sobre la base compartida de desarrollo o producción. Las pruebas SQL de concurrencia usan `ConnectionStrings__SqlServerTest` y comparan su destino con `ConnectionStrings:DefaultConnection` desde User Secrets antes de ejecutarse; si coinciden, se omiten. En MonsterASP.NET, la conexión Local solo sirve desde un sitio en el hosting; las pruebas ejecutadas desde una estación externa requieren habilitar Remote access únicamente para la base aislada y guardar su cadena Remote en configuración local privada.

## Verificación posterior a publicación

- Solicita `/health/live` y confirma HTTP 200 para validar que el proceso está activo.
- Solicita `/health/ready` y confirma HTTP 200 para validar conectividad con SQL Server; HTTP 503 indica que falta configuración de SQL o la base no está disponible. Este endpoint no aplica migraciones ni verifica servicios externos.
- Confirma que el entorno sea `Production`.
- Verifica en el hosting que Identity acceda a la base destino y que el almacenamiento privado pueda guardar y leer un comprobante de prueba.
- Valida WebSockets y el hub autenticado `/hubs/orders` desde un cliente autorizado.
- Configura Twilio Verify y valida un SMS con un número autorizado. Las pruebas automatizadas con proveedor simulado no prueban entrega real.
- OpenAPI solo se publica en Development; el snapshot local está versionado en `docs/openapi.json`.

## Comprobación local del artefacto

Se verificó con SDK .NET 10.0.402:

```powershell
dotnet publish src/Poraqui.Api/Poraqui.Api.csproj --configuration Release --no-restore --output work/publish
```

El output contiene `Poraqui.Api.dll`, `web.config`, `.deps.json` y `.runtimeconfig.json`. `work/publish` está ignorado por Git. Esta compilación comprueba el paquete ASP.NET Core, pero no acredita el arranque en IIS, el runtime asignado al sitio, sus rutas persistentes ni el acceso a las variables privadas del hosting.

## Guías oficiales consultadas

- [Publicar una aplicación desde GitHub](https://help.monsterasp.net/websites/git-deploy/github-repository)
- [Configurar variables de entorno](https://help.monsterasp.net/websites/configuration/environment-variables)
- [Obtener la cadena de conexión SQL Server](https://help.monsterasp.net/databases/mssql/connection-string)
- [Compatibilidad, WebSockets y SignalR](https://help.monsterasp.net/getting-started/first-steps/supported-technologies)
- [Modelo de hospedaje ASP.NET Core en IIS](https://help.monsterasp.net/websites/configuration/hosting-model)

## Última comprobación del paquete local

El paquete local `work/publish` se regeneró el 2026-10-22 desde el código de `main` (`9bceedd`) con SDK .NET 10.0.402 mediante `dotnet publish src/Poraqui.Api/Poraqui.Api.csproj --configuration Release --no-restore --output work/publish`. Se confirmó la presencia de `Poraqui.Api.dll`, `.deps.json`, `.runtimeconfig.json` y `web.config`; son 36 archivos, 9,553,194 bytes en total, y el directorio está ignorado por Git. Los `appsettings` publicados contienen solo `Logging` y `AllowedHosts`; no contienen claves `ConnectionStrings`, `Bootstrap`, `PrivateFiles` ni `TwilioVerify`. Esta comprobación de publicación no ejecutó la suite ni despliega el sitio.

La ejecución vigente confirmada de `main`, [39717518909](https://github.com/EmanuelC1601/poraqui-backend/actions/runs/39717518909), pasó CI para el commit `9bceedd43f34d9b7d21d69e7c289c3a33176ac2f` y generó `poraqui-api-iis-9bceedd43f34d9b7d21d69e7c289c3a33176ac2f` (5,688,862 bytes; vence el 2026-11-05). En la revalidación local de esta versión de código, la suite Release aprobó 51 pruebas y omitió nueve pruebas SQL Server porque la configuración privada aislada fue aplazada. El artefacto se descarga desde el resumen de la ejecución. Esto verifica compilación, pruebas automatizadas y publicación en el runner, no el arranque en IIS ni las rutas persistentes de MonsterASP.NET. No se ha publicado el sitio.
