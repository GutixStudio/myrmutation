# Issues · Sprints Beta 1 y Beta 2 (copiar y pegar en GitHub o usar `crear-issues-beta.sh`)

**Beta 1:** mié 14 – mar 20 oct · **Beta 2:** mié 21 – mar 27 oct. Áreas como en el GDD: A construcción e integración · B IA · C genética · D economía y partida · E UI y producción · F arte · T todos.
Referencias: `claude/diseno-construccion-beta.md` (construcción) y `claude/encargo-arte-beta.md` (arte).
Siguen abiertas de la alfa (no se duplican): **#38 F2 hormiga (prioridad)**, #40 F7 iconos de genes, #41 F8 hoja y presa, #42 F5 logo, #43 F4 UI propia, #44 F6 capturas, #34 E8 redes.

**Reglas del sprint (lecciones del post mortem):**
1. Los contratos (interfaces y eventos) se suben primero, en una PR pequeña, para que nadie espere.
2. Se integra en `Game.unity` a lo largo del sprint, no al final.
3. Escenas de prueba con el ID de la tarea (`Test_A6`, `Test_B6`…).
4. Cada archivo nuevo, con su `.meta`.

| ID | Título | Área | Sprint | h | Depende de |
|---|---|---|---|---|---|
| A6 | Rejilla del hormiguero y mapa nuevo | A | Beta 1 | 9 | — |
| A7 | Navegación por rejilla | A | Beta 1 | 7 | A6 |
| A8 | Planos y trabajos de excavación | A | Beta 1 | 7 | A6, A7 |
| B6 | Acción Excavar en el cerebro | B | Beta 1 | 4 | contrato de A8 |
| B7 | Icono de estado sobre la hormiga | B | Beta 1 | 4 | — |
| C7 | Renombrar genomas y texto de efecto de los genes | C | Beta 1 | 1,5 | — |
| C8 | Sucesos de genética en el log | C | Beta 1 | 1,5 | contrato de E11, C7 |
| D6 | Estadísticas de la colonia | D | Beta 1 | 4 | — |
| D8 | Despensa con obreras y estado de producción | D | Beta 1 | 3 | — |
| D9 | Sucesos de economía y partida en el log | D | Beta 1 | 2 | contrato de E11 |
| E11 | Log de sucesos | E | Beta 1 | 5 | — |
| E12 | HUD: ritmos, previsión de invierno y población | E | Beta 1 | 3 | D6 |
| E15 | Bug: el ×3 queda tapado en móvil + repaso de UI móvil | E | Beta 1 | 1,5 | — |
| E16 | GDD v0.3 y tablero de la beta | E | Beta 1–2 | 2 | — |
| F9 | Tileset de túnel | F | Beta 1 | 4 | — |
| A9 | Modo Construcción (plano azul) | A | Beta 2 | 8 | A8 |
| A10 | Salas como prefabs con puertas laterales | A | Beta 2 | 5 | A8 |
| A11 | Superficie y entrada del hormiguero | A | Beta 2 | 3 | A6, A7 |
| A12 | Limpieza de la construcción de la alfa e integración | A | Beta 2 | 3 | A9, A10 |
| B8 | Obreras libres sin trabajo y reparto en las salas | B | Beta 2 | 3 | A10, B6 |
| D7 | Expediciones que salen y vuelven por la entrada | D | Beta 2 | 4 | A11 |
| E13 | Panel de sala | E | Beta 2 | 4 | D8 |
| E14 | Estado de la sala sobre la sala | E | Beta 2 | 2 | D8 |
| F10 | Salas definitivas con puertas laterales | F | Beta 2 | 6 | F9 |
| F11 | Superficie, entrada y estratos | F | Beta 2 | 5 | — |
| F12 | Iconos de estado | F | Beta 2 | 3 | — |
| F13 | Iconos del modo Construcción y partículas | F | Beta 2 | 2 | — |
| T3 | Prueba en móvil real del modo Construcción | Todos | Beta 2 | 1 | A9 |

---

## A6 · Rejilla del hormiguero y mapa nuevo

**Título:** `A6 · Rejilla del hormiguero y mapa nuevo`

