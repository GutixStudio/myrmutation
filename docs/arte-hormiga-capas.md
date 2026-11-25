# Hormiga por capas (B9)

La hormiga se dibuja con **capas de pixel art apiladas** (`Scripts/Ants/AntVisual.cs`). Este documento fija las medidas para que el arte definitivo (F16) sustituya a los placeholders sin tocar código. Si A13 (contrato de escala) cambia algún número, se actualiza aquí y en `AntVisual.PixelsPerUnit`.

## Lienzo y colocación

| | Obrera | Reina |
|---|---|---|
| Lienzo de cada capa | 32 × 20 px | 56 × 28 px |
| Mirando a | la derecha | la derecha |
| Pivote | abajo al centro | abajo al centro |
| Píxeles por unidad | 32 | 32 |
| Tamaño en el juego | 1 × 0,625 u | 1,75 × 0,875 u |

- **Todas las capas usan el mismo lienzo entero** (no se recortan), así se apilan en la misma posición y no hace falta colocarlas a mano.
- Los pies tocan la **fila inferior** del lienzo (fila 0).
- Importación: Sprite (Single), Pixels Per Unit 32, Pivot *Bottom Center*, Filter *Point*, Compression *None*, **Read/Write activado** (lo necesita el cambio de paleta).

## Capas (de atrás adelante)

| Capa | Obrera | Reina | Notas |
|---|---|---|---|
| Patas | `legs_0` … `legs_3`, `legs_idle` | `queen_legs_0` … `queen_legs_3`, `queen_legs_idle` | 4 fotogramas al andar y 1 parada |
| Abdomen | `abdomen` | `queen_abdomen` | |
| Tórax | `thorax` | `queen_thorax` | incluye el peciolo |
| Cabeza | `head_menor`, `head_media`, `head_mayor` | `queen_head` | la casta se distingue por la cabeza; la corona va en la cabeza de la reina |
| Mandíbula | `mandible_menor`, `mandible_media`, `mandible_mayor` | `queen_mandible` | |
| Antenas | `antenna` | `queen_antenna` | |

Placeholders en `Art/Ants/Placeholder/` (colores planos, tamaño exacto). El arte definitivo puede ir en otra carpeta: basta con arrastrarlo a los huecos del componente **AntVisual** del prefab `Ant`.

## Colores: dibujar con 4 grises

Para que las taras cambien de **paleta** (no de tinte), el cuerpo se dibuja solo con estos 4 grises exactos; el juego los cambia por la paleta de la casta o de la tara:

| Tono | RGB | Uso |
|---|---|---|
| 0 | 32, 32, 32 | contorno |
| 1 | 96, 96, 96 | sombra |
| 2 | 160, 160, 160 | base |
| 3 | 224, 224, 224 | brillo |

Cualquier otro color (ojos, corona, detalles) se queda tal cual.

## Animación (la hace el código)

- **Andar:** cambia el fotograma de patas cada 0,09 u recorridas; en los fotogramas 1 y 3 el cuerpo sube **1 px**.
- **Trabajar:** el cuerpo sube y baja 1 px. **Comer:** la cabeza baja 1 px a ratos.
- **Excavar:** cabeceo; cabeza, mandíbula y antenas bajan 1 px y avanzan 1 px a golpes.
- Todo se mueve en **píxeles enteros**: no hace falta dibujar fotogramas para eso.

## API para genes (C10)

- `AntVisual.SetPart(BodySlot, Sprite)`: cambia una pieza por otra del mismo lienzo (`null` vuelve a la normal).
- `AntVisual.SetPalette(Color[4])` / `ClearPalette()`: paleta propia.
- Sin llamar a nada: al publicarse `AntMutated`, la hormiga aplica sola `GeneData.bodyPart` en su hueco (`GeneData.slot`) y, si el gen es una tara, una paleta con el color del gen (`GeneData.tint`) y el sombreado de la normal.
