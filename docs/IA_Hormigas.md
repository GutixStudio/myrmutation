# IA de las hormigas · Guía para el equipo

**Responsable:** B · **Código:** `Assets/_Project/Scripts/Ants/` · **Escena de pruebas:** `Scenes/Test/B.unity`  
**Última actualización:** 05/11/2026 · B8: obreras ociosas en la Sala Real y reparto por el suelo de las salas

Este documento explica cómo funciona la IA de las hormigas y **qué tenéis que tener en cuenta si vuestro sistema interactúa con ellas**. Si algo no encaja con lo que necesitáis, hablad con B antes de tocar estos scripts.

---

## 1. Cómo está montada

El prefab `Prefabs/AntsP/Ant.prefab` lleva estos componentes. Cada uno hace una sola cosa (GDD §11):

| Componente | Capa | Qué hace |
|---|---|---|
| `Ant` (A1, Core) | Datos | Nombre, casta, genes, stats, sala asignada (`AssignedRoom`) |
| `AntSensor` | Percepción | Responde preguntas: ¿tiene hambre?, ¿está cansada?, ¿ha llegado?, ¿cuál es la Despensa más cercana? |
| `AntBrain` | Decisión | Decide qué hacer cada 0,3 s (máquina de estados) |
| `AntActuator` | Actuación | Ejecuta órdenes: moverse, parar, trabajar, comer, excavar |
| `AntNeeds` | Necesidades | Los números de hambre y energía |
| `AntGenome` (C) | Genética | Recalcula `Ant.Stats` con los genes. Ya va en el prefab (las crías de la Guardería también lo llevan) |
| `AntSpread` (A5 + B8) | Visual y reparto | Desplaza un poco el dibujo (hijo `Visual`) de cada hormiga y le da su propio orden de dibujado. **B8:** además elige el sitio del suelo de cada sala al que va la hormiga (ver §2) |

**Regla:** el cerebro decide, el actuador ejecuta y el sensor informa. Si vuestro sistema necesita que una hormiga haga algo, no le deis órdenes al actuador con el cerebro activo: el cerebro las pisará en menos de medio segundo. Ver §4.

---

## 2. Qué decide el cerebro (`AntBrain`)

Por orden de prioridad (**Necesidad > Excavar > Rutina > Ociosa**):

1. **Comer:** si tiene hambre (≥ 0,6), hay Despensa construida y hay comida, va a la Despensa, come y vuelve. Con la tara *Glotona* gasta 2 raciones.
2. **Descansar:** si está cansada (≤ 0,25), descansa en su sala asignada o, si no tiene, en el túnel más cercano, hasta llegar a 0,95. Si llegó a 0 (agotada), descansa hasta 1.
3. **Excavar (B6):** si **no tiene sala**, no es la reina y hay planos (`Digging.Current.HasWork`), pide un bloque con `TryClaim`, va a su `StandPosition`, se pone mirando al bloque (`AntAction.Digging`) y llama a `DigJob.Work(ant, fuerza, dt)` cada frame hasta `IsDone`. Al terminar pide el siguiente en el mismo frame (excavación continua). Si el hambre, el cansancio, una sala asignada, una suspensión (expedición) o la muerte la interrumpen, hace `Release()` (el avance se conserva) y, cuando vuelva a estar libre, pide otra vez.
4. **Rutina:** va a su `Ant.AssignedRoom` y trabaja ahí hasta nueva orden.
5. **Ociosa (B8):** sin sala asignada y sin nada que excavar (`BrainGoal.Idle`), va a la **Sala Real** y da paseos cortos por su suelo (como mucho 1,5 u, a media velocidad, con paradas de 5–10 s), sin acercarse a las puertas ni al centro, donde está la reina. Si no hay Sala Real o no se puede llegar, espera junto a la entrada, en la superficie.

- Comer va antes que descansar porque el hambre mata y el cansancio no.
- Si algo falla (no hay camino, no hay comida), espera 3 s y lo vuelve a intentar.
- **La reina** no trabaja ni se mueve: come y descansa en su sitio.
- **Reparto en las salas (B8):** para trabajar, comer o descansar en una sala ya no va a su `WorkPosition` (el centro del suelo), sino al sitio que le da `AntSpread.SpotIn(sala)`: el más separado de las demás hormigas que están en ese suelo o van hacia él, dejando 1 u libre en cada puerta. No hay plazas: caben todas, más juntas cuantas más haya. **Si vuestro sistema necesita saber si una hormiga está en una sala, no comparéis su posición con `WorkPosition`**: usad `AntActuator.WorkRoom` (ver §4).
- **Puertas libres (B8):** si una hormiga se queda parada en una puerta o en su zona de acceso (esperando, buscando bloque…), el cerebro la aparta hacia dentro de esa sala. Si el túnel más cercano para descansar es una puerta, descansa dentro de la sala.