**Descripción:**
```markdown
## Objetivo
Sustituir las 8 zonas fijas por una rejilla fina excavable, dibujada con marching squares, con estratos y superficie.

## Criterios de aceptación
- [ ] `NestGrid` con el contrato de `diseno-construccion-beta.md` §8: casillas de 0,25, bloques de 3×3, terreno 120×78 + 12 filas de superficie
- [ ] Tipos de casilla: Solid, Dug, Room, Surface, Blocked; estrato según la profundidad (tierra 24, arcilla 27, roca 27 filas)
- [ ] **Render con marching squares:** Tilemap desplazado media casilla, 15 piezas elegidas por las 4 esquinas, sillas siempre conectadas (§2). Placeholder con las piezas de `guia/piezas/` hasta F9
- [ ] Solo se repintan las piezas alrededor de una casilla que cambia (no todo el Tilemap)
- [ ] Variantes al azar de `ms_1111` y detalles sueltos en los bordes (cuando llegue el arte)
- [ ] Mapa inicial: pozo de 3 casillas de ancho en x = 60, Sala Real a ~24 casillas de profundidad, Guardería y Despensa conectadas
- [ ] Eventos `CellDug` y `CellChanged`; `HasClearance` y `FloorPoint`
- [ ] `CameraController` limita la cámara al mapa según el aspecto de la pantalla (corrige la Sala Real cortada en pantalla completa)
- [ ] Gizmos de casillas y bloques en el editor

## Entregable
`NestGrid.cs`, `NestRenderer.cs` (marching squares) + escena `Test_A6` + PR

## Depende de
—

## Notas
Fondo de estratos con placeholder de color hasta F11. Las zonas y los waypoints se quitan de `Game.unity` en A12, no antes.

---
**Área:** A · **Sprint:** Beta 1 · **Estimación:** 9 h
```

## A7 · Navegación por rejilla

**Título:** `A7 · Navegación por rejilla`

**Descripción:**
```markdown
## Objetivo
Que las hormigas recorran los túneles excavados con el mismo contrato `INavigator`.

## Criterios de aceptación
- [ ] `GridNavigator : INavigator` (AntActuator y AntSensor sin cambios)
- [ ] **Holgura:** solo son nodos las casillas excavadas con sus 4 vecinas excavadas (con 8 se cortan las diagonales del pincel)
- [ ] A* en 8 direcciones con montículo binario y caché de rutas que se vacía en `CellDug`
- [ ] Las salas solo se conectan por sus puertas laterales inferiores, en horizontal
- [ ] Rutas **tensadas** (string pulling sobre la holgura), con esquinas redondeadas (Chaikin ×3) y bajadas al suelo según la horizontalidad del tramo, **no casilla a casilla**: la hormiga no sube y baja cada escalón del borde (§7 y `guia/ruta_hormiga.png`)
- [ ] Tensado y suavizado guardados en la caché, no cada frame
- [ ] Hormiga dentro de tierra → se recoloca en el nodo más cercano
- [ ] Túneles diagonales pintados con el pincel no se "cortan" para la holgura (prueba automática: un túnel pintado en diagonal da un solo componente conectado)
- [ ] 30 hormigas recalculando a la vez sin tirones en la build WebGL

## Entregable
`GridNavigator.cs` + PR

## Depende de
A6

## Notas
Ver §7 del diseño.

---
**Área:** A · **Sprint:** Beta 1 · **Estimación:** 7 h
```

## A8 · Planos y trabajos de excavación

**Título:** `A8 · Planos y trabajos de excavación`

