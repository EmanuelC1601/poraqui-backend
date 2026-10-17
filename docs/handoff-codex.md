# Estado y entrega de Codex

## Flujo de trabajo

El backend tiene un único responsable. `main` es la base de integración; cada incremento parte del `main` actualizado en `feature/*` y se entrega mediante PR. `AppDbContext`, contratos persistidos y migraciones tienen un solo propietario. La participación de OpenCode fue cancelada; su asignación anterior se conserva como antecedente en `docs/team-work.md`.

## Cambios integrados

- PR #1 y PR #22–#33 están integrados en `main` (actualmente `bddc081`). El recorrido cubre identidad y autorización por rol/recurso, configuración comercial y catálogo, pedidos con precios de servidor, comprobante privado y revisión manual, preparación, reparto propio/externo, ofertas, códigos, contacto autorizado, historial, incidencias, reembolsos manuales, sincronización offline, SignalR, idempotencia y métricas piloto. PR #32 agrega pruebas de concurrencia SQL Server; los casos están añadidos pero no se han ejecutado aún contra SQL Server.
- PR #29 incorpora idempotencia de creación de pedido.
- PR #30 incorpora idempotencia de subida del comprobante.
- PR #31 incorpora idempotencia de asignación de courier propio.
- Esas migraciones son revisables y aditivas; todavía no se han aplicado a SQL Server.

## Rama activa

PR #33 integra `GET /api/admin/metrics/pilot`, restringido a PlatformAdmin, con rango UTC de hasta 366 días, conteos, suma bruta, tiempo medio de entrega, incidencias abiertas y reembolsos en revisión.

La suite ejecutada para PR #33 tuvo 24 casos: 22 correctos y 2 omitidos porque no está configurada la conexión aislada SQL Server. La traducción de métricas está verificada con el proveedor SQL Server sin abrir conexión; las carreras reales aún no se han verificado en SQL Server. No se ejecutaron migraciones.

## Configuración privada y verificaciones

- `ConnectionStrings:DefaultConnection` existe en User Secrets; una consulta de solo lectura confirmó acceso al SQL Server compartido. No se revela su valor.
- Falta `ConnectionStrings:SqlServerTest`. Configurar `ConnectionStrings__SqlServerTest` como variable de entorno privada o User Secrets y apuntarla a una instancia aislada cuyo catálogo sea `PoraquiIntegrationTests`.
- En 2026-10-17, `dotnet ef migrations list` se ejecutó en lectura sobre SQL Server y devolvió las 12 migraciones actuales; no reportó IDs pendientes. No se ejecutó ninguna migración durante esta entrega.
- Restore y build de solución: correctos, 0 advertencias y errores.
- Tests de solución: 21 correctos, 2 omitidos en esta rama.
- `dotnet ef migrations has-pending-model-changes`: sin cambios pendientes del modelo.
- OpenAPI local: HTTP 200; snapshot versionado.
- La API no aplica migraciones automáticamente. Antes de futuras migraciones compartidas, conciliar `__EFMigrationsHistory` y revisar el SQL generado para asegurar que preserva datos.

## Próximos pasos verificables

1. Configurar la cadena privada SQL Server de pruebas aisladas y ejecutar las dos carreras concurrentes; conservar la BD y verificar las restricciones afectadas.
2. Para cambios futuros del modelo, generar y revisar la migración; revisar el historial de nuevo antes de aplicar cualquier migración compartida.
3. Completar OTP con proveedor real, prueba de reconexión SignalR de cliente, y configuración/documentación de despliegue MonsterASP.NET.

El ledger vigente de implementado, pendiente y bloqueado está en `docs/project-status.md`; ejecución reproducible del recorrido está en `docs/order-flow.md`.
