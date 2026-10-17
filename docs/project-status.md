# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` está en `d39d265` e integra PR #1–#6. Los incrementos posteriores siguen una pila de PRs; integrar uno en su rama base no significa que ya esté en `main`.

## Registro de funciones

| Función | Estado | Evidencia / pendiente |
|---|---|---|
| Clean Architecture, ASP.NET Core, EF Core SQL Server, Identity, roles y autorización por recurso | Implementado en `main` | PR #1–#2; API compila. |
| Negocio, catálogo básico, zonas, tarifas y pedidos con precios calculados en servidor | Implementado en `main` | PR #2–#3; permisos por propietario y pruebas de importes. |
| Transferencia manual, comprobante privado, revisión, preparación y reparto propio con códigos e historial | Implementado en `main` | PR #4–#5; comprobante en almacenamiento privado local; falta configurar almacenamiento persistente de hosting. |
| Ofertas externas, disponibilidad, expiración, cancelación y aceptación única | Implementado en `main` | PR #6; migración presente en el historial SQL Server. La carrera concurrente requiere una BD aislada `ConnectionStrings:SqlServerTest`. |
| Sincronización offline de entrega con validación del servidor e idempotencia | Implementado en rama apilada | PR #7 integrado en `feature/external-courier-offers` (`fe566db`), aún no en `main`; pruebas API cubren código, permiso, repetición y conflicto. |
| SignalR autorizado, evento después de guardar y snapshot al suscribirse/reconectar | Implementado en rama apilada | PR #8 integrado en `feature/offline-delivery-sync` (`f7b4c20`), aún no en `main`; prueba WebSocket en TestServer/SQLite. Cliente real de reconexión pendiente. |
| Opciones de catálogo, disponibilidad y snapshot de precio | Implementado en PR abierto | PR #9 (`feature/catalog-options`); migración `CatalogProductOptions` figura aplicada en SQL Server. Falta integrar la función en `main`. |
| Horarios del negocio | PR abierto | PR #10; falta revisar e integrar. |
| Cancelaciones, incidencias y reembolsos manuales | PR abierto | PR #11; PR #12 (métricas piloto) está integrado solo en esa rama. |
| Reintentos seguros de comandos de reparto y OTP por SMS | PRs abiertos | PR #13 y #14; falta revisar proveedor/configuración real y permisos/idempotencia. |
| Idempotencia de incidencias, pedidos, comprobantes y asignación propia de courier | Implementada en rama apilada | PR #18–#21 integrados en la pila `feature/phone-verification`; aún fuera de `main`. Carrera SQL de asignación añadida, pendiente de ejecutarse con la conexión aislada. |
| Despliegue en MonsterASP.NET | Pendiente | Configurar claves privadas, ruta persistente de Data Protection y archivos, y ejecutar recorrido publicado. |
| GPS, distancias automáticas, transferencias bancarias automáticas y frontend | Fuera de alcance | El envío externo usa zona/tarifa configurada; no se calcula distancia. |

## Verificaciones de esta rama

- PR #6: `dotnet build Poraqui.sln` completó con 0 advertencias y 0 errores; `dotnet test Poraqui.sln` pasó 14/14 con SQLite temporal. Se ejecutó el test host net9 usando roll-forward al runtime net10 disponible.
- PR #7: build limpio y 14/14 pruebas; `git diff --check` limpio. Las dos pruebas de concurrencia SQL Server no se ejecutan contra la BD compartida.
- PR #8: build limpio y 14/14 pruebas; la prueba SignalR abre WebSocket y verifica que el estado ya está persistido.
- PR #9: verificar build/test al revisar esta revisión; no declarar la rama integrada en `main` hasta completar esa revisión.
- `ConnectionStrings:DefaultConnection` existe en User Secrets. Una consulta EF de solo lectura, sin mostrar la cadena, confirmó conexión al host/base esperados y reportó aplicadas `InitialCommerceAndIdentity`, `BusinessTransferInstructionsSnapshot`, `ExternalCourierOffers`, `OfflineDeliveryCommandSync` y `CatalogProductOptions`.
- `ConnectionStrings:SqlServerTest` no está configurada. No se han ejecutado pruebas que limpian datos sobre la BD compartida.
- La API no aplica migraciones al iniciar. No se ejecutaron borrados, recreaciones ni reinicios de datos.

## Próximos pasos

1. Revisar y probar PR #9; mantenerlo en su rama base hasta que GitHub lo marque integrable.
2. Integrar en orden las ramas apiladas y revalidar el estado de esquema después de cada integración.
3. Completar SQL Server aislado para aceptación simultánea de ofertas/asignaciones y restricciones.
4. Revisar PRs de horarios, cancelaciones/incidencias/reembolsos, métricas, reintentos y OTP; llevar cada cambio a `main` en orden.
5. Cubrir pruebas de permisos entre negocios, importes, transiciones, códigos y reintentos, y preparar OpenAPI, despliegue y recorrido reproducible.

## Configuración privada necesaria

La conexión de desarrollo está presente localmente como `ConnectionStrings:DefaultConnection`; no guardar ni imprimir su valor. Para otra copia local, configura `ConnectionStrings:DefaultConnection` con User Secrets del proyecto `src/Poraqui.Api`. Para habilitar concurrencia SQL aislada, crea una BD desechable separada y configura `ConnectionStrings:SqlServerTest` para ella. No apuntes esa clave a la BD compartida.