**Descripción:**
```markdown
## Objetivo
Que los planos se conviertan en trabajos que las obreras libres excavan bloque a bloque.

## Criterios de aceptación
- [ ] **Primera PR (el primer día): solo el contrato** `IDigJobs`, `DigJob` y `Digging.Current` (§8), para que B6 empiece
- [ ] `DigPlanner`: planos de túnel (casillas finas) y de sala; trabajo por **bloques de 3×3**; bloque trabajable = con casillas en plano y vecino excavado alcanzable
- [ ] Salas: se empiezan solo por sus zonas de acceso; "Sin acceso" si ninguna está excavada ni en plano
- [ ] Coste por bloque = (casillas en plano / 9) × 1,5 × estrato; hasta 2 obreras por bloque; el progreso se conserva al soltar
- [ ] Las casillas del bloque se vacían de la más cercana a la obrera a la más lejana según el progreso
- [ ] Grieta en 3 fases sobre el bloque y terrones al excavar
- [ ] API de prueba para planificar desde código: `PaintBrush(centro, radio)` y `PlanRoom(data, origen)` (sin modo Construcción todavía)

## Entregable
`IDigJobs.cs`, `DigPlanner.cs` + PR

## Depende de
A6, A7

## Notas
Elimina los mínimos de obreras. Las obreras libres son la cuadrilla de excavación.

---
**Área:** A · **Sprint:** Beta 1 · **Estimación:** 7 h
```

## B6 · Acción Excavar en el cerebro

**Título:** `B6 · Acción Excavar en el cerebro`

**Descripción:**
```markdown
## Objetivo
Que las obreras libres excaven los planos por su cuenta.

## Criterios de aceptación
- [ ] Nuevo objetivo **Excavar**: prioridad Necesidad > Excavar > Rutina; solo obreras sin sala, vivas y no de expedición
- [ ] `TryClaim` → ir a `StandPosition` → mirar al bloque → `Work(fuerza, dt)` hasta `IsDone`
- [ ] Nueva acción `AntAction.Digging` en el actuador, con animación propia (cabeceo más marcado que al trabajar)
- [ ] Si la interrumpe el hambre o el cansancio: `Release()` y vuelve después
- [ ] Si se le asigna una sala mientras excava, suelta el trabajo y va a su sala
- [ ] Probado en `Test_B6` con una rejilla pequeña

## Entregable
Cambios en `AntBrain.cs` y `AntActuator.cs` + PR

## Depende de
Contrato de A8 (la PR pequeña del primer día)

## Notas
Coordinar con A8: probar juntos a mitad de semana, no al final.

---
**Área:** B · **Sprint:** Beta 1 · **Estimación:** 4 h
```

## B7 · Icono de estado sobre la hormiga

**Título:** `B7 · Icono de estado sobre la hormiga`

**Descripción:**
```markdown
## Objetivo
Que se vea de un vistazo qué hace cada hormiga y cuál tiene un problema, sin saturar la pantalla.

## Criterios de aceptación
- [ ] Burbuja con icono sobre la cabeza (`AntStatusBubble`), leída del estado del cerebro (`StateChanged`)
- [ ] **Alertas, siempre visibles:** hambre crítica (≥ 85 %), agotada, sin camino
- [ ] **Actividad (excavando, trabajando, comiendo, descansando, cargando), solo** si la cámara está cerca (zoom por debajo de un umbral) o la hormiga está seleccionada
- [ ] Tamaño en pantalla constante al hacer zoom
- [ ] Las alertas laten suave para llamar la atención
- [ ] Iconos placeholder (letra o forma de color) hasta F12

## Entregable
`AntStatusBubble.cs` + PR

## Depende de
—

## Notas
Regla general de la beta: **problemas siempre, información solo al acercarse o seleccionar.**

---
**Área:** B · **Sprint:** Beta 1 · **Estimación:** 4 h
```

## C7 · Renombrar genomas y texto de efecto de los genes

**Título:** `C7 · Renombrar genomas y texto de efecto de los genes`

**Descripción:**
```markdown
## Objetivo
Cerrar lo pendiente de la alfa y tener textos legibles para el log y los paneles.

## Criterios de aceptación
- [ ] `Antgenome.cs` → `AntGenome.cs` y `Queengenome.cs` → `QueenGenome.cs` (renombrar con su `.meta` para no romper referencias)
- [ ] `GeneData.shortEffect` (p. ej. "+50 % velocidad, se cansa antes") rellenado en los 3 genes
- [ ] El panel de hormiga usa `shortEffect` en el tooltip o al tocar el icono del gen

## Entregable
PR

## Depende de
—

## Notas
Renombrar desde Unity (no desde el explorador) para que se conserve el GUID.

---
**Área:** C · **Sprint:** Beta 1 · **Estimación:** 1,5 h
```

## C8 · Sucesos de genética en el log

