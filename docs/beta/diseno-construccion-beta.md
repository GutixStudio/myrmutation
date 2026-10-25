# MYRMUTATION · Diseño de la construcción (beta)

Documento de referencia para las issues A6–A12, B6 y D7. Todo número es un valor inicial ajustable desde el inspector.

## 1. Idea
**Todo es excavar.** El jugador no construye salas al instante: dibuja **planos** (túneles y salas) y las **obreras libres** los excavan desde lo ya excavado. El hormiguero crece a la vista y con **formas orgánicas** (curvas y diagonales suaves, sin escalera).

- Desaparecen las 8 zonas fijas (`BuildZone`), el menú por zona (`BuildMenuUI`), `ConstructionSite` y el grafo de waypoints (`WaypointGraph`, `WaypointNode`, `PathTester`, `ZoneTapTest`, prefabs `Zone_*`).
- Desaparecen los **mínimos de obreras**: la velocidad depende de la fuerza de quien excava y de la dureza del estrato.
- **Obreras libres = cuadrilla de excavación.** Toda obrera sin sala asignada (y que no esté de expedición) excava si hay planos pendientes. Resuelve "constructoras sin sala al terminar".

## 2. Rejilla fina + marching squares
La lógica es una rejilla **fina**; el dibujo se hace **por esquinas** (marching squares / rejilla dual) para que las formas salgan suaves.

| Parámetro | Valor |
|---|---|
| Casilla lógica | **0,25 unidades = 40 px** (PPU 160) |
| Bloque de trabajo | **3 × 3 casillas = 0,75 u = 120 px** (ver §4) |
| Terreno excavable | **120 × 78** casillas (30 × 19,5 unidades) |
| Superficie | 12 filas por encima del terreno (3 de hierba transitable + cielo). No se excava |
| Coordenadas | `x` 0–119 de izquierda a derecha · `y` 0 = fondo; la fila 77 es la última de tierra |
| Estratos por profundidad | Tierra: 24 filas superiores · Arcilla: 27 siguientes · Roca: 27 del fondo |
| Ancho de un túnel | **3 casillas** (pincel de radio 1,5) = 0,75 u, el hueco útil para la hormiga Mayor |

Tipos de casilla: `Solid` (tierra sin excavar) · `Dug` (excavada: túnel) · `Room` · `Surface` · `Blocked` (bordes del mapa).

**Cómo se dibuja (marching squares):**
- La pieza visual no se coloca en cada casilla, sino **entre 4 casillas**, con sus esquinas en los centros de esas 4 casillas. Es un Tilemap desplazado media casilla.
- Cada pieza mira cuáles de sus 4 esquinas están excavadas y elige 1 de **15 formas** (`ms_` + 4 dígitos: sup-izq, sup-der, inf-der, inf-izq; 1 = excavada). Si no hay ninguna excavada, no se pone pieza.
- **Las "sillas"** (`ms_0101`, `ms_1010`, dos esquinas excavadas en diagonal) se tratan siempre como **conectadas**, para que no se corten túneles diagonales.
- **Resultado:** el borde del hueco pasa por los límites de las casillas excavadas, pero las diagonales y las esquinas salen redondeadas, sin escalera.
- Piezas de **80 × 80 px importadas a PPU 320** (miden 0,25 u, con el doble de detalle). 2–3 variantes de `ms_1111` (interior lleno) elegidas al azar para que no se repita.
- Encima van detalles sueltos por código (raicillas y piedras en los bordes) para romper la regularidad.
- Guías: `guia/guia_marching_squares_15.png` (las 15 piezas placeholder) y `guia/ejemplo_tunel_curvo.png` (cómo queda un trazo curvo).

## 3. Salas
Son **prefabs** con tamaño fijo por tipo (no hay Despensa grande ni Guardería pequeña). El tamaño en píxeles del arte no cambia respecto al plan anterior.

| Sala | Huella (casillas finas) | Lienzo del arte |
|---|---|---|
| Sala Real | 27 × 15 | 1080 × 600 px |
| Guardería, Despensa, Hongos, Festín | 21 × 12 | 840 × 480 px |

