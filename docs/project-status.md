# Estado verificable del proyecto

Actualizado: 2026-10-17. `main` está en `d39d265` e integra PR #1–#6. Las integraciones en ramas apiladas no forman parte de `main` hasta que la pila llegue a esa rama.

## Registro de funciones

| Función | Estado | Evidencia / pendiente |
|---|---|---|
| Clean Architecture, Web API, EF Core SQL Server, Identity, roles y permisos por recurso | Implementado en `main` | PR #1–#2; la solución compila. |
| Registro/configuración de negocio, catálogo básico, zonas, tarifas y precios de pedido calculados en servidor | Implementado en `main` | PR #2–#3; hay pruebas de permisos e importes. |
| Transferencia manual, comprobante privado, revisión, preparación y reparto propio con códigos/historial | Implementado en `main` | PR #4–#5; falta configurar almacenamiento persistente de hosting. |
| Ofertas externas, disponibilidad, vencimiento, cancelación y aceptación exclusiva | Implementado en `main` | PR #6; migración confirmada por el historial SQL Server. La prueba de simultaneidad necesita `ConnectionStrings:SqlServerTest` aislada. |
| Sincronización offline de confirmación de entrega con validación del servidor e idempotencia | Implementado en rama apilada | PR #7 integrado en `feature/external-courier-offers` (`fe566db`), fuera de `main`. Pruebas cubren código, permisos, reintentos y colisión. |
| SignalR autorizado, publicación posterior al guardado y snapshot actual al suscribirse | Implementado en rama apilada | PR #8 integrado en `feature/offline-delivery-sync` (`f7b4c20`), fuera de `main`. Prueba WebSocket real en TestServer/SQLite; falta reconexión en cliente real. |
| Opciones disponibles, límites y snapshots de precio | Implementado en rama apilada | PR #9 integrado en `feature/realtime-order-updates` (`6a73c9d`), fuera de `main`. Pruebas API validan permisos, selección e importes. |
| Horarios y zona horaria local del negocio | Implementado en PR abierto | PR #10 (`feature/business-hours`), sobre la pila anterior. Prueba reglas y acceso; migración aditiva figura aplicada en SQL Server. Falta integrar en la rama base y luego en `main`. |
| Cancelaciones, incidencias y reembolsos manuales | PR abierto | PR #11; PR #12 de métricas integrado solo en su rama base. Revisar cobertura e invariantes antes de integrar. |
| Reintentos seguros de comandos de reparto y OTP por SMS | PRs abiertos | PR #13 y #14; verificar integración, proveedor OTP y configuración real. |
| Idempotencia durable para incidencias, creación de pedidos, comprobantes y asignación de courier propio | Implementada en otra rama apilada | PR #18–#21 integrados en `feature/phone-verification`, fuera de `main`. La prueba de carrera de asignación requiere SQL aislado. |
| Despliegue en MonsterASP.NET | Pendiente | Configurar secretos, Data Protection persistente y ruta privada de comprobantes; ejecutar recorrido publicado. |
| GPS, distancias automáticas, transferencias bancarias automáticas y frontend | Fuera de alcance | El reparto utiliza zonas y tarifas configuradas, sin calcular distancias. |

## Verificaciones recientes

- Rama `feature/business-hours`, antes de publicar cambios documentales: `dotnet build Poraqui.sln` con 0 advertencias/errores y `dotnet test Poraqui.sln` con 15/15 pruebas SQLite. El test host net9 usó `DOTNET_ROLL_FORWARD=Major` al runtime net10 disponible.
- `git diff --check` está limpio después de quitar el espacio final de `docs/sql/BusinessOpeningHours.sql`.
- `ConnectionStrings:DefaultConnection` existe en User Secrets. EF pudo consultar el historial sin exponer la cadena y reportó aplicadas las primeras seis migraciones, hasta `BusinessOpeningHours`. `has-pending-model-changes` indicó que el modelo coincide con el snapshot.
- `ConnectionStrings:SqlServerTest` sigue sin configurar. No se ejecutan pruebas con limpieza de datos contra la BD compartida.
- La API no migra al iniciar. No se realizaron borrados, recreaciones ni reinicios de datos.

## Pendientes y bloqueos

1. Integrar PR #10 en su rama base tras corregir el chequeo documental; el PR #10 todavía no está en `main`.
2. Ejecutar las pruebas de concurrencia de ofertas y asignaciones propias en una BD aislada SQL Server.
3. Revisar/integrar en orden PR #11–#14 y las pilas #18–#21; reforzar permisos entre negocios, transiciones, códigos y reintentos idempotentes.
4. Validar reconexión SignalR con cliente real, configurar despliegue MonsterASP y entregar OpenAPI/recorrido final.

La conexión de desarrollo está configurada como `ConnectionStrings:DefaultConnection` en secretos privados locales. En otra copia usa User Secrets del proyecto `src/Poraqui.Api`; para concurrencia, crea una BD aislada y configura `ConnectionStrings:SqlServerTest`. Nunca apuntes pruebas que limpian datos a la BD compartida.
