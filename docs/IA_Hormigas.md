# IA de las hormigas · Guía para el equipo

**Responsable:** B · **Código:** `Assets/_Project/Scripts/Ants/` · **Escena de pruebas:** `Scenes/Test/B.unity`

Este documento explica cómo funciona la IA de las hormigas y **qué tenéis que tener en cuenta si vuestro sistema interactúa con ellas**. Si algo no encaja con lo que necesitáis, hablad con B antes de tocar estos scripts.

---

## 1. Cómo está montada

El prefab `Prefabs/AntsP/Ant.prefab` lleva estos componentes. Cada uno hace una sola cosa (GDD §11):

| Componente | Capa | Qué hace |
|---|---|---|
| `Ant` (A1, Core) | Datos | Nombre, casta, genes, stats, sala asignada (`AssignedRoom`) |
| `AntSensor` | Percepción | Responde preguntas: ¿tiene hambre?, ¿está cansada?, ¿ha llegado?, ¿cuál es la Despensa más cercana? |
| `AntBrain` | Decisión | Decide qué hacer cada 0,3 s (máquina de estados) |
| `AntActuator` | Actuación | Ejecuta órdenes: moverse, parar, trabajar, comer |
| `AntNeeds` | Necesidades | Los números de hambre y energía |
| `AntGenome` (C) | Genética | Recalcula `Ant.Stats` con los genes |

**Regla:** el cerebro decide, el actuador ejecuta y el sensor informa. Si vuestro sistema necesita que una hormiga haga algo, no le deis órdenes al actuador con el cerebro activo: el cerebro las pisará en menos de medio segundo. Ver §4.

---

## 2. Qué decide el cerebro (`AntBrain`)

Por orden de prioridad (**Necesidad > Rutina**):

1. **Comer:** si tiene hambre (≥ 0,6), hay Despensa construida y hay comida, va a la Despensa, come y vuelve. Con la tara *Glotona* gasta 2 raciones.
2. **Descansar:** si está cansada (≤ 0,25), descansa en su sala asignada o, si no tiene, en el túnel más cercano, hasta llegar a 0,95. Si llegó a 0 (agotada), descansa hasta 1.
3. **Rutina:** va a su `Ant.AssignedRoom` y trabaja ahí hasta nueva orden. Sin sala asignada, espera quieta.

- Comer va antes que descansar porque el hambre mata y el cansancio no.
- Si algo falla (no hay camino, no hay comida), espera 3 s y lo vuelve a intentar.
- **La reina** no trabaja ni se mueve: come y descansa en su sitio.

## 3. Necesidades (`AntNeeds`)

| | Valor | Notas |
|---|---|---|
| Hambre | 0 → 1 | Sube a `Stats.hungerRate × 0,5`. Con 10 s al máximo, `Ant.Kill(Starvation)` |
| Energía | 1 → 0 | Baja a `Stats.energyRate ×` factor de actividad: trabajando ×1, andando o parada ×0,3 |
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

### D · Producción (`ColonyProduction`)
Ahora que existe el cerebro, **activad *Only Count Working Ants*** en `ColonyProduction`. Así solo producen las hormigas que están en la sala trabajando, no las que han ido a comer.

### A · Construcción (`ConstructionSite`)
`ConstructionSite` suma la fuerza de **todas** las constructoras reclutadas, estén donde estén. Las hormigas ya van a la obra (su `AssignedRoom`) y trabajan allí, así que conviene contar solo las que cumplan la condición de arriba, igual que hace D.

### C · Festín / expediciones / cualquiera que "se lleve" una hormiga
Para controlar una hormiga un rato (devorarla, mandarla de expedición…), **suspended su cerebro primero** y reanudadlo al terminar:
```csharp
var brain = ant.GetComponent<IAntBrain>();
brain?.Suspend();   // a partir de aquí el cerebro no da órdenes
// ... vuestro sistema mueve / usa la hormiga con AntActuator ...
brain?.Resume();    // vuelve a decidir sola desde cero
```

### Genes con comportamiento
El cerebro y las necesidades leen `GeneData.behaviorTag`. Ahora mismo se usa **`"Glotona"`** (come doble ración). Si creáis un gen que deba cambiar el comportamiento, poned un `behaviorTag` y avisad a B para programarlo.

---

## 5. Pendiente para más adelante

- **Transporte (carga):** al andar cargada, una hormiga debe gastar más energía según el peso. Está marcado con `TODO (TRANSPORTE)` en `AntNeeds.EnergyDrainFactor()`, con la fórmula propuesta. Quien haga el transporte debe exponer la carga actual y hablarlo con B.
- **Dormitorio (beta):** descansar ahí será ×2. Falta un `RoomType` nuevo en `Core/Enums.cs` (A1). El hueco está preparado en `AntNeeds.RestMultiplier()`.
- **Balance:** el hambre va a la mitad mediante `AntNeeds` (*Hunger Rate Scale* = 0,5). Si D ajusta `hungerRate` directamente en las castas, hay que volver a poner ese valor a 1.