**Título:** `C8 · Sucesos de genética en el log`

**Descripción:**
```markdown
## Objetivo
Que cada mutación, tara y herencia se cuente en el log.

## Criterios de aceptación
- [ ] "Lía ha devorado a Tom → Patas de corredora (+50 % velocidad)" (categoría Mutación)
- [ ] Tara en rojo: "Lía ha salido Glotona: come el doble"
- [ ] Mutación fallida: "Lía ha devorado a Tom… y no ha pasado nada"
- [ ] Herencia: "La reina hereda Patas de corredora: todas las nuevas Menores nacerán con ella"
- [ ] Cada entrada con foco en la hormiga (tocar la entrada centra la cámara)

## Entregable
PR

## Depende de
Contrato de E11 (`LogMessage`), C7

## Notas
—

---
**Área:** C · **Sprint:** Beta 1 · **Estimación:** 1,5 h
```

## D6 · Estadísticas de la colonia

**Título:** `D6 · Estadísticas de la colonia`

**Descripción:**
```markdown
## Objetivo
Datos de producción y previsión para que el jugador sepa si va bien de cara al invierno.

## Criterios de aceptación
- [ ] `ColonyStats` escucha `ResourceChanged` y calcula, en una ventana móvil de 60 s de juego, lo **producido** y lo **consumido** por minuto de cada recurso
- [ ] `float NetPerMinute(ResourceType)`, `Produced(...)`, `Consumed(...)`
- [ ] `int DaysUntilWinter` y `float WinterDaysCovered` = comida / consumo diario en invierno de la población actual
- [ ] Población por casta, obreras libres y de expedición
- [ ] Respeta la pausa y las velocidades ×2 y ×3 (todo en tiempo de juego)

## Entregable
`ColonyStats.cs` + PR

## Depende de
—

## Notas
No cambia `ResourceManager`: el signo de la diferencia dice si se produce o se consume.

---
**Área:** D · **Sprint:** Beta 1 · **Estimación:** 4 h
```

## D8 · Despensa con obreras y estado de producción

**Título:** `D8 · Despensa con obreras y estado de producción`

**Descripción:**
```markdown
## Objetivo
Corregir lo conocido de la alfa (las obreras de la Despensa no hacían nada) y explicar por qué una sala no produce.

## Criterios de aceptación
- [ ] La conversión hongo → comida la hacen las obreras de la Despensa (según afinidad). Sin obreras sigue al 25 % para no bloquear el inicio
- [ ] `RoomStatus GetStatus(Room)`: Ok, SinObreras, SinEntrada (sin hojas o sin hongo), Llena
- [ ] `float RatePerMinute(Room)` (producción de esa sala)
- [ ] Valores en el inspector y apuntados en la hoja de balance

## Entregable
Cambios en `ColonyProduction.cs` + PR

## Depende de
—

## Notas
Lo usan E13 (panel de sala) y E14 (estado sobre la sala). Decidir el 25 % entre todos si no convence.

---
**Área:** D · **Sprint:** Beta 1 · **Estimación:** 3 h
```

## D9 · Sucesos de economía y partida en el log

**Título:** `D9 · Sucesos de economía y partida en el log`

**Descripción:**
```markdown
## Objetivo
Que el log avise de lo que importa en la economía y la partida.

## Criterios de aceptación
- [ ] Nacimiento: "Ha nacido Rubí (Menor)" · Larva esperando casta (aviso, foco en la Guardería)
- [ ] Muerte: "Tom ha muerto de hambre" (con la causa)
- [ ] Comida baja: aviso cuando la comida cubre menos de 1 día de consumo
- [ ] "El invierno llega en 2 días: comida para X días" y cambio de estación
- [ ] Expedición: salida y vuelta con el botín ("3 obreras vuelven con 11 hojas y una presa")

## Entregable
PR

## Depende de
Contrato de E11 (`LogMessage`)

## Notas
Usar los datos de D6 para el aviso de invierno.

---
**Área:** D · **Sprint:** Beta 1 · **Estimación:** 2 h
```

## E11 · Log de sucesos

**Título:** `E11 · Log de sucesos`

