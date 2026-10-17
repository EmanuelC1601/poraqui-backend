# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` es la base única de integración. PR #22–#27 están integrados hasta `ce0cc7b`. OpenCode ya no participa. El responsable único implementa ramas `feature/*`, prepara PRs pequeños, verifica y actualiza este ledger, los contratos y el handoff.

## Ledger

| Estado | Función | Evidencia |
|---|---|---|
| Integrado | Arquitectura, Identity, permisos por negocio, catálogo con opciones, zonas/tarifas, precios del servidor, comprobante privado, revisión manual, pedido propio/externo, códigos, pago separado al recoger, historial, sync offline, SignalR, horarios, cancelaciones, incidencias, reembolsos manuales y reintentos idempotentes de transiciones | `main` `ce0cc7b`, PR #22–#27; recorrido SQLite de API y reglas de dominio |
| En rama | Reportes de incidencia idempotentes por `commandId` | `feature/incident-idempotency`, commit `8181753`; build 0 advertencias/errores, 21/21 pruebas SQLite, sin cambios pendientes del modelo |
| Pendiente | Idempotencia de creación de pedidos, comprobantes y asignaciones; OTP con proveedor real; métricas; pruebas de concurrencia SQL Server aislada; reconexión SignalR en cliente real; despliegue MonsterASP.NET | Incrementos de ramas anteriores por revisar e integrar; configurar proveedor/hosting y `ConnectionStrings:SqlServerTest` |
| Fuera de alcance | GPS, distancia inferida, transferencia bancaria automatizada y frontend | Se usan zonas y tarifas configuradas y revisión humana |

## Verificación

En PR #27: restore, build (0 advertencias/errores), 21/21 pruebas SQLite y generación local de OpenAPI (59 paths, HTTP 200). En la rama activa: restore/build pasan, 21/21 pruebas y `dotnet ef migrations has-pending-model-changes` informa que no hay cambios sin migración.

## SQL Server y migraciones

`ConnectionStrings:DefaultConnection` está en User Secrets. La conexión se verificó mediante consulta de solo lectura; no se imprimió la cadena ni se ejecutaron migraciones en esta iteración. `dbo.__EFMigrationsHistory` en la BD de desarrollo tiene 12 entradas hasta `CourierAssignmentCommandIdempotency`. `main` versiona ocho migraciones hasta cancelaciones; la rama activa versiona nueve, al recuperar `IncidentCommandIdempotency`. Aún faltan tres migraciones registradas y sus implementaciones por reconciliar (`OrderCreationCommandIdempotency`, `TransferEvidenceCommandIdempotency`, `CourierAssignmentCommandIdempotency`). No generar/aplicar nuevas migraciones hasta recuperar y revisar esta cadena.

Falta `ConnectionStrings:SqlServerTest`; configúrala en User Secrets de `src/Poraqui.Api` o en el entorno de test para una BD aislada `PoraquiIntegrationTests`. Nunca dirijas limpieza de pruebas a la BD compartida. La API no migra al iniciar.