**Entradas: solo por los laterales inferiores.**
- **Puertas:** cada sala tiene dos, en las **3 filas inferiores** de su primera y última columna (abajo a la izquierda y abajo a la derecha). Miden **120 px de alto**: lo mismo que un túnel de 3 casillas.
- **Zona de acceso:** el bloque de 3 × 3 casillas justo fuera de cada puerta.
- **Requisito para que la sala se use:** al menos una zona de acceso excavada entera y unida a la red de túneles.

Reglas de colocación:
1. La huella debe caer entera en terreno `Solid` excavable (no en la superficie ni fuera del mapa).
2. **Anillo de pared:** las casillas a 2 o menos de la huella no se pueden excavar, salvo las dos zonas de acceso. El pincel que pase por el anillo simplemente no lo marca, así que ningún túnel muerde la pared de la sala y el dibujo siempre encaja.
3. Dos salas no pueden tocarse: la huella de una no puede caer en el anillo de otra.
4. Las zonas de acceso no pueden ser huella ni anillo de otra sala.

Dentro de una sala terminada, las hormigas caminan por el **suelo** (8 px por encima del borde inferior de la huella), de puerta a puerta. Una sala también sirve de paso entre sus dos lados, como en Fallout Shelter.

**Al terminar una sala:**
- Las casillas de la huella pasan a `Room` y dejan de dibujarse como túnel.
- Encima se pone el prefab con el arte. Su interior es opaco y su exterior transparente, así que en las esquinas se ve el fondo de tierra.

**Coste:** cada sala tiene `workCost` total, que se reparte entre sus bloques, y sube un 25 % por sala ya construida (como en la alfa). Sin coste de recursos por ahora.

## 4. Excavar
- **Plano:** casillas marcadas para excavar (con el pincel) o la huella de una sala.
- **Bloques de trabajo:** el mapa se divide en bloques fijos de 3 × 3 casillas (0,75 u). Excavar casilla a casilla serían miles de trabajos minúsculos; el bloque es la unidad que se reserva, se trabaja y se ve avanzar.
- **Bloque trabajable (túnel):** tiene casillas en plano y hay un nodo alcanzable a ≤ 2 casillas (Chebyshev) de alguna de ellas, fuera de la huella de cualquier sala en plano. La obrera se pone en el `GroundPoint` de ese nodo (el más cercano a las casillas en plano del bloque). Los bloques de sala siguen sus propias reglas (punto siguiente). *(Cambiado en A8 y A10, ver decisiones.md.)*
- **Hormigas no alcanzables:** `TryClaim` devuelve false sin reservar nada si el nodo más cercano a la hormiga no es alcanzable (atrapada en tierra o en una sala sin salida). *(Cambiado en A8, ver decisiones.md.)*
- **Salas (con gravedad):** las obreras las excavan siempre de pie en su suelo, nunca flotando. *(Cambiado en A10, ver decisiones.md.)*
  - Desde que la sala está en plano, su huella se navega como una sala terminada: dentro solo hay nodos en la fila central de las 3 inferiores y solo se entra y sale en horizontal, por las puertas.
  - **Por capas de bloques:** la capa 0 (la fila inferior de bloques) se empieza solo desde sus zonas de acceso y avanza hacia dentro por sus propios bloques ya excavados. Un bloque de la capa k ≥ 1 solo es trabajable cuando **toda** la capa k−1 está terminada: el techo sube parejo.
  - La obrera de un bloque de sala se pone en el `GroundPoint` del nodo alcanzable del suelo de la sala (o de sus zonas de acceso) más cercano en x al centro del bloque, sin el límite de 2 casillas.
  - Si ninguna zona de acceso está excavada ni en plano, la sala muestra **"Sin acceso"**.
- **Coste de un bloque** = (casillas en plano del bloque / 9) × 1,5 × estrato (tierra ×1, arcilla ×1,33, roca ×2). Un bloque completo de tierra lo excava una Media (fuerza 1) en 1,5 s.
- **Hasta 2 obreras por bloque** (sus fuerzas se suman). Sin esto, un túnel largo lo excavaría una sola hormiga, porque su frente solo tiene un bloque.
- **Mientras se excava,** las casillas del bloque se van vaciando según el progreso: en los túneles, de la más cercana a la obrera a la más lejana (el túnel "avanza" de verdad); en las salas, por filas de abajo arriba. Encima va la grieta en 3 fases y saltan terrones.
- La obrera se coloca en la parte excavada más cercana, mirando al bloque, y trabaja hasta terminarlo, salvo que la interrumpa una necesidad (comer o descansar). Entonces suelta el bloque y el progreso se conserva.
- **Cada casilla que termina:** se avisa al navegador (invalida la caché) y se publica `CellDug`.
- **Al excavar el último bloque de una sala:** aparece el prefab de la sala con un fundido, se llama a `Room.CompleteBuild()` y la sala pasa a funcionar.
- **Cancelar:** borrar un plano no rellena lo ya excavado. Las casillas excavadas de una sala cancelada quedan como túnel.