**Descripción:**
```markdown
## Objetivo
Que el jugador sepa en todo momento lo que pasa en la colonia, también en móvil.

## Criterios de aceptación
- [ ] **Primera PR (el primer día): el contrato** en `GameEvents.cs`: `struct LogMessage { LogCategory Category; string Text; Transform Focus; }` y `enum LogCategory { Info, Nacimiento, Muerte, Construccion, Mutacion, Expedicion, Aviso, Estacion }`
- [ ] `EventLog` guarda las últimas 50 entradas con el día de juego
- [ ] **Avisos flotantes:** las 3 últimas, arriba a la izquierda bajo la barra superior, 6 s y se desvanecen; ancho máx. 35 % de la pantalla
- [ ] **Panel completo** al tocar los avisos o un botón: lista con filtro por categoría
- [ ] Color e icono por categoría (placeholders hasta F12)
- [ ] Tocar una entrada con foco centra la cámara en ella
- [ ] **Móvil:** texto ≥ 32 px y botones ≥ 88 px de alto en la resolución de referencia (1920×1080); probado en un móvil real
- [ ] Escucha también los eventos ya existentes (`AntDied`, `AntSpawned`, `RoomBuilt`, `SeasonChanged`…)

## Entregable
`EventLog.cs`, `EventLogUI.cs` + prefab + PR

## Depende de
—

## Notas
C8 y D9 publican sus mensajes sobre este contrato: subirlo antes que nada.

---
**Área:** E · **Sprint:** Beta 1 · **Estimación:** 5 h
```

## E12 · HUD: ritmos, previsión de invierno y población

**Título:** `E12 · HUD: ritmos, previsión de invierno y población`

**Descripción:**
```markdown
## Objetivo
Datos de gestión a la vista sin abrir menús.

## Criterios de aceptación
- [ ] Debajo de cada recurso, su ritmo neto: "+8/min" en verde o "−3/min" en rojo
- [ ] Tocar un recurso → pequeño desglose: producido y consumido por minuto
- [ ] Junto al día: "Invierno en 3 días · comida para 4" (verde si cubre los días de invierno, rojo si no)
- [ ] Población: "Obreras 7 (2 libres)" con las castas al tocar
- [ ] Legible en móvil (mismos mínimos que E11)

## Entregable
Cambios en `HUD.cs` + PR

## Depende de
D6

## Notas
—

---
**Área:** E · **Sprint:** Beta 1 · **Estimación:** 3 h
```

## E15 · Bug: el ×3 queda tapado en móvil + repaso de UI móvil

**Título:** `E15 · Bug: el ×3 queda tapado en móvil + repaso de UI móvil`

**Descripción:**
```markdown
## Objetivo
Que ningún control del juego quede debajo de los botones de itch.io.

## Criterios de aceptación
- [ ] `SpeedBar` desplazada unos 120 px a la izquierda (resolución de referencia) o subida a la barra superior: la esquina inferior derecha queda libre
- [ ] En itch (Edit game → Embed options): "Mobile friendly" activado con orientación horizontal
- [ ] Repaso en móvil real de todos los paneles (tamaños mínimos de E11)
- [ ] Probado en Chrome Android y Safari iOS

## Entregable
PR + build en itch

## Depende de
—

## Notas
El botón de pantalla completa de itch se dibuja encima del juego, en la esquina inferior derecha. En iPhone, Safari no permite pantalla completa para el juego, así que no basta con quitar ese botón.

---
**Área:** E · **Sprint:** Beta 1 · **Estimación:** 1,5 h
```

## E16 · GDD v0.3 y tablero de la beta

**Título:** `E16 · GDD v0.3 y tablero de la beta`

**Descripción:**
```markdown
## Objetivo
Que el GDD y el tablero reflejen las decisiones de la beta.

## Criterios de aceptación
- [ ] Sección 5.3 reescrita con el diseño de construcción (excavar, puertas laterales, modo Construcción, sin mínimos)
- [ ] Superficie y entrada; claridad (log, iconos, estadísticas) en la sección 8
- [ ] Fila nueva en el control de versiones
- [ ] Issues de la beta en el tablero con responsable y fecha

## Entregable
`docs/GDD.md` + tablero

## Depende de
—

## Notas
Se actualiza al cerrar cada sprint, no solo al final.

---
**Área:** E · **Sprint:** Beta 1–2 · **Estimación:** 2 h
```

