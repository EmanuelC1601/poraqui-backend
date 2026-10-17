# Handoff de desarrollo

## Responsabilidad y base

Un responsable mantiene todos los módulos, contratos, `AppDbContext`, migraciones, pruebas y despliegue. `main` es la base de integración. Trabaja en ramas `feature/*`, con PR por incremento; actualiza este handoff, `docs/project-status.md` y `docs/api-contracts.md`. OpenCode ya no participa; la asignación previa se conserva como antecedente.

## Estado Git

- `main` integra PR #1–#6 (`d39d265`).
- PR #7–#13 están integrados en ramas apiladas, fuera de `main`: #10 `fc68274`, #11/#12 `efa53d9`, #13 `634590c`.
- PR #14 está abierto desde `feature/phone-verification`; su pila contiene PR #15–#18 integrados en dicha rama. PR #19–#21 siguen después en ramas apiladas.
- Para mantener los PR encadenados, sincroniza primero cada rama base y revisa el diff efectivo del PR, no solo sus commits nuevos.

## SQL Server y migraciones

`ConnectionStrings:DefaultConnection` existe en User Secrets. Con `dotnet-ef 10.0.13`, EF conectó al SQL Server y encontró aplicadas nueve migraciones, la última `IncidentCommandIdempotency`; el modelo coincide con el snapshot. No se aplicaron migraciones en esta sesión. La API no migra al arrancar.

`ConnectionStrings:SqlServerTest` no está configurada. La prueba optativa de aceptación concurrente exige una base aislada llamada exactamente `PoraquiIntegrationTests`; crea y borra únicamente sus GUID de prueba. Configura la cadena solo para esa base separada. Nunca ejecutes esas pruebas contra la BD compartida ni borres, recrees o reinicies datos de desarrollo. Mantén secretos fuera de Git, consola y logs.

La rama incorpora `IncidentCommandIdempotency`, migración aditiva de una columna nullable e índice único filtrado. El historial EF muestra que ya está aplicada. No regenerar/aplicar migraciones sin revisar primero el historial de destino.

## Build, pruebas y configuración funcional

La rama actual usa .NET 10 / EF Core 10. Restaurar y verificar con:

```powershell
dotnet restore Poraqui.sln
dotnet build Poraqui.sln --no-restore
dotnet test Poraqui.sln --no-build --no-restore
```

Resultado actual: build 0 advertencias/errores; 24 pruebas aprobadas y una omitida porque falta `ConnectionStrings__SqlServerTest`. Las pruebas SQLite son aisladas y una comprueba traducción de SQL Server sin conexión real. La omisión impide afirmar que la aceptación simultánea ya esté verificada en SQL Server.

OTP usa las claves privadas `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid` y `TwilioVerify:ApiKeySecret`. La suite reemplaza Twilio por un provider falso; no ha enviado SMS real. Ver OpenAPI de Development en `/openapi/v1.json` y el recorrido HTTP en `docs/order-flow.md`.

## Próximos pasos

1. Sincronizar `feature/phone-verification` con la base actualizada de PR #13, resolver únicamente estados/documentación en conflicto y revisar PR #14.
2. Confirmar con build/test el cambio de OTP e idempotencia de flujo; resolver carreras de claves duplicadas de incidencias y validar con SQL Server aislado.
3. Revisar PR #19–#21 en orden y llevar la pila funcional completa a `main` por incrementos.
4. Probar migraciones/restricciones y carreras en `PoraquiIntegrationTests`, configurar Twilio si se probará SMS real, terminar almacenamiento privado y despliegue MonsterASP.NET.

## Antecedente

La organización inicial asignó el reparto a OpenCode. La decisión vigente es responsable único; no hay dependencia operativa con otro agente.
