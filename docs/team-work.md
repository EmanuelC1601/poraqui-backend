# Acuerdos de trabajo

## Responsabilidades

- **Agente 1 (Codex)**: solución y composición Clean Architecture, contexto/configuración EF e Identity, usuarios/permisos, negocios, catálogo, cobertura y tarifas, flujo de pedidos y revisión manual de transferencias; único responsable de migraciones y de aplicarlas a la BD compartida.
- **Agente 2 (OpenCode)**: módulo de reparto, usando los contratos de `docs/api-contracts.md`.

## Archivos compartidos

- `src/Poraqui.Domain/Orders/OrderStatus.cs`: Agente 1 mantiene el enum; coordinar cambios de estados y transiciones antes de modificar.
- `src/Poraqui.Application/Delivery/DeliveryOrderContracts.cs`: Agente 1 mantiene los contratos iniciales; el Agente 2 puede proponer extensiones mediante su PR, sin romper métodos o DTO existentes.
- `src/Poraqui.Infrastructure/Persistence/AppDbContext.cs`, configuración de DI y migraciones: Agente 1 propietario. El Agente 2 no genera migraciones.
- `docs/api-contracts.md`: ambos agentes coordinan contratos entre módulos vía PR/documentación del repositorio.

## Flujo y base de datos

Trabajar en ramas `feature/*` y entregar incrementos mediante PR. Antes de actualizar una rama, revisar cambios y resolver conflictos conservando ambos módulos. Tras cada integración, Agente 1 comunica en `docs/handoff-codex.md` los cambios de esquema y los comandos para actualizar la rama.

La base de desarrollo puede recibir registros funcionales de prueba identificables por ambos agentes. No borrar ni recrear datos ni reiniciar la BD compartida. Toda prueba que modifique o limpie datos debe usar una base independiente. Nunca incluir credenciales en Git.