## F9 · Tileset de túnel

**Título:** `F9 · Tileset de túnel`

**Descripción:**
```markdown
## Objetivo
Que los túneles excavados se vean dibujados a mano y con formas suaves.

## Criterios de aceptación
- [ ] 15 piezas de marching squares de 80×80 px (`ms_0001` … `ms_1111`) según `claude/encargo-arte-beta.md` (§1)
- [ ] 2 variantes extra del interior lleno (`ms_1111_b`, `ms_1111_c`)
- [ ] 3 fases de grieta (120×120) y 4 detalles de borde (raicillas, piedras)
- [ ] PNG transparente, nombres exactos

## Entregable
`Art/Tiles/` (24 PNG)

## Depende de
—

## Notas
**Prioridad 1 de la beta.** Mientras tanto se usan las piezas placeholder de `guia/piezas/`.

---
**Área:** F · **Sprint:** Beta 1 · **Estimación:** 4 h
```

## A9 · Modo Construcción (plano azul)

**Título:** `A9 · Modo Construcción (plano azul)`

**Descripción:**
```markdown
## Objetivo
Que el jugador dibuje túneles con un pincel y coloque salas sobre un plano azul.

## Criterios de aceptación
- [ ] Botón **Construir** en la barra inferior → plano azul por bloques sobre el terreno excavable (§5 del diseño)
- [ ] Al entrar pausa el tiempo por defecto; botón ▶ para reanudar dentro del modo; al salir recupera la velocidad anterior
- [ ] Plano, fantasma y UI animados con tiempo sin escala (siguen funcionando en pausa)
- [ ] **Pincel de túnel:** estampa un círculo de radio 1,5 cada media casilla del trazo, interpolando entre muestras; el anillo de las salas se recorta solo
- [ ] Vista previa del pincel desplazada un poco por encima del dedo en táctil
- [ ] Salas: fantasma verde o rojo con motivo; en táctil, confirmar con ✓ / ✕
- [ ] Borrar: pincel del mismo tamaño; tocar una sala en plano la cancela
- [ ] Táctil: un dedo usa la herramienta, dos dedos mueven y hacen zoom. Ratón: izquierdo herramienta, derecho o rueda pulsada mueven, rueda zoom
- [ ] Línea de estado: "Planos: N bloques · Obreras libres: M"
- [ ] Fuera del modo, planos tenues y etiqueta en cada sala en obras ("45 %", "Sin acceso", "Sin obreras libres")

## Entregable
`BuildModeController.cs`, `BuildModeUI.cs`, `BlueprintOverlay.cs` + PR

## Depende de
A8

## Notas
Probar pronto en móvil (T3).

---
**Área:** A · **Sprint:** Beta 2 · **Estimación:** 8 h
```

## A10 · Salas como prefabs con puertas laterales

**Título:** `A10 · Salas como prefabs con puertas laterales`

**Descripción:**
```markdown
## Objetivo
Salas de tamaño fijo, que solo se entran por los laterales inferiores.

## Criterios de aceptación
- [ ] `RoomData`: + `footprint` (casillas finas), `prefab`; se elimina `minWorkers` (y su uso en la UI)
- [ ] Prefab por sala: Real 27×15, resto 21×12, con `WorkPoint` y puertas en las 3 filas inferiores de la primera y la última columna
- [ ] Reglas de colocación: anillo de 2 casillas, sin tocar otras salas, zonas de acceso de 3×3 libres
- [ ] Al excavar el último bloque: las casillas pasan a `Room`, fundido del arte, `CompleteBuild()`, coste +25 % por sala
- [ ] Salas iniciales colocadas por código en el mapa inicial

## Entregable
Prefabs `Room_*` + cambios en `RoomData` y `BuildManager` + PR

## Depende de
A8

## Notas
Con el arte actual (640×400) estirado como placeholder hasta F10.

---
**Área:** A · **Sprint:** Beta 2 · **Estimación:** 5 h
```

## A11 · Superficie y entrada del hormiguero

**Título:** `A11 · Superficie y entrada del hormiguero`