## 5. Modo Construcción
- Botón **Construir** en la barra inferior. Al pulsarlo se activa el **plano azul (blueprint)** sobre el hormiguero:
  - Papel **opaco** (`#1E3F73`, alfa 1) con textura de papel encima, en todo el mapa: tapa terreno, túneles, arte de salas y hormigas (no se ven franjas de estratos).
  - Rejilla **por niveles según el zoom**, como papel milimetrado: cuadrados de 9 × 9 casillas, bloques de 3 × 3 y casillas sueltas. Cada nivel aparece con un fundido según el tamaño en pantalla de su cuadrado (se empieza a ver a > 40 px y es opaco a > 80 px): con el mapa casi entero solo se ven los de 9 × 9; al acercar, los de 3 × 3 y, muy cerca, las casillas. Solo se ve en terreno donde se puede construir (más tenue donde no se puede pintar). Líneas de grosor constante en pantalla (2 / 1,5 / 1 px, más marcadas las de nivel superior). Shader `Myrmutation/BlueprintGrid` (`BlueprintGrid.mat`).
  - Lo existente (túneles, pozo y salas, todo lo excavado): **relleno** azul sólido (`#2E5A9A`, más claro que el papel) sin rejilla dentro, con contorno blanco completo encima (piezas `_bp` y `_line`).
  - Planos: cada casilla en plano es un **cuadrado** relleno azul claro (`#8FD3FF`, alfa 0,45) con borde fino, alineado con la rejilla.
  - Zonas prohibidas (superficie, montículo, filas junto a la hierba, anillos de sala): rejilla más tenue. Al pasar el pincel por encima, se marcan en rojo.
  - Salas en plano: su huella en cuadrados, con el contorno de la huella marcado, y flechas en sus dos puertas.
  - Hormigas (y crías, cuando tengan dibujo): encima del papel como siluetas `#E8F4FF`; al salir, colores y orden normales.
  - Orden de dibujo dentro del modo: papel → rejilla → relleno y contorno de lo existente → hormigas → planos → pincel y fantasma.
  - Con el modo abierto se ocultan la barra inferior del HUD (Guardería, Expedición, Construir) y la de velocidad; al salir vuelven.
- **Al entrar, el tiempo se pausa por defecto** y al salir vuelve a la velocidad que había. En el modo hay un botón ▶ para reanudar sin salir (para ver excavar mientras se planifica); al salir con Listo se recupera la velocidad anterior. Las animaciones del plano y de la interfaz usan tiempo sin escala (`unscaledDeltaTime`) para que no se congelen.
- **Barra de herramientas** (abajo, botones grandes): `Túnel` · `Salas` · `Borrar` · `▶` · `Listo`, más una línea de estado: "Planos: 12 bloques · Obreras libres: 2".
  - **Túnel (pincel):** al arrastrar se estampa un círculo de radio 1,5 casillas cada media casilla del trazo (se interpola entre muestras para que no queden huecos aunque el dedo vaya rápido). Sale un túnel de 3 casillas de ancho que sigue la curva. Tocar una vez estampa un solo círculo.
  - **Salas:** lista de tarjetas (icono, nombre, trabajo, capacidad). Al elegir una, su **fantasma** sigue al dedo o al cursor y se ajusta a la rejilla fina: verde si es válida; roja si no, con el motivo ("Toca otra sala", "Fuera del terreno"). Con ratón se confirma con clic. En táctil se confirma con los botones ✓ / ✕ (para evitar colocar sin querer al arrastrar).
  - **Borrar:** pincel del mismo tamaño que quita casillas en plano. Tocar una sala en plano la cancela.
