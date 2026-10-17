# Handoff de desarrollo

## Base y responsabilidad

`main` es la base de integración y contiene PR #1–#5 hasta `d677167` (PR #5, transferencia y snapshot por pedido). El desarrollo lo lleva una sola persona; OpenCode ya no participa. Se conservan sus contratos y asignación anterior como antecedentes en `docs/team-work.md`.

## Incremento actual

Rama `feature/external-courier-offers`, basada en el `main` actualizado. Agrega perfil y disponibilidad por zona para repartidores externos, ofertas con condiciones e importe visibles antes de aceptar, rechazo, cancelación, vencimiento y asignación de pedido al aceptar. La aceptación actualiza oferta y pedido en la misma transacción EF; `Order.Version`, versión de oferta e índice único filtrado protegen contra aceptaciones concurrentes. La remuneración ofertada se registra como el importe que el negocio paga al recoger; la confirmación separada existente del pago permanece en el flujo de pedido.

Migraciones generadas: `InitialCommerceAndIdentity`, `BusinessTransferInstructionsSnapshot` y `ExternalCourierOffers`. El script SQL aislado del último incremento está en `docs/sql/20261017191548_ExternalCourierOffers.sql`. `Up` crea tablas e índices; no elimina ni reemplaza datos existentes. No se ha aplicado ninguna migración.

## Verificación y límites

El recorrido API aislado usa SQLite en memoria y almacenamiento temporal. Comprueba creación de oferta, origen/destino/remuneración/condiciones, cancelación, salida sin repartidores, rechazo, aceptación y posterior recogida/entrega. Ejecutar `dotnet build Poraqui.sln`, `dotnet test Poraqui.sln` y `dotnet tool run dotnet-ef migrations has-pending-model-changes --project src/Poraqui.Infrastructure --startup-project src/Poraqui.Api` antes de integrar.

La prueba simultánea en SQL Server no se ha ejecutado: falta la configuración privada `ConnectionStrings:DefaultConnection`. La migración se generó sin conexión por la factoría de diseño y está pendiente de revisar frente al esquema de SQL Server. La API no migra automáticamente al iniciar.

Configura la conexión localmente sin compartirla:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena privada>" --project src/Poraqui.Api
```

Después, comprueba la conexión y el esquema antes de aplicar cualquier migración. No uses datos del chat ni limpies/recrees la base compartida.

## Siguiente trabajo

Completar opciones y horarios de catálogo, cancelaciones e incidencias/reembolsos, SignalR, reconexión, idempotencia, sincronización offline validada, métricas y despliegue. Ejecutar el recorrido external courier con dos conexiones SQL Server para verificar una sola aceptación real bajo carrera. Mantener `docs/project-status.md` como ledger verificable y `docs/api-contracts.md` como contrato vigente.
