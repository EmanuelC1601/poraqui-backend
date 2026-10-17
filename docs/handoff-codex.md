# Handoff de desarrollo

## Base, ramas y responsabilidad

`main` contiene PR #1–#5 hasta `d677167`. PR #6 está abierto desde `feature/external-courier-offers`. El incremento actual está en `feature/offline-delivery-sync`, basado en PR #6; integra #6 antes de llevar esta rama a `main`. Un solo responsable mantiene todos los módulos, contratos, `AppDbContext` y migraciones. OpenCode ya no participa; su anterior asignación de reparto se conserva como antecedente.

## Incremento actual

Agrega `POST /api/orders/{orderId}/sync/delivery-confirmation` para confirmaciones capturadas offline. El servidor valida courier y código con el estado actual del pedido; el dato de hora del dispositivo se guarda como auditoría y no determina una transición. Guarda transición e ID/payload idempotente en una transacción. Un reintento idéntico devuelve la aceptación guardada; misma clave para datos distintos responde 409.

Migración nueva `OfflineDeliveryCommandSync` crea tabla e índice por pedido. Script desde la migración externa anterior: `docs/sql/20261017192726_OfflineDeliveryCommandSync.sql`. No altera ni elimina tablas existentes. Aún no aplicada.

## Verificación

Verificado antes de publicar PR #7: `dotnet build Poraqui.sln` (0 advertencias, 0 errores), `dotnet test Poraqui.sln` (14/14; SQLite en memoria y archivos temporales), `git diff --check` y `dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Poraqui.Infrastructure --startup-project src/Poraqui.Api` (sin cambios pendientes en el modelo). La suite cubre código inválido, acceso del courier asignado, sync aceptado, repetición sin duplicado y colisión de clave.

PR #7 está publicado y apilado sobre PR #6. La base compartida se inspeccionó desde el portal de solo lectura: `dbo` sin tablas. Falta `ConnectionStrings:DefaultConnection` en la configuración privada de la API, así que no se verificó EF contra SQL Server, migraciones vivas ni aceptación simultánea real. Mantener PR #6/#7 abiertos hasta hacer esas verificaciones. No ejecutar migraciones hasta configurar la clave y revisar estado actual. La API no aplica migraciones automáticamente al iniciar.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
```

## Siguiente trabajo

SignalR con autorización tras persistencia y estado al reconectar; idempotencia general de otras operaciones; cancelaciones de pedidos, incidencias y reembolsos; opciones/horarios de catálogo; métricas y despliegue. Ver contratos y ledger en `docs/api-contracts.md` y `docs/project-status.md`.