**Descripción:**
```markdown
## Objetivo
Que el hormiguero tenga una salida a la superficie por la que entran y salen las hormigas.

## Criterios de aceptación
- [ ] Franja de superficie (hierba + cielo) sobre el terreno, visible en la cámara
- [ ] Entrada en x = 60 con pozo de 3 casillas de ancho excavado desde el inicio
- [ ] Superficie transitable en `GridNavigator`, con puntos de salida en los dos extremos (fuera de cámara)
- [ ] `NestEntrance` con `Position`, `ExitLeft` y `ExitRight` para D7 y eventos futuros

## Entregable
`NestEntrance.cs` + cambios en el mapa + PR

## Depende de
A6, A7

## Notas
La entrada será el punto de los eventos de invasores y lluvia más adelante.

---
**Área:** A · **Sprint:** Beta 2 · **Estimación:** 3 h
```

## A12 · Limpieza de la construcción de la alfa e integración

**Título:** `A12 · Limpieza de la construcción de la alfa e integración`

**Descripción:**
```markdown
## Objetivo
Dejar `Game.unity` solo con el sistema nuevo.

## Criterios de aceptación
- [ ] Fuera: `BuildZone`, `BuildMenuUI`, `ConstructionSite`, `WaypointGraph`, `WaypointNode`, `PathTester`, `ZoneTapTest`, prefabs `Zone_*`
- [ ] `AntSelection` y `AntTestController` sin referencias a `BuildZone`
- [ ] Partida completa sin errores en consola
- [ ] Build en itch probada en PC y móvil

## Entregable
PR

## Depende de
A9, A10

## Notas
Borrar con su `.meta`.

---
**Área:** A · **Sprint:** Beta 2 · **Estimación:** 3 h
```

## B8 · Obreras libres sin trabajo y reparto en las salas

**Título:** `B8 · Obreras libres sin trabajo y reparto en las salas`

**Descripción:**
```markdown
## Objetivo
Que las hormigas sin tarea no se amontonen y que dentro de las salas se repartan por el suelo.

## Criterios de aceptación
- [ ] Obrera libre sin planos: espera junto a la Sala Real o la entrada, con pequeños paseos
- [ ] `AntSpread` reparte a las obreras por la fila del suelo de la sala (las salas nuevas son más anchas)
- [ ] Ninguna hormiga se queda de pie en una puerta bloqueando el paso visualmente

## Entregable
PR

## Depende de
A10, B6

## Notas
—

---
**Área:** B · **Sprint:** Beta 2 · **Estimación:** 3 h
```

## D7 · Expediciones que salen y vuelven por la entrada

**Título:** `D7 · Expediciones que salen y vuelven por la entrada`

**Descripción:**
```markdown
## Objetivo
Que se vea a las hormigas salir del hormiguero y volver cargadas.

## Criterios de aceptación
- [ ] Al enviar: caminan hasta `NestEntrance`, suben y se van por un extremo de la superficie; ahí desaparecen
- [ ] La duración de la expedición cuenta desde que salen por el extremo
- [ ] Al volver: aparecen por un extremo y caminan a la entrada cargando hoja o presa (sprite de F8)
- [ ] El botín se suma al pasar por la entrada
- [ ] El cerebro queda suspendido mientras van y vuelven (como ahora)

## Entregable
Cambios en `ExpeditionSystem.cs` + PR

## Depende de
A11

## Notas
—

---
**Área:** D · **Sprint:** Beta 2 · **Estimación:** 4 h
```

## E13 · Panel de sala

**Título:** `E13 · Panel de sala`

**Descripción:**
```markdown
## Objetivo
Ver y gestionar una sala al tocarla (previsto para la beta en el GDD).

## Criterios de aceptación
- [ ] Tocar una sala (fuera del modo Construcción) → panel a la derecha (el mismo hueco que el de hormiga; solo uno abierto)
- [ ] Nombre, descripción corta, obreras n/capacidad con su casta (tocar una la selecciona)
- [ ] Producción: "+12 hongo/min · −12 hojas/min" y estado ("Sin hojas", "Llena"…)
- [ ] Botón "Añadir obrera libre" (elige la de mejor afinidad)

## Entregable
`RoomPanelUI.cs` + prefab + PR

## Depende de
D8

## Notas
—

---
**Área:** E · **Sprint:** Beta 2 · **Estimación:** 4 h
```

