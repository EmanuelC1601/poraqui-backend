# Handoff de desarrollo

## Responsable, base e integración

El proyecto tiene un responsable único para todos los módulos, contratos, persistencia, migraciones y despliegue. `main` es la base de integración. Cada incremento se prepara en `feature/*`, se verifica localmente y se publica como PR pequeño cuando las dependencias de la pila lo permiten. OpenCode ya no participa; su asignación anterior de reparto se conserva como antecedente.

PR #1–#6 están en `main` (`d39d265`). PR #7–#21 se integraron en sus ramas apiladas, no en `main`. La rama actual de revisión integral `feature/backend-integration-verified` parte de `origin/feature/courier-assignment-idempotency` (`236fd19`) y contiene la pila completa publicada. En GitHub se confirmó que PR #14, #19, #20 y #21 están cerrados como merged en las ramas base de sus pilas. El siguiente paso es revisar el diff de esta rama contra `main`, abrir PR y mantener `main` como destino.

## Estado de build y pruebas

En la rama de integración:

```powershell
dotnet restore Poraqui.sln
dotnet build Poraqui.sln --no-restore
dotnet test Poraqui.sln --no-build --no-restore
```

Resultado verificado el 2026-10-17: build .NET 10 limpio (0 advertencias/errores), 24 pruebas aprobadas y 2 omitidas de 26. Las omitidas son aceptación simultánea de ofertas externas y asignación interna simultánea porque falta `ConnectionStrings__SqlServerTest`. La suite funcional usa SQLite aislado y no demuestra las restricciones/concurrencia del proveedor SQL Server.

## SQL Server y migraciones

`ConnectionStrings:DefaultConnection` está en User Secrets local. EF conectó al SQL Server de desarrollo y enumeró 12 migraciones aplicadas hasta `CourierAssignmentCommandIdempotency`; `migrations has-pending-model-changes` informó que no hay cambios de modelo. No se aplicaron migraciones durante esta revisión. La API no migra al arrancar.

La clave `ConnectionStrings:SqlServerTest` no está configurada. Para ejecutar las pruebas SQL Server, configura esa clave solo con una base aislada `PoraquiIntegrationTests`; esas pruebas aplican migraciones y limpian filas propias de prueba. Nunca la apuntes a la BD compartida. En desarrollo compartido no borrar, recrear ni reiniciar datos; usar registros identificables de prueba.

Las futuras migraciones pertenecen al responsable único. Revisa el SQL generado y el historial remoto antes de aplicar; no regeneres ni apliques migraciones por defecto.

## Configuración externa pendiente

- SMS real: claves privadas `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid`, `TwilioVerify:ApiKeySecret`; la suite usa proveedor/transporte falso.
- Despliegue: `ConnectionStrings__DefaultConnection`, `Bootstrap__AdminKey`, `PrivateFiles__Root`, `DataProtection__KeysPath`, credenciales Twilio y soporte WebSockets en MonsterASP.NET. Los archivos y anillo de Data Protection deben persistir fuera de `wwwroot`.
- Configuración local: `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api`. No incluir la cadena en terminal compartida, documentación o Git.

## Recorrido y límites

Seguir [order-flow.md](order-flow.md) para reproducir el pedido de punta a punta. OpenAPI en Development: `/openapi/v1.json`. Quedan pruebas de concurrencia SQL Server, reconexión SignalR desde cliente real, SMS real y publicación en MonsterASP.NET. Las tarifas usan zonas configuradas: no hay GPS ni distancias calculadas, transferencias automáticas ni frontend.
