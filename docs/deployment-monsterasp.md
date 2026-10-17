# Despliegue en MonsterASP.NET

Estado: instrucciones preparadas; la API aún no se ha publicado en un sitio MonsterASP.NET. Este repositorio apunta a .NET 9 y el proveedor anuncia soporte para ese runtime. Verifica el runtime habilitado para el sitio antes de publicar.

## Preparación del sitio

1. Crea o selecciona el sitio web y la base SQL Server desde el panel de MonsterASP.NET. Para producción, usa una base de datos destino independiente de la BD compartida de desarrollo y confirma que el sitio y la base pertenecen al mismo plan/entorno.
2. Usa Git Deploy con tipo **Build .NET app** y selecciona `src/Poraqui.Api/Poraqui.Api.csproj`, o publica la carpeta de salida de ese proyecto con Web Deploy/FTP. No elijas **Static** para la API.
3. Habilita `ASPNETCORE_ENVIRONMENT=Production` en **Websites → sitio → Scripting → Environment variables**.

MonsterASP.NET corre ASP.NET Core detrás de IIS y permite WebSockets/SignalR. El hosting `InProcess` es el recomendado; no configures arranque como EXE combinado con `OutOfProcess`.

## Variables privadas del sitio

En **Scripting → Environment variables**, agrega estos nombres y valores obtenidos de los servicios correspondientes. El panel aplica los cambios al reiniciar la aplicación. No guardes valores secretos en archivos `appsettings*.json`, `web.config` versionado, archivos `.publishSettings` ni en Git.

| Nombre | Propósito |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena **Local** de SQL Server para el sitio alojado. MonsterASP.NET distingue las cadenas Local y Remote; la Local es para sitios alojados en su misma infraestructura. |
| `Bootstrap__AdminKey` | Clave aleatoria privada de al menos 32 caracteres para crear el primer PlatformAdmin. Después de `POST /api/auth/bootstrap-admin`, elimina esta variable y reinicia la aplicación; el endpoint responderá 404 cuando no esté configurada y también rechaza administradores adicionales cuando ya existe uno. |
| `PrivateFiles__Root` | Ruta persistente y escribible por el proceso IIS, fuera de `wwwroot`, para comprobantes privados. Confirma con el hosting una ruta absoluta admitida por el plan y los permisos del Application Pool. |
| `DataProtection__KeysPath` | Opcional para una instancia; recomendado en ruta persistente para conservar códigos protegidos tras reciclar el proceso. Con varias instancias debe ser almacenamiento persistente compartido. |
| `TwilioVerify__ServiceSid` | Servicio Twilio Verify para SMS de confirmación telefónica. |
| `TwilioVerify__ApiKeySid` | Identificador de API key de Twilio Verify. |
| `TwilioVerify__ApiKeySecret` | Secreto de API key de Twilio Verify. |

ASP.NET Core interpreta `__` como separador de secciones (`ConnectionStrings__DefaultConnection` equivale a `ConnectionStrings:DefaultConnection`). Usa valores secretos guardados desde el panel del sitio. La cadena de la aplicación alojada debe ser la cadena Local del panel; la cadena pública del servidor es para conexiones remotas y requiere habilitar acceso remoto.

## Migraciones y activación

La API no aplica migraciones al iniciar. Antes de usar una base de producción, el responsable de migraciones debe revisar el destino, conciliar `__EFMigrationsHistory`, generar scripts SQL revisables, verificar que sean aditivos y preserven datos, y aplicarlos explícitamente. No confundas la base de desarrollo con la base destino de producción.

No ejecutes pruebas que inserten y limpien datos sobre la base compartida de desarrollo o producción. Las pruebas SQL de concurrencia usan `ConnectionStrings__SqlServerTest` y comparan su destino con `ConnectionStrings:DefaultConnection` desde User Secrets antes de ejecutarse; si coinciden, se omiten. En MonsterASP.NET, la conexión Local solo sirve desde un sitio en el hosting; las pruebas ejecutadas desde una estación externa requieren habilitar Remote access únicamente para la base aislada y guardar su cadena Remote en configuración local privada.

## Verificación posterior a publicación

- Solicita `/health/live` y confirma HTTP 200.
- Confirma que el entorno sea `Production`.
- Verifica en el hosting que Identity acceda a la base destino y que el almacenamiento privado pueda guardar y leer un comprobante de prueba.
- Valida WebSockets y el hub autenticado `/hubs/orders` desde un cliente autorizado.
- Configura Twilio Verify y valida un SMS con un número autorizado. Las pruebas automatizadas con proveedor simulado no prueban entrega real.
- OpenAPI solo se publica en Development; el snapshot local está versionado en `docs/openapi.json`.

## Guías oficiales consultadas

- [Publicar una aplicación desde GitHub](https://help.monsterasp.net/websites/git-deploy/github-repository)
- [Configurar variables de entorno](https://help.monsterasp.net/websites/configuration/environment-variables)
- [Obtener la cadena de conexión SQL Server](https://help.monsterasp.net/databases/mssql/connection-string)
- [Compatibilidad, WebSockets y SignalR](https://help.monsterasp.net/getting-started/first-steps/supported-technologies)
- [Modelo de hospedaje ASP.NET Core en IIS](https://help.monsterasp.net/websites/configuration/hosting-model)
