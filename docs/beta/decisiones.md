# Decisiones de diseño surgidas al programar (beta)

Cada cambio respecto a `diseno-construccion-beta.md` o a una issue se apunta aquí y se revisa antes de darlo por bueno.

| Fecha | Tarea | Qué cambia | Por qué | Alternativa descartada | ¿Aprobado? |
|---|---|---|---|---|---|
| 2026-10-17 | A6 | `NestGrid.Set(Vector2Int, CellType)` público (no estaba en el contrato del §8). Cambia entre `Solid`, `Dug` y `Room` (incluido `Solid→Room` para A10); no toca `Surface` ni `Blocked`. Lanza `CellChanged` y, si es `Solid→Dug`, `CellDug` | El contrato no tenía forma de cambiar una casilla y la necesitan A8 (excavar) y A10 (salas) | Métodos separados `Dig` / `MakeRoom` | Sí (Ayoze) |
| 2026-10-17 | A6 | `Surface` cuenta como abierta para la lógica (`NestGrid.IsOpen` = `Dug`/`Room`/`Surface`, usado en `HasClearance` y `FloorPoint`) y como cerrada para el dibujo (`NestRenderer`: solo `Dug`/`Room`) | Si la superficie fuera cerrada, la boca del pozo no tendría holgura y la entrada quedaría desconectada en A7; si se dibujara abierta, se pintaría túnel sobre el cielo | Tratar la superficie igual en lógica y dibujo | Sí (Ayoze) |
