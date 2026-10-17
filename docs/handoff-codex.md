# Estado y entrega de Codex

## Flujo de trabajo

El backend tiene un único responsable. `main` es la base de integración; cada incremento parte del `main` actualizado en `feature/*` y se entrega mediante PR. `AppDbContext`, contratos persistidos y migraciones tienen un solo propietario. La participación de OpenCode fue cancelada; su asignación anterior se conserva como antecedente en `docs/team-work.md`.

## Cambios integrados

- PR #1 y PR #22–#41 están integrados en `main`. El recorrido cubre identidad y autorización por rol/recurso, configuración comercial y catálogo, pedidos con precios de servidor, comprobante privado y revisión manual, preparación, reparto propio/externo, ofertas, códigos, contacto autorizado, historial, incidencias, reembolsos manuales, sincronización offline, SignalR, idempotencia, métricas piloto y verificación telefónica OTP. PR #32 agrega pruebas de concurrencia SQL Server; los casos están añadidos pero no se han ejecutado aún contra SQL Server.
- PR #29 incorpora idempotencia de creación de pedido.
- PR #30 incorpora idempotencia de subida del comprobante.
- PR #31 incorpora idempotencia de asignación de courier propio.
- Esas migraciones son revisables y aditivas; todavía no se han aplicado a SQL Server.

## Incrementos recientes integrados

PR #33 integra `GET /api/admin/metrics/pilot`, restringido a PlatformAdmin, con rango UTC de hasta 366 días, conteos, suma bruta, tiempo medio de entrega, incidencias abiertas y reembolsos en revisión. PR #36 integra Twilio Verify, endpoints de inicio/confirmación OTP y límite por IP. PR #38 cubre reconexión SignalR y recuperación del snapshot actual. PR #39 documenta publicación en MonsterASP.NET. El proveedor tiene pruebas con HTTP simulado; SMS real y publicación real siguen pendientes.

## Configuración privada y verificaciones

- `ConnectionStrings:DefaultConnection` existe en User Secrets; una consulta de solo lectura confirmó acceso al SQL Server compartido. No se revela su valor.
- Se creó una base MSSQL gratuita nueva para pruebas; permanece vacía y su acceso remoto está deshabilitado. Las pruebas comparan el destino de `ConnectionStrings__SqlServerTest` con `DefaultConnection` (cargada desde User Secrets) y omiten ejecución si servidor y catálogo coinciden. Desde este equipo, falta habilitar el acceso Remote a esa base y configurar en privado la cadena Remote con la clave `ConnectionStrings__SqlServerTest`.
- En 2026-10-17, `dotnet ef migrations list` se ejecutó en lectura sobre SQL Server y devolvió las 12 migraciones actuales; no reportó IDs pendientes. No se ejecutó ninguna migración durante esta entrega.
- `dotnet build Poraqui.sln --no-restore`: correcto, 0 advertencias y errores.
- `dotnet test Poraqui.sln`: 29 correctos; 2 carreras SQL omitidas por falta de `ConnectionStrings__SqlServerTest`.
- PR #41 añade cinco pruebas unitarias del guard de destino aislado. El catálogo de `ConnectionStrings__SqlServerTest` se compara contra el de `DefaultConnection` (User Secrets); ambos no pueden compartir el mismo nombre.
- `dotnet ef migrations has-pending-model-changes`: sin cambios pendientes del modelo.
- OpenAPI local: HTTP 200; snapshot versionado.
- La API no aplica migraciones automáticamente. Antes de futuras migraciones compartidas, conciliar `__EFMigrationsHistory` y revisar el SQL generado para asegurar que preserva datos.

## Próximos pasos verificables

1. Configurar la cadena privada SQL Server de pruebas aisladas y ejecutar las dos carreras concurrentes; conservar la BD y verificar las restricciones afectadas.
2. Para cambios futuros del modelo, generar y revisar la migración; revisar el historial de nuevo antes de aplicar cualquier migración compartida.
3. Seguir `docs/deployment-monsterasp.md` para preparar el sitio, secretos, almacenamiento y publicación; verificar health, SignalR y comprobantes en el hosting. La guía está integrada, pero el sitio aún no se ha publicado.
4. Configurar privadamente `TwilioVerify:ServiceSid`, `TwilioVerify:ApiKeySid` y `TwilioVerify:ApiKeySecret`, y verificar un SMS de extremo a extremo con un número autorizado.

## Incremento local actual

La rama `feature/external-courier-active-zone` valida que un courier solo pueda activar disponibilidad en una zona existente y activa. Añade cobertura API con SQLite para zona inexistente y documenta el `400`. Todavía requiere compilación, ejecución de pruebas, commit y PR; no contiene cambios de esquema ni migraciones.

El ledger vigente de implementado, pendiente y bloqueado está en `docs/project-status.md`; ejecución reproducible del recorrido está en `docs/order-flow.md`.