- **Táctil:** la herramienta (pincel y fantasma) va justo bajo el dedo. Mientras un dedo pinta o borra aparece una **lupa** como la de iPhone al seleccionar texto: burbuja circular (~140 px, borde blanco fino y sombra) unos 100 px por encima del dedo, o a un lado si no cabe arriba, que muestra ×2 la zona bajo el dedo con el círculo del pincel en el centro. Con ratón no aparece.
- **Cámara en el modo:** táctil, un dedo usa la herramienta y dos dedos mueven y hacen zoom. Ratón: el izquierdo usa la herramienta; el derecho o la rueda pulsada mueven; la rueda hace zoom.
- **Fuera del modo,** los planos se siguen viendo tenues (los mismos cuadrados, relleno alfa 0,25), y cada sala en obras muestra una etiqueta: "Guardería · 45 %", "Sin acceso" o "Sin obreras libres".

## 6. Superficie y entrada
- Franja de superficie visible encima del hormiguero (hierba + cielo).
- **Entrada:** montículo con cráter en la cima, centrado en `x = 60` sobre el pozo: ~17 casillas de ancho y 5 de alto sobre la hierba, laderas de 45° como máximo (1 casilla de altura por columna). No se excava. Un pozo vertical de 3 casillas de ancho viene excavado desde el principio y atraviesa el montículo hasta el cráter.
- **Una sola entrada:** no se planifica nada en el montículo ni en las 2 filas de terreno pegadas a la hierba (salvo en las columnas del pozo).
- La superficie es transitable de borde a borde siguiendo el relieve (hierba y laderas del montículo); se sale por el cráter. **Puntos de salida:** los dos extremos de la superficie. La cámara ve todo el ancho, así que las hormigas desaparecen y aparecen ahí con un fundido.
- Decoración (árboles y matorrales) sobre la hierba, sin navegación.
- **Expediciones:** las hormigas caminan hasta la entrada, suben a la superficie, se van por un lateral y desaparecen. Al volver, aparecen por un lateral, caminan hasta la entrada cargando hoja o presa y entregan el botín al entrar.
- Más adelante, la entrada será el punto de los eventos: invasores, lluvia y tapones de cabeza (*Pheidole*).

**Mapa inicial (diseñado a mano):** pozo de entrada hasta la **Sala Real** a unas 24 casillas de profundidad; Guardería y Despensa conectadas con túneles cortos. Más del 70 % del terreno queda sin excavar.

## 7. Navegación (`GridNavigator : INavigator`)
- **Contrato:** el mismo `INavigator`, así que `AntActuator` y `AntSensor` no cambian.
- **Holgura:** una casilla de 0,25 es más pequeña que una hormiga. Solo son nodos las casillas excavadas cuyas **4 vecinas (arriba, abajo, izquierda y derecha) también están excavadas**. Así las hormigas no se meten en recovecos. *Probado: exigir las 8 vecinas parte los túneles diagonales del pincel de radio 1,5 en trozos sin conexión; con las 4 vecinas quedan conectados.*
- **Salas:** dentro de una sala terminada (o en plano, §4), los nodos son la fila central de las 3 filas inferiores (de puerta a puerta). Las salas solo se conectan con el exterior por sus puertas, en horizontal.
- **Superficie:** la fila central de la hierba.
- **Búsqueda:** A* (8 direcciones) con montículo binario y caché de rutas que se vacía en `CellDug`. Son unas 9.400 casillas, pero los nodos con holgura son muchos menos.
- **La hormiga no sigue la forma de cada casilla.** La ruta del A* va casilla a casilla, y si la hormiga la siguiera pegada al suelo subiría y bajaría cada escalón del borde. Por eso la ruta se procesa en 3 pasos (ver `guia/ruta_hormiga.png`):
  1. **Tensado (string pulling):** desde cada punto se salta al punto más lejano de la ruta que se vea en línea recta sin salir de las casillas con holgura. Una diagonal de 30 casillas se queda en 1 tramo recto. Resultado: 5–10 puntos en vez de 40–50.
  2. **Esquinas redondeadas:** 3 pasadas de Chaikin sobre esos puntos (cada tramo se corta al 25 % y al 75 %), así gira en curva y no en ángulo.
  3. **Bajada al suelo según la pendiente, no según las casillas:** cada punto se baja una cantidad fija (0,2 u, lo que va del centro del túnel al suelo menos media hormiga) multiplicada por lo horizontal que es el tramo (`horizontalidad⁴`). En tramos horizontales camina por el suelo; en diagonales y verticales va por el centro, y el cambio entre ambos es gradual.
