# Post mortem · Beta (v0.2-beta)

**Myrmutation** · Gutix (GutixStudio) · Juegos para Web y Redes Sociales · URJC 2025-26  
Entrega: 15 de noviembre de 2026

## 1. Trabajo colectivo

La beta se planificó en dos sprints (Beta 1: 14–20 de octubre; Beta 2: 21–27 de octubre) a partir de las lecciones de la alfa. El cambio principal ha sido la construcción: las zonas fijas se han sustituido por un hormiguero que se excava casilla a casilla, con modo Construcción, salas con puertas laterales y una superficie por la que salen y vuelven las expediciones. Además se han añadido el registro de sucesos, las estadísticas de la colonia, los paneles de sala y de hormigas, el menú del Festín y las expediciones de tamaño libre. Desde la alfa se han fusionado en `develop` 34 pull requests de código (de la #116 a la #154), además de las de la documentación de esta entrega (GDD v0.3.1 y post mortem).

### Lo que ha funcionado

- **Contratos primero.** Siguiendo la primera regla del sprint, los contratos se subieron en PR pequeñas antes que el sistema: `IDigJobs` (A8) y `LogMessage` (E11). Gracias a eso, la acción Excavar del cerebro (B6) y los sucesos de genética y de economía en el log (C8 y D9) se programaron en paralelo, sin esperar a que el sistema completo estuviera terminado.
- **Registro de decisiones.** El archivo `docs/beta/decisiones.md` recoge más de 75 decisiones técnicas: qué se decidió, por qué, qué se descartó y quién lo aprobó. Cuando una tarea dependía del sistema de otro compañero, la respuesta solía estar ya escrita allí.
- **Escenas de prueba con el ID de la tarea.** Con `Test_A6`, `Test_B8`, `Test_E17`… no se ha vuelto a repetir ningún nombre de escena.
- **Beta 1 casi a tiempo.** La mayoría de tareas de la Beta 1 (rejilla, navegación, planificador de excavación, log, estadísticas, HUD y repaso de la UI móvil) se fusionaron entre el 17 y el 23 de octubre, casi dentro del sprint. Solo B6 y D9 se quedaron fuera y llegaron en noviembre.
- **Permisos explícitos entre áreas.** Cuando una tarea necesitaba tocar un script o una escena de otro (por ejemplo `Game.unity`), se pidió permiso a su responsable antes de cambiarlo y se avisó en Teams.

### Lo que no ha funcionado tan bien

- **La Beta 2 se retrasó.** Entre el 26 de octubre y el 5 de noviembre no se fusionó ninguna PR, y varias tareas (B6, B8, C9, D7, D9, D10, E13, E14 y E17) no llegaron hasta noviembre.
- **Arreglos provisionales que duran demasiado.** Como la acción Excavar (B6) tardó en llegar, la escena principal usó durante semanas una cuadrilla de prueba (`DigTestDriver`) que suspendía el cerebro de las hormigas. Al fusionar B6 y B8, la cuadrilla y el cerebro se pisaban hasta que se quitó.
- **La integración en `Game.unity` volvió a quedarse atrás.** La regla de integrar a lo largo del sprint se cumplió solo en parte. Las estadísticas (D6), los avisos de economía (D9), el registro de sucesos (E11) y el menú del Festín (C9) funcionaban en sus escenas de prueba, pero no se montaron en la escena principal hasta el último día.
- **Conflictos en las escenas.** Dos PR que tocaban `Game.unity` y `C3.unity` a la vez obligaron a rehacer las escenas sobre `develop` (A12).
- **El arte de la beta no ha llegado al proyecto.** El tileset de túnel, las salas con puertas, la superficie y los iconos (F9–F13) no se han integrado, así que el juego sigue usando piezas provisionales en varios sitios.

### Lecciones aprendidas

1. **Una tarea no está terminada hasta que está en `Game.unity`.** Para la versión final, montar en la escena principal formará parte de cada PR (con permiso de A), no de una tarea aparte al final.
2. **Cada arreglo provisional, con su tarea para quitarlo.** Si algo se monta solo hasta que llegue otra tarea, se crea a la vez la issue que lo quita y se enlaza con la tarea de la que depende.
3. **Revisar el tablero a mitad de sprint.** Si una tarea con dependientes se atasca, se reparte o se simplifica en ese momento, sin esperar al final del sprint.
4. **El arte entra en el proyecto pieza a pieza.** Cada pieza terminada se sube en su propia PR, aunque falten las demás, para que el juego deje de depender de imágenes provisionales.
5. **Mantener lo que ha funcionado:** contratos primero, registro de decisiones y escenas de prueba con el ID de la tarea.

## 2. Trabajo individual

### Íñigo García — Inteligencia artificial (B) · Interfaz (E)

En esta beta me he encargado de que las hormigas sepan qué hacer cuando no tienen trabajo, y también de una parte de la interfaz: el registro de sucesos, el repaso de la UI en móvil y la lista de hormigas. Al final también he montado en la escena principal todo lo que se había quedado solo en las escenas de prueba.

Lo que mejor me ha funcionado ha sido subir primero el contrato del log en una PR pequeña (solo el mensaje y sus categorías). Así Alonso y Nacho pudieron mandar sus sucesos de genética y de economía mientras yo todavía estaba haciendo el panel, y nadie tuvo que esperarme. También me ha venido muy bien el registro de decisiones: antes de tocar las salas o la entrada miraba cómo lo había dejado Ayoze, y casi nunca he tenido que preguntar.

Lo malo es que la B8 dependía de las salas con puertas (A10) y de la acción Excavar (B6), y esta última llegó bastante tarde, así que no pude probar el reparto en las salas con el juego de verdad hasta noviembre. Mientras tanto la escena principal usaba una cuadrilla de prueba que se peleaba con el cerebro, y hubo que quitarla al final. Además, para la B8 tuve que ampliar `AntSpread`, que venía de la integración de la alfa (A5). En el registro de decisiones, al hacer las salas (A10), el reparto por el suelo se dejó pendiente para la B8 porque `AntSpread` es del área B, así que me tocó a mí, pero son cosas que conviene tener claras antes de empezar la tarea y no a mitad.

De lo que más contento estoy:

- **Obreras ociosas que se ven vivas.** Las que no tienen trabajo van a la Sala Real o a la entrada y pasean despacio con pausas, en vez de quedarse quietas.
- **Reparto por el suelo de las salas.** Cada hormiga busca el sitio más alejado de las demás, sin límite de hormigas por sala, y se aparta si se queda parada en una puerta.
- **Lista de hormigas.** En móvil, los botones de la primera versión salían enormes e iguales. La rehíce para que cada botón mida lo que su texto y el cajón no tape la partida.

Las horas dedicadas a cada tarea han sido las siguientes:

**Horas para**

- **E11 (log de sucesos): 5** Contrato del mensaje, avisos flotantes, panel con filtro por categoría y foco de la cámara en el suceso.
- **E15 (×3 tapado en móvil y repaso de UI móvil): 1,5** Sacar los botones de velocidad de debajo del botón de pantalla completa de itch.io y fijar tamaños mínimos táctiles en toda la UI.
- **B8 (obreras libres sin trabajo y reparto en las salas): 3** Objetivo nuevo para las ociosas y reparto por el suelo de las salas.
- **E17 (lista de hormigas): 2,5** Cajón lateral con filtro por casta, orden y envío a expedición.
- **Montaje de la beta en `Game.unity`: 2,5** ColonyStats, ColonyLog, EventLog y FeastPanel en la escena principal, y quitar la cuadrilla de prueba.

### Ayoze González — Construcción e integración (A)

En esta beta he llevado el área A: pasar la construcción de zonas fijas a un hormiguero que se excava casilla a casilla e integrar lo nuevo en la escena principal. Eso ha supuesto rehacer la rejilla, la navegación, el planificador de excavación, el modo Construcción, las salas, la superficie y la entrada. Además subí las issues de la beta y repartí las tareas entre el equipo.

En lo positivo, la base entró pronto. La rejilla, la navegación y el planificador (A6–A8) se fusionaron entre el 17 y el 20 de octubre, dentro de la Beta 1, y el resto de mi parte (A9–A12) entre el 22 y el 26 de octubre. El contrato de excavación (`IDigJobs`, `DigJob` y `Digging.Current`) lo subí en una PR aparte el 18 de octubre, dos días antes que el planificador, para que la acción Excavar (B6) pudiera programarse contra la interfaz. También fui apuntando en `decisiones.md` cada decisión que tomaba al programar, con lo descartado y el motivo; casi todas las entradas del archivo son de la construcción, y algunas iban dirigidas a otras áreas, como la altura a la que F10 debe dibujar el suelo de las salas o cómo debe B6 pedir y soltar un bloque.

En lo negativo, el problema más claro sale de mi parte. La cuadrilla de prueba `DigTestDriver` la creé en A8 para ver excavar a las hormigas en `Game.unity` mientras no existía B6, pero no creé ninguna tarea para quitarla, y siguió en la escena principal hasta el montaje final del 15 de noviembre, pisándose con el cerebro cuando entraron B6 y B8. En A12, además, dos PR tocaron `Game.unity` y `C3.unity` a la vez y hubo que abrir otra (#135) solo para resolver el conflicto, y el modo Construcción necesitó dos PR de ajustes (#137 y #138) después de cerrar A9. Por último, desde mi última PR de la beta (26 de octubre) hasta el 5 de noviembre no se fusionó nada en `develop`, y como el reparto de tareas lo llevaba yo, revisar el tablero a mitad de sprint (lección 3) es algo que me toca directamente.

Lo más destacado de mi trabajo:

- **Túneles con forma.** La rejilla fina se pinta con marching squares, así que lo excavado tiene bordes suaves en vez de cuadrados, y solo se repintan las piezas alrededor de la casilla que cambia.
- **Reparto de la excavación.** Los bloques se reparten por pasos de ruta reales y no por distancia en línea recta, y una obrera de camino cede su bloque a otra libre que esté más cerca. Así ninguna se queda parada al lado de algo que podría cavar.
- **Modo Construcción.** Plano azul con rejilla por niveles, lupa táctil para móvil y las hormigas dibujadas en silueta encima del plano, para ver quién trabaja.
- **Respuesta visual al cavar.** Grietas en tres fases sobre cada bloque empezado y terrones del color del estrato, sin física para no penalizar el rendimiento en WebGL, con sus tests de editor.

**Horas para**

- **A6 (rejilla del hormiguero y mapa nuevo): 9**
- **A7 (navegación por rejilla): 7**
- **A8 (planos y trabajos de excavación): 7**
- **A9 (modo Construcción): 8**
- **A10 (salas como prefabs con puertas laterales): 5**
- **A11 (superficie y entrada del hormiguero): 3**
- **A12 (limpieza de la construcción de la alfa e integración): 3**

### Nicolás Rosón — IA (B6–B7) · Interfaz (E12–E14)

Mis tareas de la beta han sido cinco. En IA, la acción Excavar dentro del cerebro de las hormigas (B6) y la burbuja de estado sobre cada hormiga (B7). En interfaz, los datos de gestión del HUD (E12), el panel de sala (E13) y el icono de estado sobre las salas (E14).

Lo que salió bien fue el arranque. B7 entró el 19 de octubre y E12 el 20, en los primeros días del sprint. La burbuja de B7 lee la acción de la hormiga por su nombre, así que pudo fusionarse antes que B6 y empezó a enseñar "Excavando" sola en cuanto B6 añadió esa acción. E12 se apoya en las estadísticas de Ignacio (D6), que entraron el mismo día. Y en E13 y E14 toda la interfaz se crea por código desde un prefab, de modo que no hubo que tocar ninguna escena ajena.

Lo que salió mal fue B6. Era una tarea de la Beta 1 y solo dependía del contrato de excavación, disponible desde el 18 de octubre, pero no se fusionó hasta el 5 de noviembre. De ella dependía la B8 de Íñigo, y mientras no estuvo, la escena principal tuvo que tirar de la cuadrilla de prueba. El panel y el icono de sala (E13 y E14) llegaron el 12 de noviembre, tres días antes de la entrega.

Lo que más destaco:

- **Burbuja de estado (B7).** Los problemas se ven siempre (hambre crítica, agotada, sin camino) y laten; la actividad (excavando, trabajando, comiendo, descansando) solo aparece al acercar la cámara o seleccionar la hormiga, para que el hormiguero no se llene de iconos.
- **Excavar en el cerebro (B6).** Excavar es un objetivo más de la hormiga, por detrás de comer y descansar y por delante de la rutina. Si lo deja a medias, suelta el bloque y el avance se conserva.
- **HUD de gestión (E12).** Ritmo neto de cada recurso, previsión del tipo "Invierno en 3 días · comida para 4" y población con obreras libres.
- **Panel e icono de sala (E13–E14).** El panel muestra obreras, producción y estado, y permite añadir la obrera libre que mejor rinde. Sobre cada sala aparece como mucho un icono, solo si hay un problema (sin acceso, sin obreras, sin materia prima o llena), y al tocarlo se abre el panel.

**Horas para**

- **B6 (acción Excavar en el cerebro): 4**
- **B7 (icono de estado sobre la hormiga): 4**
- **E12 (HUD: ritmos, previsión de invierno y población): 3**
- **E13 (panel de sala): 4**
- **E14 (estado de la sala sobre la sala): 2**

### Ignacio Perea — Economía y partida (D)

Mi área en la beta ha sido la economía de la colonia y las expediciones, en este orden: las estadísticas de la colonia (D6, 20 de octubre), la Despensa con obreras y el estado de producción (D8, 23 de octubre), las expediciones que salen y vuelven por la entrada (D7, 5 de noviembre) y, el día de la entrega, los sucesos de economía en el log (D9) y las expediciones de tamaño libre (D10).

Como pro, las dos primeras tareas entraron en la Beta 1 y `ColonyStats` sirvió de base a otros: el HUD de Nicolás (E12) y mis propios avisos del log (D9) leen de ahí. Además, D6 solo lee datos, sin modificar recursos ni hormigas ni tocar `Core` ni scripts de otras áreas. En D10 seguí la misma idea: el botín usa la carga y la fuerza que ya tiene cada casta en vez de añadir campos nuevos a `Core`, y dejé la decisión apuntada en `decisiones.md`.

Como contra, la segunda mitad se me fue de fechas. D7 dependía de la superficie y la entrada (A11), fusionadas el 25 de octubre, y no la subí hasta el 5 de noviembre. D9 era de la Beta 1 y solo dependía del contrato del log, disponible desde el 20 de octubre, pero entró el 15 de noviembre junto con D10, el mismo día de la entrega. Por eso las estadísticas y los avisos de economía solo llegaron a la escena principal en el montaje final, y la decisión de D10 sigue pendiente de aprobar.

Lo más destacado:

- **Expediciones visibles (D7).** Las hormigas caminan hasta la entrada, salen por un extremo de la superficie y vuelven por el mismo sitio cargando la hoja o la presa; cada una entrega su parte del botín al llegar.
- **Producción que se explica (D8).** Cada sala dice por qué no produce (sin obreras, sin materia prima o llena), y la Despensa sin obreras sigue al 25 % para no bloquear el inicio de la partida.
- **Avisos de economía (D9).** "Larva esperando casta", "Comida baja" y "El invierno llega en 2 días: comida para X días".
- **Botín que escala (D10).** Más hormigas, o más fuertes, traen más hojas y más presas, y como se calcula con las estadísticas de cada hormiga, los genes también cuentan.

**Horas para**

- **D6 (estadísticas de la colonia): 4**
- **D7 (expediciones que salen y vuelven por la entrada): 4**
- **D8 (Despensa con obreras y estado de producción): 3**
- **D9 (sucesos de economía y partida en el log): 2**
- **D10 (expediciones de tamaño libre): 2,5**

### Alonso Cancela — Genética (C) · GDD

En la beta he tenido cuatro tareas de tamaño muy distinto: C7, de mantenimiento (renombrar `AntGenome` y `QueenGenome` y dar a cada gen un texto corto de efecto); C8, que conecta la genética con el registro de sucesos; C9, el menú del Festín, la más grande; y E16, la actualización del GDD a la versión 0.3.

Si analizo lo que ha ido bien, C7 entró el 19 de octubre y C8 el 23, prácticamente dentro de la Beta 1. C8 es un ejemplo de lo útil que fue la regla de contratos primero: con `LogMessage` ya en `develop`, los sucesos de genética (mutación, tara y herencia) se resolvieron dentro de `FeastSystem` y `QueenGenome`, sin tocar la interfaz del log. En C9, el prefab del menú se genera desde un menú del editor, así que se puede reconstruir sin editar nada a mano.

Si analizo lo que no, C9 se fusionó el 6 de noviembre pero no llegó a la escena principal hasta el montaje final del 15, así que hasta entonces el menú solo existía en su escena de prueba. Y la versión 0.3 del GDD es del 14 de noviembre: recoge en una sola entrada cambios de diseño que se habían decidido durante octubre, como la nueva construcción, cuando el GDD debería actualizarse a medida que el diseño cambia.

Lo más destacado:

- **Menú del Festín (C9).** En tres pasos: elegir la presa, elegir qué hormiga la devora (la reina no come presas) y confirmar. El gen de cada especie sale como "?" hasta devorar la primera presa de esa especie, y entonces queda descubierto para el resto de la partida y se avisa en el log.
- **Genes con su efecto a la vista (C7).** Tocar el icono de un gen en el panel de la hormiga enseña lo que hace.
- **Sucesos de genética (C8).** Cada devorada queda en el log con su resultado, y las taras salen en rojo.
- **GDD v0.3 (E16).** Sección 5.3 reescrita con la construcción de la beta (excavación, puertas laterales, modo Construcción, sin mínimo de obreras), superficie y entrada, y sección 8 con la claridad de la beta (log, iconos de estado y estadísticas).

**Horas para**

- **C7 (renombrar genomas y texto de efecto de los genes): 1,5**
- **C8 (sucesos de genética en el log): 1,5**
- **C9 (menú del Festín): 2,5**
- **E16 (GDD v0.3 y tablero de la beta): 2**

### Naroa Jiménez — Arte (F)

Como única persona de arte del equipo, en la beta tenía asignadas cinco tareas: el tileset de túnel (F9), las salas definitivas con puertas laterales (F10), la superficie, la entrada y los estratos (F11), los iconos de estado (F12) y los iconos del modo Construcción y las partículas (F13).

El balance de mi parte es negativo: ninguna de las piezas de F9 a F13 ha llegado al repositorio en esta entrega, y el juego sigue usando piezas provisionales en los túneles, las salas, la superficie y los iconos. Es el punto que el trabajo colectivo recoge en "El arte de la beta no ha llegado al proyecto" y el origen de la lección 4: subir cada pieza en su propia PR en cuanto esté terminada, aunque falten las demás.

Lo positivo es que la integración del arte ya no es un bloqueo. El encargo de la beta fija el tamaño, el nombre de archivo y la posición de cada pieza (por ejemplo, la altura a la que va el suelo de las salas para que las hormigas no floten), y el código ya tiene el hueco preparado para recibirla: el sprite de cada sala en su prefab, el montículo y la decoración de la superficie, las fases de la grieta, los terrones y los iconos de la burbuja de estado. Para la versión final basta con añadir los PNG con el nombre acordado, sin cambiar código.

**Horas para** (estimadas en el tablero)

- **F9 (tileset de túnel): 4**
- **F10 (salas definitivas con puertas laterales): 6**
- **F11 (superficie, entrada y estratos): 5**
- **F12 (iconos de estado): 3**
- **F13 (iconos del modo Construcción y partículas): 2**

## 3. Resumen de horas

Horas estimadas en el tablero de la beta; E17, el montaje en `Game.unity`, C9 y D10 no tenían estimación y se indican las dedicadas.

| Miembro | Área | Horas |
|---|---|---|
| Ayoze González | A6–A12 | 42 h |
| Íñigo García | B8 · E11 · E15 · E17 · montaje en Game | 14,5 h |
| Nicolás Rosón | B6–B7 · E12–E14 | 17 h |
| Ignacio Perea | D6–D10 | 15,5 h |
| Alonso Cancela | C7–C9 · E16 | 7,5 h |
| Naroa Jiménez | F9–F13 | 20 h |