## 3. Necesidades (`AntNeeds`)

| | Valor | Notas |
|---|---|---|
| Hambre | 0 → 1 | Sube a `Stats.hungerRate × 0,5`. Con 10 s al máximo, `Ant.Kill(Starvation)` |
| Energía | 1 → 0 | Baja a `Stats.energyRate ×` factor de actividad: trabajando o excavando ×1, andando o parada ×0,3 |
| Descanso | +0,1/s | 10 s de 0 a 1 |
| Agotada | energía 0 | No trabaja (`CanWork = false`) hasta recuperar el 100 % |

Todos los valores se ajustan en el Inspector del prefab. Los genes afectan a través de `Ant.Stats` (lo calcula `AntGenome`).

---

## 4. Lo que tenéis que saber según vuestro sistema

### Todos: cómo saber si una hormiga está trabajando de verdad
Estar asignada a una sala (`AssignedRoom`) **no** significa estar en ella: puede haber ido a comer o a descansar. Una hormiga está trabajando en una sala cuando:
```csharp
var act = ant.GetComponent<AntActuator>();
bool trabajandoAqui = act != null && act.Current == AntAction.Working && act.WorkRoom == sala;
```

Para saber si está **excavando**: `act.Current == AntAction.Digging` (en el panel sale "Excavando"; en la burbuja de B7, el icono de excavar).

### A · Excavación (`DigPlanner`) · *B6*
El cerebro ya hace de cuadrilla: **quitad `DigTestDriver` de `Game.unity`** (o desactivadlo). Si se queda con *Use Crew*, suspende el cerebro a obreras libres y las dos cosas se pisan. En las escenas de prueba de A con el `AntBrain` apagado (*Use Crew* desactivado) puede seguir.

### D · Producción (`ColonyProduction`)
*Only Count Working Ants* está **activado** en `Game.unity`: solo producen las hormigas que están en la sala trabajando, no las que han ido a comer.
- En la alfa la conversión hongo → comida está en modo **Direct** (automática). Las obreras asignadas a la **Despensa** comen allí, pero no producen: en modo *Storage* serían ellas quienes convierten el hongo (pendiente de decidir en el balance).

### A · Construcción (`ConstructionSite`) · *hecho en A5*
- La obra **solo recluta** si hay al menos `minWorkers` obreras **libres** (`AssignedRoom == null`, no reina). Si no, no manda a nadie. El menú de construcción (`BuildMenuUI`) ya desactiva las salas para las que faltan obreras.
- Cuenta solo las constructoras que cumplen la condición de arriba (trabajando **en** la obra) y, con *Require Min Workers On Site* (en `BuildManager`, activado), **no avanza hasta que estén allí las mínimas**. Si alguna se va a comer o a descansar, la obra se para.
- Si el grupo baja del mínimo (muerte, reasignación), libera al resto. Al terminar, todas quedan **sin sala** (vuelven a estar libres).

### C · Festín / expediciones / cualquiera que "se lleve" una hormiga
Para controlar una hormiga un rato (devorarla, mandarla de expedición…), **suspended su cerebro primero** y reanudadlo al terminar:
```csharp
var brain = ant.GetComponent<IAntBrain>();
brain?.Suspend();   // a partir de aquí el cerebro no da órdenes
// ... vuestro sistema mueve / usa la hormiga con AntActuator ...
brain?.Resume();    // vuelve a decidir sola desde cero
```

### D · Expediciones (`ExpeditionSystem`)
Al salir, cada hormiga tiene el cerebro **suspendido** y el GameObject **desactivado**: desaparece de `Ant.All` y no cuenta para nada (ni comida, ni obras, ni selección) hasta que vuelve, cuando se reactiva y se reanuda el cerebro.
- Ojo: si una constructora se va de expedición sigue apuntada a su obra, así que en modo estricto la obra puede pararse hasta que vuelva.

### D · Invierno (`SeasonRules`)
- `SeasonRules` cobra en invierno 2 de comida por hormiga y día (y mata de hambre si no llega), y bloquea las expediciones.
- `SeasonRules.HungerMultiplier` (×1,5 en invierno) **todavía no lo lee `AntNeeds`**. Si se decide usarlo, es una línea en `AntNeeds.Update`: multiplicar `stats.hungerRate` por `SeasonRules.HungerMultiplier`. En ese caso conviene quitar el mantenimiento diario para no cobrar dos veces.