## E14 · Estado de la sala sobre la sala

**Título:** `E14 · Estado de la sala sobre la sala`

**Descripción:**
```markdown
## Objetivo
Avisar de problemas en las salas sin saturar la pantalla.

## Criterios de aceptación
- [ ] Como mucho **un icono por sala**, y **solo si hay problema**
- [ ] Prioridad: Sin acceso > Sin obreras > Sin hojas o hongo > Llena
- [ ] Tocar el icono abre el panel de sala (E13)

## Entregable
`RoomStatusIcon.cs` + PR

## Depende de
D8

## Notas
—

---
**Área:** E · **Sprint:** Beta 2 · **Estimación:** 2 h
```

## F10 · Salas definitivas con puertas laterales

**Título:** `F10 · Salas definitivas con puertas laterales`

**Descripción:**
```markdown
## Objetivo
Las salas finales, con las puertas abajo a los lados para que encajen con los túneles.

## Criterios de aceptación
- [ ] 5 salas: Real (1080×600), Guardería, Despensa, Hongos y Festín (840×480)
- [ ] Puertas según `claude/encargo-arte-beta.md` (§2): los 120 px inferiores de cada lateral, abiertos hasta el borde
- [ ] Suelo plano a 8 px del borde inferior
- [ ] Interior opaco, exterior transparente

## Entregable
`Art/Rooms/` (5 PNG)

## Depende de
F9 (el contorno de las puertas tiene que parecerse al de los túneles)

## Notas
`room_empty.png` ya no hace falta.

---
**Área:** F · **Sprint:** Beta 2 · **Estimación:** 6 h
```

## F11 · Superficie, entrada y estratos

**Título:** `F11 · Superficie, entrada y estratos`

**Descripción:**
```markdown
## Objetivo
El fondo del hormiguero y la superficie.

## Criterios de aceptación
- [ ] 3 texturas de estrato repetibles y 2 transiciones (encargo §3)
- [ ] Hierba y cielo repetibles en horizontal, montículo de la entrada

## Entregable
`Art/Environment/` (8 PNG)

## Depende de
—

## Notas
—

---
**Área:** F · **Sprint:** Beta 2 · **Estimación:** 5 h
```

## F12 · Iconos de estado

**Título:** `F12 · Iconos de estado`

**Descripción:**
```markdown
## Objetivo
Iconos de la burbuja de las hormigas, del estado de las salas y del log.

## Criterios de aceptación
- [ ] Burbuja + 8 iconos de hormiga, 4 de sala y 8 de categoría del log (encargo §4)
- [ ] Legibles a 32 px

## Entregable
`Art/Icons/Status/`

## Depende de
—

## Notas
—

---
**Área:** F · **Sprint:** Beta 2 · **Estimación:** 3 h
```

## F13 · Iconos del modo Construcción y partículas

**Título:** `F13 · Iconos del modo Construcción y partículas`

**Descripción:**
```markdown
## Objetivo
Iconos de las herramientas de construcción y la tierra que salta al excavar.

## Criterios de aceptación
- [ ] 6 iconos de herramienta y 3 terrones (encargo §5)

## Entregable
`Art/UI/Build/` y `Art/FX/`

## Depende de
—

## Notas
—

---
**Área:** F · **Sprint:** Beta 2 · **Estimación:** 2 h
```

## T3 · Prueba en móvil real del modo Construcción

**Título:** `T3 · Prueba en móvil real del modo Construcción (todos)`

**Descripción:**
```markdown
## Objetivo
Validar los gestos y los tamaños en móvil antes de seguir.

## Criterios de aceptación
- [ ] Cada miembro prueba la build de itch en su móvil: pintar túnel, colocar sala, mover la cámara con dos dedos, leer el log
- [ ] Problemas apuntados como issues con captura y modelo de móvil

## Entregable
Issues de bugs

## Depende de
A9

## Notas
—

---
**Área:** Todos · **Sprint:** Beta 2 · **Estimación:** 1 h
```