- **Movimiento:** velocidad constante a lo largo de la curva y el cuerpo girado según la tangente (`alignToPath` del actuador, que ya existe), con el giro limitado por `turnSpeed` para que no tiemble.
- **Coste:** el tensado y el suavizado se hacen una vez por ruta y se guardan en la caché, no cada frame.
- `Nearest()` busca el nodo más cercano. Si una hormiga queda dentro de tierra por un bug, se recoloca en él.

## 8. Contratos nuevos (en `Core` o `Building`)
```csharp
// Building/NestGrid.cs (A6)
public enum CellType { Solid, Dug, Room, Surface, Blocked }
public class NestGrid : MonoBehaviour {
    public static NestGrid Instance { get; }
    public int Width { get; }  public int Height { get; }  public float CellSize { get; }   // 0,25
    public const int ChunkSize = 3;                                                         // bloque 3×3
    public Vector2Int WorldToCell(Vector3 world);
    public Vector3 CellToWorld(Vector2Int cell);        // centro de la casilla
    public Vector2Int CellToChunk(Vector2Int cell);
    public Vector3 FloorPoint(Vector3 world);           // suelo bajo un punto (para caminar)
    public CellType Get(Vector2Int cell);
    public Stratum StratumAt(Vector2Int cell);
    public bool HasClearance(Vector2Int cell);          // las 4 vecinas excavadas
    public event Action<Vector2Int> CellChanged;
}

// Building/IDigJobs.cs (A8) — lo usa el cerebro (B6)
public interface IDigJobs {
    bool HasWork { get; }
    /// Reserva el bloque trabajable más cercano a 'ant'. False si no hay.
    bool TryClaim(Ant ant, out DigJob job);
}
public class DigJob {
    public Vector2Int Chunk { get; }
    public Vector3 StandPosition { get; }   // dónde se coloca la obrera
    public bool FacingRight { get; }
    public float Progress { get; }          // 0..1
    public bool IsDone { get; }
    public void Work(float strength, float deltaTime);   // la llama la obrera cada frame que excava
    public void Release();                               // al interrumpir: se conserva el progreso
}
public static class Digging { public static IDigJobs Current { get; set; } }

// Core/GameEvents.cs
public struct CellDug { public Vector2Int Cell; }
public struct BuildPlanned { public RoomData Room; public Vector2Int Origin; }   // Room = null para túneles
```

## 9. Qué se toca de lo existente
| Script | Cambio |
|---|---|
| `RoomData` | + `Vector2Int footprint` (en casillas finas), `GameObject prefab`, `Sprite ghostIcon`; − `minWorkers` |
| `BuildManager` | Reescrito: salas iniciales en casillas fijas, coste +25 %, crear la sala al terminar |
| `CameraController` | Límites según `NestGrid` (corrige el bug de la Sala Real cortada en pantalla completa); gestos de dos dedos en el modo Construcción |
| `AntSelection` | Ya no usa `BuildZone.At` |
| `AntSpread` | Reparte a las obreras por el suelo de la sala |
| `AntBrain` / `AntActuator` | + objetivo **Excavar** y acción `Digging` (B6) |
| `ExpeditionSystem` | Salir y volver caminando (D7) |
| `Game.unity` | Sin zonas ni waypoints: `NestGrid`, `GridNavigator`, `DigPlanner`, `BuildModeUI` |

## 10. Riesgos de esta opción
- **Las diagonales con piezas placeholder salen onduladas** (ver el ejemplo). Las piezas finales de Naroa y los detalles sueltos lo suavizan; si aun así molesta, el render se puede cambiar a un contorno suavizado (malla), sin tocar la lógica.
- **Más nodos para el A\*:** probar pronto con 30 hormigas en la build WebGL (criterio de A7).
- **El pincel en móvil:** el dedo tapa lo que se pinta. Se dibuja un círculo de vista previa desplazado un poco por encima del dedo (como las lupas de los móviles). Probar en T3.