### Genes con comportamiento
El cerebro y las necesidades leen `GeneData.behaviorTag`. Ahora mismo se usa **`"Glotona"`** (come doble ración). Si creáis un gen que deba cambiar el comportamiento, poned un `behaviorTag` y avisad a B para programarlo.

---

## 5. Selección y panel de hormiga (B4)

**Scripts:** `Scripts/Ants/UI/` · **Prefab:** `Prefabs/AntsP/AntPanel.prefab`

- Tocar o hacer clic cerca de una hormiga la selecciona (sin colliders: se busca la más cercana al punto). Tocar en otro sitio la deselecciona.
- *(A5)* La distancia se mide al **dibujo** de la hormiga (hijo `Visual`, que mueve `AntSpread`), no a su posición real. Si hay varias bajo el dedo, **tocar otra vez en el mismo sitio selecciona la siguiente**, así se llega a todas aunque estén amontonadas. `AntSelection.AntsAt(punto)` devuelve la lista ordenada por distancia.
- La hormiga seleccionada está en `AntSelection.Current`.
- **Evento `AntSelected`:** se publica al seleccionar, y también al deseleccionar con **`Ant = null`**. Si os suscribís, comprobad siempre si es `null`:
  ```csharp
  void OnAntSelected(AntSelected e) { if (e.Ant == null) { /* ninguna seleccionada */ } }
  ```
- El panel muestra nombre, casta, estado, genes (el `icon` de cada `GeneData`; si no tiene, un cuadro de su `tint`) y barras de hambre y energía.
- **Asignar a sala:** lista con el rendimiento de la hormiga en cada sala (`ColonyProduction.GetEfficiency`) y su ocupación (`workerCapacity`). Con la lista abierta, también se puede tocar la sala en el mapa. La Sala Real no aparece y la reina no se puede asignar. La opción **Ninguna** la deja libre (para construir).
- **Botones de acción que hay ahora en `Game.unity`:**

| Botón | Lo registra | Cuándo sale |
|---|---|---|
| *Devorar* | `FeastSystem` (C3) | Hormiga viva (y Cámara del Festín si *Require Feast Room*) |
| *Comer presa* | `PreyFeastAction` (A5) | Obrera, hay presas guardadas y Cámara del Festín construida |
| *Apuntar / Quitar de expedición* | `ExpeditionSystem` (D4) | Según si está apuntada |

  Como pueden salir 3 o más, el contenedor de acciones del panel debe repartirlos en filas (Grid de 2 columnas o similar).

### C (y cualquiera) · Añadir un botón al panel (p. ej. "Devorar")
No hace falta tocar `AntPanelUI`. Desde vuestro script:
```csharp
void OnEnable()  => AntPanelAction.Register("Devorar", ant => !ant.IsQueen, ant => Devorar(ant));
void OnDisable() => AntPanelAction.Unregister("Devorar");
```
- El segundo parámetro decide cuándo se ve el botón (se comprueba dos veces por segundo).
- El tercero es lo que hace al pulsarlo. Recordad suspender el cerebro si os lleváis la hormiga (ver §4).
- Si la hormiga muere, el panel se cierra solo.

### A · Aviso
`BuildManager` abre su menú al tocar una **zona libre**. Si hay una hormiga encima de una zona libre y se toca, se abren el menú y el panel a la vez. Es poco habitual (las hormigas suelen estar en salas construidas o túneles).

---

## 6. Pendiente para más adelante

- **Transporte (carga):** al andar cargada, una hormiga debe gastar más energía según el peso. Está marcado con `TODO (TRANSPORTE)` en `AntNeeds.EnergyDrainFactor()`, con la fórmula propuesta. Quien haga el transporte debe exponer la carga actual y hablarlo con B.
- **Dormitorio (beta):** descansar ahí será ×2. Falta un `RoomType` nuevo en `Core/Enums.cs` (A1). El hueco está preparado en `AntNeeds.RestMultiplier()`.
- **Invierno:** usar o no `SeasonRules.HungerMultiplier` en `AntNeeds` (ver §4).
- **Balance:** el hambre va a la mitad mediante `AntNeeds` (*Hunger Rate Scale* = 0,5). Si D ajusta `hungerRate` directamente en las castas, hay que volver a poner ese valor a 1.
