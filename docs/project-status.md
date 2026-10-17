# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` es la base única de integración. PR #22–#26 están integrados hasta `1d75577`. OpenCode ya no participa. El responsable único implementa ramas `feature/*`, prepara PRs pequeños, verifica y actualiza este ledger, los contratos y el handoff.

## Ledger

| Estado | Función | Evidencia |
|---|---|---|
| Integrado | Arquitectura, Identity, permisos de negocio, catálogo con opciones, zonas/tarifas, precio de servidor, comprobante privado, revisión manual, pedido propio/externo, códigos, pago separado al recoger, historial, sync offline, SignalR, horario semanal, cancelaciones, incidencias y reembolsos manuales | `main` `1d75577`, PR #22–#26; recorrido SQLite en `BusinessApiTests.cs` y reglas de dominio |
| En rama | Reintentos idempotentes de transiciones de negocio y entrega | `feature/workflow-idempotency-main`, commits `0c39f51`, `69131f7`; build 0 advertencias/errores, 21/21 pruebas SQLite |
| Pendiente | OTP con proveedor real; idempotencia de reportes, creación de órdenes, comprobantes y asignaciones; métricas; pruebas de concurrencia SQL Server aislada; validación de reconexión SignalR con cliente real; despliegue MonsterASP.NET | Incrementos de ramas antiguas por auditar e integrar; configurar proveedor/hosting y `ConnectionStrings:SqlServerTest` |
| Fuera de alcance | GPS, distancia inferida, transferencia bancaria automatizada y frontend | Se usan las zonas y tarifas configuradas y revisión humana |

## Verificación

En PR #26: `dotnet restore Poraqui.sln`, `dotnet build Poraqui.sln --no-restore` (0 advertencias/errores), `dotnet test Poraqui.sln --no-build --no-restore` (18/18 SQLite). La rama activa agrega dos pruebas de reintentos y pasa las mismas verificaciones con 21/21.

## SQL Server y migraciones

`ConnectionStrings:DefaultConnection` está en User Secrets. La conexión se verificó con consulta de solo lectura; no se imprimió la cadena ni se ejecutaron migraciones en esta iteración. `dbo.__EFMigrationsHistory` en la BD de desarrollo tiene 12 entradas: hasta `CourierAssignmentCommandIdempotency`. `main` versiona ocho migraciones hasta cancelaciones; faltan cuatro migraciones registradas y sus implementaciones por reconciliar (`IncidentCommandIdempotency`, `OrderCreationCommandIdempotency`, `TransferEvidenceCommandIdempotency`, `CourierAssignmentCommandIdempotency`). No generar/aplicar nuevas migraciones hasta recuperar y revisar esta cadena.

Falta `ConnectionStrings:SqlServerTest`; configúrala en User Secrets del proyecto `src/Poraqui.Api` o en el entorno de test para una BD aislada `PoraquiIntegrationTests`. Nunca dirijas limpieza de pruebas a la BD compartida. La API no migra al iniciar.
