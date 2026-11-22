# MYRMUTATION · Game Design Document

**Estudio:** Gutix (GutixStudio)  
**Género:** gestión de colonia + roguelite · 2D vista lateral  
**Plataforma:** Web (WebGL) · itch.io · Chrome y Firefox · PC, tablet y móvil  
**Motor:** Unity 6000.0.57f1 LTS  
**Asignatura:** Juegos para Web y Redes Sociales · Grado en Diseño y Desarrollo de Videojuegos · URJC · Curso 2025-26

## Control de versiones

| Versión | Fecha      | Autor  | Cambios                                                                                                                                                                                                                                                                                                     |
|---------|------------|--------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 0.1     | 06/10/2026 | Gutix  | Primer borrador completo del GDD (entrega Alfa)                                                                                                                                                                                                                                                             |
| 0.1.1   | 09/10/2026 | Alonso | Controles: precisión del clic izquierdo                                                                                                                                                                                                                                                                     |
| 0.2     | 11/10/2026 | Gutix  | Actualizado a lo implementado en la alfa (v0.1-alpha): valores reales de salas, castas, cría, producción, Festín, expediciones e invierno; HUD con pestañas; reglas de construcción (mínimo de obreras); Festín obligatorio para devorar; uso de la biomasa; equipo por áreas; estado de la alfa y recortes |
| 0.2.1   | 11/10/2026 | Gutix  | Revisión del equipo: nombres completos, monetización (fase actual) y referencias |
| 0.3     | 14/11/2026 | Alonso | Beta 1-2: sección 5.3 reescrita con el diseño de construcción (excavar, puertas laterales, modo Construcción, sin mínimos de obreras); superficie y entrada; sección 8 con la claridad de la beta (log, iconos de estado, estadísticas) |
| 0.3.1   | 15/11/2026 | Ayoze  | Revisión de la entrega beta (v0.2-beta): bucle y salas con la excavación; IA con Excavar y Ociosa; Despensa que produce comida; expediciones por la entrada y de tamaño libre; controles del modo Construcción; sucesos de economía; tabla de genes; tecnología de la construcción; estado de la beta |

## Índice

1.  [Concepto](#1-concepto)
2.  [Ficha técnica](#2-ficha-técnica)
3.  [Público objetivo](#3-público-objetivo)
4.  [Narrativa](#4-narrativa)
5.  [Mecánicas](#5-mecánicas)
6.  [Controles](#6-controles)
7.  [Pantallas y flujo](#7-pantallas-y-flujo)
8.  [Interfaz (HUD)](#8-interfaz-hud)
9.  [Arte](#9-arte)
10. [Sonido](#10-sonido)
11. [Tecnología](#11-tecnología)
12. [Monetización y modelo de negocio](#12-monetización-y-modelo-de-negocio)
13. [Alcance por entregas](#13-alcance-por-entregas)
14. [Marketing y redes sociales](#14-marketing-y-redes-sociales)
15. [Equipo](#15-equipo)
16. [Referencias](#16-referencias)

## 1. Concepto

### 1.1 Pitch

> Gestiona un hormiguero de Glotonas, una especie de hormiga que evoluciona comiéndose a otras criaturas… y a sí misma. Excava salas, cría a tus obreras, cultiva hongos y decide a quién devorar para que tu colonia herede los mejores rasgos antes de que llegue el invierno.

### 1.2 Introducción

La receta del éxito de las hormigas es la colaboración: solas son inútiles, pero juntas consiguen logros increíbles. Construyen su hábitat, crían ganado, practican la agricultura y mantienen relaciones simbióticas. Y también hacen la guerra.

Algunas especies son tan letales que las demás han tenido que especializarse durante millones de años para sobrevivir: cabezas que taponan la entrada del nido, huidas masivas con las larvas a cuestas, mandíbulas que cortan hojas…

Las Glotonas encontraron un atajo: en lugar de esperar a que la evolución las especialice, se comen la especialización ajena.

### 1.3 Pilares de diseño

1.  Cada hormiga importa. Pocas hormigas, cada una con nombre, casta, necesidades y genes.
2.  Mutar es una decisión, no un premio. Devorar cuesta una hormiga, puede salir mal (taras) y lo que hereda la reina lo heredan todas.
3.  La colonia vive sola. El jugador decide el qué; las hormigas deciden el cuándo y el cómo.
4.  Partidas cortas y distintas. Roguelite: cada colonia evoluciona de forma diferente.

### 1.4 Referencias

| Juego               | Qué tomamos                                                         |
|---------------------|---------------------------------------------------------------------|
| **Fallout Shelter** | Gestión de salas en corte lateral, asignación de habitantes         |
| **Mewgenics**       | Herencia de rasgos, taras, rejugabilidad, humor negro cartoon       |
| **Spore**           | Cambios visibles en el cuerpo por partes; fases evolutivas (futuro) |
| **Los Sims**        | Personajes autónomos con necesidades y rutinas                      |

### 1.5 Originalidad

La gestión de colonias es un género conocido; lo que diferencia a Myrmutation es el canibalismo mutagénico en dos niveles (individuo → linaje) y que las mutaciones cambian el cuerpo y el comportamiento de las hormigas, no solo sus números.

## 2. Ficha técnica

|                           |                                                       |
|---------------------------|-------------------------------------------------------|
| Título                    | Myrmutation                                           |
| Género                    | Gestión / simulación de colonia + roguelite           |
| Vista                     | 2D lateral (sección de hormiguero)                    |
| Jugadores                 | 1                                                     |
| Duración de partida       | 10–15 min (alfa) · 30–45 min (release)                |
| Plataforma                | Navegador (WebGL) en PC, tablet y móvil               |
| Distribución              | itch.io                                               |
| Idiomas                   | Español (alfa) · Español e inglés (release)           |
| Clasificación orientativa | PEGI 7 (violencia cartoon entre insectos, sin sangre) |

## 3. Público objetivo

- **Principal:** jugadores de 16–35 años de juegos de gestión y roguelites (Fallout Shelter, Mewgenics, RimWorld, Oxygen Not Included) que buscan partidas cortas.
- **Secundario:** jugadores casuales de navegador y móvil atraídos por el estilo cartoon y el tema curioso (hormigas reales).
- **Perfil:** les gusta experimentar con combinaciones, ver crecer un sistema y contar sus partidas ("mi colonia de mieleras gigantes…").

## 4. Narrativa

### 4.1 Premisa

Las Glotonas parecen hormigas normales: misma jerarquía (una reina y sus obreras) y misma forma de construir el nido. Pero practican el canibalismo. La Dra. Coleóptera, una escarabajo científica algo excéntrica, descubre que estas hormigas son capaces de absorber el material genético de lo que comen y desarrollar rasgos de otras especies. Decide estudiarlas… guiándote a ti, la mente de la colonia.

### 4.2 Personajes

| Personaje                | Rol                                                                                                                            |
|--------------------------|--------------------------------------------------------------------------------------------------------------------------------|
| **La reina**             | Corazón de la colonia. Si muere, termina la partida. Hereda los genes de las mutantes que devora y los transmite a sus crías.  |
| **Las obreras Glotonas** | Cada una con nombre, casta y genes. Trabajan, comen, descansan… y a veces son devoradas.                                       |
| **Dra. Coleóptera**      | Narradora y tutorial. Comenta con humor lo que hace el jugador y escribe el Mirmecodex, su cuaderno de investigación. *(beta)* |

### 4.3 El Mirmecodex *(beta)*

Cuaderno de la doctora. Cada gen descubierto se registra con una ilustración, una descripción y un comentario suyo. Se conserva entre partidas y permite empezar nuevas colonias con 1–2 genes ya descubiertos.

### 4.4 Tono

Cartoon desenfadado. El canibalismo y las mutaciones se tratan con humor, sin sangre ni gore: las hormigas tienen ojos grandes y expresivos, y las víctimas desaparecen con un efecto de "glup".

### 4.5 Arco narrativo *(pendiente para la beta)*

La investigación de la doctora avanza partida a partida a través del Mirmecodex. Se plantea un misterio de fondo sobre el origen de la capacidad de las Glotonas, que conecta con el final evolutivo previsto para versiones futuras.

## 5. Mecánicas

### 5.1 Bucle de juego

1.  **Construir:** dibujar túneles y salas en el plano para que las obreras los excaven.
2.  **Criar:** la reina pone huevos; el jugador decide cómo alimentar a cada larva y eso fija su casta.
3.  **Asignar:** colocar a cada obrera en una sala; ella decide cuándo trabajar, comer o descansar.
4.  **Producir:** hojas → hongo → comida.
5.  **Explorar:** enviar expediciones que traen hojas y presas.
6.  **Mutar:** devorar para ganar genes; la reina hereda y transmite.
7.  **Sobrevivir:** llegar al final del invierno.

### 5.2 Estructura de partida (roguelite)

- **Victoria:** sobrevivir hasta el final del invierno (4 estaciones).
- **Derrota (Game Over):** muere la reina (hambre, ataque o, en la beta, rebelión) o el jugador pulsa Salir.
- **Tiempo:** real con pausa y velocidades ×1, ×2 y ×3. En la alfa: 1 día = 20 s, 5 días por estación y 4 estaciones (primavera → invierno). Se gana al terminar el día 20 del invierno.
- **Inicio de partida (alfa):** reina + 4 obreras (2 Medias, 1 Menor, 1 Mayor), con la Sala Real, la Guardería y la Despensa ya construidas. Recursos iniciales: 50 de comida y 10 de hojas.
- **Meta-progresión** *(beta)*: el Mirmecodex conserva los genes descubiertos; se pueden elegir 1–2 genes iniciales para la reina.

### 5.3 Construcción

El hormiguero se construye **excavando**. El jugador dibuja túneles y coloca salas sobre un plano azul, y las obreras libres los excavan bloque a bloque. Ya no hay zonas prediseñadas ni mínimos de obreras.

- **El terreno:** una rejilla fina excavable (casillas de 0,25 unidades, agrupadas en bloques de 3×3) de 120×78 casillas más 12 filas de superficie. Cada casilla es tierra sin excavar, excavada, sala, superficie o bloqueada.
- **Modo Construcción:** el botón *Construir* de la barra inferior muestra un plano azul sobre el terreno excavable. Al entrar se pausa el tiempo (con ▶ se reanuda dentro del modo; al salir vuelve a la velocidad anterior).
  - *Túneles:* se dibujan con un pincel; en táctil la vista previa se desplaza un poco por encima del dedo. Borrar usa el mismo pincel.
  - *Salas:* se coloca un fantasma que se ve verde si cabe y rojo si no. En táctil se confirma con ✓ / ✕. Tocar una sala en plano la cancela.
  - *Controles:* en táctil, un dedo usa la herramienta y dos dedos mueven la cámara y hacen zoom; con ratón, el botón izquierdo usa la herramienta, el derecho (o la rueda pulsada) mueve y la rueda hace zoom.
  - Una línea de estado indica "Planos: N bloques · Obreras libres: M".
- **Excavación:** las obreras libres son la cuadrilla de excavación. Cada plano se convierte en trabajos por bloques de 3×3, con hasta 2 obreras por bloque; si una lo suelta, el progreso se conserva. Sobre el bloque se ve una grieta en tres fases y salen terrones. Una obrera libre sin planos espera junto a la Sala Real o la entrada. La **fuerza** de cada obrera (de su casta y sus genes) acelera la excavación: cada segundo aporta *fuerza ÷ coste del bloque* a su avance, así que una Mayor (fuerza 2) excava el doble de rápido que una Media (1) y casi el triple que una Menor (0,7).
- **Salas:** tienen tamaño fijo (Sala Real 27×15 casillas, el resto 21×12) y **solo se entran por las puertas laterales**, situadas en la parte inferior de la primera y la última columna. Una sala se empieza por sus zonas de acceso (si ninguna está excavada ni en plano, aparece "Sin acceso") y se termina al excavar su último bloque. Reglas de colocación: dejar un anillo de 2 casillas alrededor, no tocar otras salas y mantener libres las zonas de acceso de 3×3.
- **Obras:** fuera del modo Construcción se ven los planos tenues y, sobre cada sala en obras, una etiqueta con su avance ("45 %") o su problema ("Sin acceso", "Sin obreras libres").
- **Coste:** cada bloque tiene un coste de trabajo que se multiplica por el factor de su estrato.
  - *Túneles:* (casillas en plano del bloque / 9) × 1,5.
  - *Salas:* el coste de la sala se reparte entre sus bloques (28 en una sala normal, 45 en la Sala Real) y **cada sala ya construida sube un 25 %** el coste de la siguiente. Coste base: Cámara del Festín 40, Sala de hongos 35, Guardería 30, Despensa 20.
- **Estratos:** tierra (24 filas), arcilla (27) y roca (27), cada uno más duro de excavar que el anterior: el coste se multiplica por ×1 en tierra, ×1,33 en arcilla y ×2 en roca (la misma velocidad que en la alfa: ×1, ×0,75 y ×0,5). Abajo hace más calor en invierno, pero hay riesgo de inundación *(beta)*; arriba se excava más rápido, pero los invasores llegan antes *(beta)*.
- **Mapa inicial:** el pozo de entrada, la Sala Real a unas 24 casillas de profundidad, y la Guardería y la Despensa ya construidas y conectadas por túneles. Las tres salas iniciales no cuentan para la subida de coste.
- **Navegación:** las hormigas solo andan por casillas excavadas y entran en las salas por sus puertas laterales.

| Sala                  | Función                                                                             | Tamaño (casillas) | Capacidad | Entrega |
|-----------------------|-------------------------------------------------------------------------------------|-------------------|-----------|---------|
| **Sala Real**         | Vive la reina; pone huevos y devora mutantes para heredar sus genes                 | 27×15             | —         | Alfa    |
| **Guardería**         | Huevo → larva → pupa → adulta; aquí se elige la casta. Sus obreras aceleran la cría | 21×12             | 4         | Alfa    |
| **Despensa**          | Las hormigas vienen aquí a comer (sin Despensa no comen); sus obreras convierten el hongo en comida | 21×12             | 4         | Alfa    |
| **Sala de hongos**    | Convierte hojas en hongo                                                            | 21×12             | 4         | Alfa    |
| **Cámara del Festín** | Necesaria para devorar y comer presas (mutación y herencia)                         | 21×12             | 4         | Alfa    |
| **Sala de Descanso**  | Recuperar energía y acicalarse                                                      |                   |           | Beta    |
| **Basurero**          | Restos y cadáveres; reduce infecciones                                              |                   |           | Beta    |
| **Cuarentena**        | Aísla hormigas infectadas                                                           |                   |           | Beta    |
| **Solárium**          | Acelera la cría, pero la expone                                                     |                   |           | Release |

Todas las salas de trabajo admiten **4 obreras**, como en la alfa. En la Sala Real no se asignan obreras: es el punto de reunión de las obreras ociosas.

### 5.4 Hormigas: necesidades y comportamiento

Cada hormiga es un agente autónomo: el jugador asigna su puesto y ella decide según sus necesidades.

| Necesidad            | Efecto                                                                                                                                                                      | Entrega |
|----------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------|
| **Hambre**           | Sube con el tiempo (según casta y genes). Al 60 % va a la Despensa a comer (1 ración; 2 si es *Glotona*). Si pasa 10 s al máximo, muere                                     | Alfa    |
| **Energía**          | Baja más rápido trabajando (×1) que andando o parada (×0,3). Al 25 % va a descansar a su sala hasta el 95 %. A 0 queda agotada: deja de trabajar hasta recuperarse del todo | Alfa    |
| **Higiene**          | Si baja, aumenta el riesgo de infección                                                                                                                                     | Beta    |
| **Lealtad / estrés** | Baja con el canibalismo y el hambre; a 0 hay rebelión                                                                                                                       | Beta    |

IA: arquitectura en tres capas (percepción, decisión y actuación). La decisión es una máquina de estados jerárquica con prioridades (Emergencia \> Necesidad \> Rutina); en la alfa: Comer \> Descansar \> Trabajar en su sala. En la beta: Comer \> Descansar \> Excavar (obreras sin sala y con planos) \> Trabajar en su sala \> Ociosa (obreras sin sala ni planos: van a la Sala Real o junto a la entrada y dan paseos cortos). Dentro de una sala, cada obrera se coloca en el sitio del suelo más separado de las demás, lejos de las puertas. Cada hormiga decide cada 0,3 s. La reina no se mueve: come y descansa en su sitio. Los genes modifican el comportamiento: por ejemplo, una hormiga *Glotona* come el doble. Este sistema se desarrolla también para la asignatura de Comportamiento de Personajes.

### 5.5 Castas

Como en las hormigas reales, la casta no depende de los genes, sino de la alimentación de la larva: cuanta más comida, mayor casta.

| Casta               | Coste de cría (comida) | Fuerza | Velocidad | Destaca en                  | Flojea en                  |
|---------------------|------------------------|--------|-----------|-----------------------------|----------------------------|
| **Menor**           | 3                      | 0,7    | 1,6       | Guardería y Hongos (+50 %)  | Construcción (poca fuerza) |
| **Media**           | 6                      | 1      | 1,5       | Despensa (+25 %)            | —                          |
| **Mayor** (soldado) | 10                     | 2      | 1,1       | Construcción (mucha fuerza) | Guardería y Hongos (−50 %) |

La asignación es libre, pero cada casta rinde distinto en cada sala (**afinidad**). La interfaz muestra el rendimiento al asignar.

### 5.6 Recursos

| Recurso              | Cómo se obtiene                         | Para qué                                                                                                                                                                                        |
|----------------------|-----------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Comida**           | Del hongo, en la Despensa (1 hongo → 2 comida cada 5 s por obrera; sin obreras, al 25 %) | Comer, poner huevos (2) y criar larvas (3/6/10 según casta)                                                                                                                                     |
| **Hojas**            | Expediciones (según la carga del grupo, ver §5.8) | Cultivar hongo (2 hojas → 2 hongo por ciclo de 10 s, más rápido con más obreras y mejor afinidad)                                                                                               |
| **Hongo**            | Sala de hongos (a partir de hojas)      | Se convierte en comida                                                                                                                                                                          |
| **Biomasa genética** | Devorar hormigas (+3) o presas (+5)     | En la alfa solo se acumula. **Beta:** coste de que la reina herede un gen (fijar un gen en el linaje cuesta biomasa) y moneda del Mirmecodex para desbloquear genes iniciales. Nunca da comida. |

### 5.7 Mutación: canibalismo en dos niveles

**Nivel 1 · Individuo:** una obrera devora a otra hormiga o a una presa y muta ella sola. Es el experimento. Requiere tener construida la Cámara del Festín.

| Víctima                  | Resultado                                   |
|--------------------------|---------------------------------------------|
| Hormiga propia con genes | Copia uno de sus genes (80 %) · tara (20 %) |
| Hormiga propia sin genes | Nada (60 %) · tara (40 %)                   |
| Presa de expedición      | Su gen (70 %) · tara (20 %) · nada (10 %)   |

**Cría (alfa):** la reina pone un huevo cada 15 s si hay Guardería, sitio (máx. 6 crías) y 2 de comida. Huevo (10 s) → larva: espera a que el jugador elija su casta (pestaña *Guardería*, que avisa con un !) → larva alimentada (10 s) → pupa (10 s) → nace la obrera en la Guardería. Las obreras asignadas a la Guardería aceleran el proceso.

**Nivel 2 · Linaje:** la reina devora a una mutante y sus genes (y taras) pasan al genoma de la casta de esa mutante. Todas las crías futuras de esa casta nacen con ellos. Con el tiempo, las castas se diferencian visualmente: es un polimorfismo construido por el jugador.

**Diseño de genes:**

1.  Cada gen cambia una regla, no solo un porcentaje.
2.  Se ve en el cuerpo (hormiga modular de 5 ranuras: cabeza, antenas, tórax, abdomen, patas).
3.  Se ve en el comportamiento.
4.  Mutar es un evento: efecto visual, mensaje y entrada en el Mirmecodex.
5.  Las taras también se ven.

**Genes de la alfa:**

| Gen                      | Origen                               | Ranura | Efecto                                   |
|--------------------------|--------------------------------------|--------|------------------------------------------|
| **Patas de corredora**   | Hormiga del desierto (*Cataglyphis*) | Patas  | Velocidad +50 %, se cansa un 25 % antes  |
| **Mandíbula cortadora**  | Cortadora de hojas (*Atta*)          | Cabeza | Fuerza +30 %, carga +1                   |
| **Glotona** (tara)       | Mutación fallida                     | —      | Hambre ×2 y come doble ración            |

**Genes previstos (beta y release):** Abdomen-despensa (*Myrmecocystus*), Cabeza-tapón (*Pheidole obtusospinosa*), Instinto de evacuación (*Pheidole desertorum*), Glándula ácida (*Formica*), Ojos compuestos (*Harpegnathos*), Alas, Mandíbula-trampa (*Odontomachus*), Aguijón (*Solenopsis*), Coraza (*Cephalotes*), Seda tejedora (*Oecophylla*)… y recetas que combinan dos genes en uno nuevo.

### 5.8 Expediciones

- **Alfa**: se apuntan 1–3 hormigas desde su panel (o se eligen en la pestaña *Expedición*) y se envían. Tardan 30 s y vuelven con 3–5 hojas cada una y, con un 30 % de probabilidad (+10 % por hormiga extra), con una presa (hormiga del desierto o cortadora). La presa se guarda y, con la Cámara del Festín construida, cualquier obrera se la puede comer con el botón *Comer presa* de su panel: gen de la presa 70 % · tara 20 % · nada 10 %. No hay expediciones en invierno.
- **Beta (implementado):** en la pestaña *Expedición* se elige con − / + cuántas hormigas van, de 1 a todas las disponibles (nunca la reina); también se pueden marcar desde la lista de hormigas. Primero van las apuntadas, después las obreras sin sala y, si faltan, el resto. Las hormigas caminan hasta la entrada, salen por un extremo de la superficie y, a los 30 s, vuelven por el mismo extremo cargando el botín, que cada una entrega al llegar a la entrada (§8). El botín depende de la carga y la fuerza de cada hormiga (de su casta y sus genes):
  - *Hojas:* 8 × √(carga total del grupo), ±15 %. Cada hormiga extra aporta menos. Carga: Menor 1, Media 1,5, Mayor 2.
  - *Presas:* probabilidad de 1 − 0,8^(fuerza total), con un máximo del 80 %. Por cada 4 de fuerza más se tira una presa extra, hasta 3. Las presas se comen en la Cámara del Festín (menú del Festín) o con *Comer presa*.
  - Una expedición a la vez y ninguna en invierno. Mandar a todas no siempre compensa: el botín crece cada vez menos y las que están fuera no trabajan.
- **Beta (pendiente):** se elige una zona (pradera, charca, nido rival…) y durante la expedición aparecen 1–3 eventos de texto con decisiones; algunas opciones solo aparecen si el grupo tiene ciertos genes.

### 5.9 Estaciones y eventos

| Evento             | Descripción                                                                                                                            | Entrega |
|--------------------|----------------------------------------------------------------------------------------------------------------------------------------|---------|
| **Invierno**       | Marca el final. El hambre sube ×1,5, cada hormiga consume 2 de comida al día (si no hay, mueren de hambre) y no hay expediciones       | Alfa    |
| **Ophiocordyceps** | Hongo parásito que llega con las expediciones, controla a la hormiga y se contagia. Se contrarresta con cuarentena, higiene y basurero | Beta    |
| **Invasores**      | Arañas u hormigas rivales entran en el nido; las hormigas interrumpen su rutina para defenderse                                        | Beta    |
| **Inundación**     | Las lluvias o tocar el nivel freático inundan las salas profundas                                                                      | Beta    |
| **Derrumbe**       | Excavaciones extensas pueden colapsar                                                                                                  | Beta    |

### 5.10 Dificultad y progresión

- **Aprendizaje:** la partida empieza con la reina, 4 obreras y las salas básicas (Real, Guardería y Despensa); el jugador amplía con Hongos (comida a largo plazo) y Festín (mutaciones). En la beta, la Dra. Coleóptera guía los primeros pasos.
- **Tensión:** el invierno obliga a acumular comida; devorar da poder, pero cuesta hormigas.
- **Rejugabilidad:** genes aleatorios, presas aleatorias, taras y, en la beta, eventos, genes iniciales y Mirmecodex.

## 6. Controles

Un único esquema táctil y de ratón para PC, tablet y móvil.

| Acción                     | Ratón                                                                           | Táctil                                    |
|----------------------------|---------------------------------------------------------------------------------|-------------------------------------------|
| Mover la cámara            | Arrastrar                                                                       | Arrastrar con un dedo                     |
| Zoom                       | Rueda                                                                           | Pellizcar                                 |
| Seleccionar hormiga / sala | Clic Izquierdo                                                                  | Tocar                                     |
| Construir                  | Botón *Construir* → modo Construcción: pincel de túneles o elegir sala y colocarla (§5.3) | Igual; un dedo usa la herramienta y dos dedos mueven la cámara |
| Asignar hormiga a sala     | *Asignar a sala* en el panel de la hormiga → elegir en la lista o tocar la sala | Igual                                     |
| Devorar                    | *Devorar* en el panel → tocar a la víctima (la reina devorando = herencia)      | Igual                                     |
| Comer presa                | *Comer presa* en el panel de la hormiga, o tocar la Cámara del Festín → menú del Festín | Igual                               |
| Expedición                 | *Apuntar a expedición* en el panel, pestaña *Expedición* (− / + y *Enviar*) o modo selección de la lista de hormigas | Igual            |
| Elegir casta de una larva  | Pestaña *Guardería* → Menor / Media / Mayor                                     | Igual                                     |
| Pausa y velocidad          | Botones del HUD                                                                 | Igual                                     |
| Ver una sala               | Clic Izquierdo en la sala → panel de sala                                       | Tocar la sala                             |
| Lista de hormigas          | Botón *Hormigas* del borde izquierdo                                            | Igual                                     |

## 7. Pantallas y flujo

| Pantalla                | Contenido                                                                                                              |
|-------------------------|------------------------------------------------------------------------------------------------------------------------|
| **Menú principal**      | Logo, *Nueva partida*, *Contactar*                                                                                     |
| **Créditos / Contacto** | Equipo y roles, enlaces a redes, assets de terceros, botón *Volver*                                                    |
| **Partida**             | Hormiguero + HUD                                                                                                       |
| **Game Over**           | Resultado (victoria o derrota), resumen de la partida, volver al menú. Aparece al perder, al ganar y al pulsar *Salir* |

## 8. Interfaz (HUD)

Distribución pensada para que nada se solape y el centro quede libre para el hormiguero:

- **Barra superior:** comida, hojas, hongo y biomasa, cada uno con su ritmo neto debajo ("+8/min" en verde, "−3/min" en rojo); día y estación con una barra de progreso del día; previsión de invierno; población; botón *Salir*.
- **Barra inferior:** botón *Construir* (entra en el modo Construcción, §5.3), pestañas *Guardería* y *Expedición* (solo una abierta a la vez, en el hueco de abajo a la izquierda; la Guardería muestra un **!** cuando una larva espera comida) y botones de pausa, ×1, ×2 y ×3 (el activo se resalta). Los botones de velocidad están abajo a la derecha, separados del borde para no quedar bajo el botón de pantalla completa de itch.io.
- **Panel de hormiga** (derecha, al tocarla): nombre, casta, estado, genes (iconos), barras de hambre y energía, *Asignar a sala*, *Devorar*, *Comer presa* y *Apuntar a expedición*.
- **Lista de hormigas** (cajón lateral a toda la altura, desde un botón del HUD): una fila por hormiga con nombre, casta, barras de hambre y energía y su oficio o estado; filtro por casta y orden; tocar una fila centra la cámara y abre su panel; en modo selección se marcan varias para enviarlas de expedición.
- **Panel de sala** (derecha, al tocar una sala fuera del modo Construcción; solo uno abierto junto con el de hormiga): nombre, descripción corta, obreras n/capacidad con su casta (tocar una la selecciona), producción ("+12 hongo/min · −12 hojas/min") y estado, y el botón *Añadir obrera libre*.
- **Modo Construcción:** plano azul, pincel de túneles, fantasma de sala y línea de estado (ver §5.3).
- **Superficie y entrada:** sobre el terreno hay una franja de superficie (hierba y cielo) visible en la cámara. La entrada del hormiguero es un pozo de 3 casillas de ancho excavado desde el inicio. La superficie es transitable hasta sus dos extremos, fuera de cámara: por ahí salen y vuelven las expediciones (caminan hasta la entrada, suben y desaparecen por un extremo; al volver aparecen por un extremo y caminan hasta la entrada cargando hoja o presa, y el botín se suma al pasar por ella). La entrada será también el punto de los eventos de invasores y lluvia *(beta)*. Toda la expedición sale y vuelve por el mismo extremo, elegido al azar; cada hormiga entrega su carga al llegar a la entrada y vuelve a su rutina. Si una no encuentra camino o tarda más de 60 s en llegar a la salida, se va igualmente.
- **Claridad: qué cuenta el juego y cómo se ve**
  - *Registro de sucesos (log):* guarda las últimas 50 entradas con el día de juego. Los 3 últimos avisos salen arriba a la izquierda, bajo la barra superior, durante 6 s; tocar un aviso abre el panel completo, con filtro por categoría (Info, Nacimiento, Muerte, Construcción, Mutación, Expedición, Aviso, Estación), color e icono por categoría. Tocar una entrada con foco centra la cámara en ella. Cuenta nacimientos, muertes (con su causa), salas terminadas, estaciones, expediciones, mutaciones y herencia (por ejemplo, "Lía ha devorado a Tom → Patas de corredora (+50 % velocidad, se cansa antes)"; las taras salen en rojo). También avisa de la economía: "Larva esperando casta" (con foco en la Guardería), "Comida baja" cuando la comida no cubre un día de consumo y "El invierno llega en 2 días: comida para X días".
  - *Iconos de estado:* sobre cada hormiga, una burbuja con las alertas siempre visibles (hambre crítica, agotada, sin camino) y la actividad solo al acercar la cámara o al seleccionarla. Sobre cada sala, como mucho un icono y solo si hay problema (prioridad: Sin acceso > Sin obreras > Sin hojas o hongo > Llena); tocarlo abre el panel de sala. Regla general: **los problemas, siempre; la información, solo al acercarse o seleccionar.** *(Iconos definitivos pendientes, F12.)*
  - *Estadísticas:* el ritmo neto de cada recurso está siempre a la vista; al tocar un recurso se ve el desglose de lo producido y lo consumido por minuto. Junto al día aparece la previsión ("Invierno en 3 días · comida para 4"), en verde si la comida cubre los días de invierno y en rojo si no. La población se muestra como "Obreras 7 (2 libres)" y, al tocarla, por castas.
- Tamaños de botón y texto pensados para móvil: texto de al menos 32 px y botones de al menos 88 px de alto en la resolución de referencia (1920×1080).

## 9. Arte

- **Estilo:** cartoon de contorno oscuro grueso, colores planos con un tono de sombra y uno de brillo, ojos grandes y expresivos.
- **Hormiga modular:** piezas separadas (cabeza, ojo, mandíbula, antenas, tórax, pecíolo, abdomen) montadas en Unity. Las **castas** reutilizan las mismas piezas a distinta escala. Las **patas** se dibujan por código (líneas animadas), lo que permite mutaciones de patas sin arte adicional.
- **Mutaciones:** cada gen visual sustituye la pieza de su ranura (beta). En la alfa se indican con color, icono y texto flotante.
- **Escenario:** corte lateral de terreno con estratos (tierra, arcilla, roca) y superficie con hierba. Salas como cámaras ovaladas reconocibles de un vistazo.
- **Paleta:** tierras cálidas para el entorno, terracota para las Glotonas, morado como color de la mutación.
- **UI:** formas redondeadas tipo piedra y hoja (beta). En la alfa, UI por defecto de Unity como placeholder.
- **Estado en la alfa:** ilustraciones definitivas de las salas (Real, Guardería, Despensa, Hongos y zona vacía); hormigas con formas básicas (placeholder); mutaciones indicadas con tinte, icono, destello y texto flotante.

## 10. Sonido

- **Música:** ambiente subterráneo tranquilo y algo juguetón (percusión suave, marimba, bajo), que se vuelve más tenso en invierno.
- **Efectos:** pasos de hormiga, excavar, construir completado, comer, "glup" al devorar, mutación (sonido mágico y gracioso), nacimiento, alerta, victoria y derrota.
- **UI:** clics y confirmaciones.
- **Fuentes:** librerías libres (Freesound, Kenney, OpenGameArt) con licencia compatible, generadores (jsfxr) y grabaciones propias. Todo acreditado en *Créditos*.
- **Alfa:** efectos básicos si da tiempo. Beta: música y efectos completos.

## 11. Tecnología

|                       |                                                                                                                                                                                                                                                                          |
|-----------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Motor                 | Unity 6000.0.57f1 LTS, URP 2D                                                                                                                                                                                                                                            |
| Plataforma            | WebGL (compresión Gzip + Decompression Fallback) en itch.io                                                                                                                                                                                                              |
| Control de versiones  | Git + GitHub (organización GutixStudio), Git LFS para assets, ramas `main` / `develop` / `feature/*`, PR revisadas                                                                                                                                                       |
| Arquitectura          | `EventBus` para comunicación desacoplada; `GameManager` (tiempo, estados); `ResourceManager`; datos en ScriptableObjects (genes, castas, salas)                                                                                                                          |
| IA                    | Tres capas: `AntSensor` (percepción), `AntBrain` (decisión, FSM jerárquica intercambiable), `AntActuator` (acción) + `AntNeeds` (hambre y energía)                                                                                                                       |
| Sistemas de juego     | Construcción: `NestGrid` (rejilla y estratos), `NestRenderer` (dibujo con marching squares), `DigPlanner` + `DigJob` (planos y trabajos de excavación), `RoomSpawner` (salas), `BuildModeController` (modo Construcción), `NestEntrance` (superficie y entrada); `ColonyStats` (estadísticas), `EventLog` (registro de sucesos), `ColonyProduction` (hojas → hongo → comida), `Queen` + `Nursery` (cría y castas), `AntGenome` / `QueenGenome` (genes por hormiga y por casta), `FeastSystem` (devorar), `ExpeditionSystem`, `SeasonRules` (invierno) |
| Navegación            | Rejilla de casillas con A\* (`GridNavigator`); en la alfa, grafo de puntos de paso entre salas                                                                                                                                                                          |
| Entrada               | Input System (ratón y táctil)                                                                                                                                                                                                                                            |
| Persistencia *(beta)* | Mirmecodex y genes desbloqueados en PlayerPrefs (almacenamiento del navegador)                                                                                                                                                                                           |
| Rendimiento           | 15–40 hormigas; la IA decide cada 0,3 s, no en cada frame                                                                                                                                                                                                                |

## 12. Monetización y modelo de negocio

**Modelo: demo gratuita + versión premium.**

- **Fase actual:** versión web gratuita en itch.io, con opción de donación voluntaria ("paga lo que quieras").
- **Futuro:** versión completa premium (PC, Steam / itch.io), precio orientativo 7–10 €: más genes y especies, campaña, combate táctico y modo infinito. La versión web queda como demo que alimenta la lista de deseados.
- **DLC** de contenido (nuevas especies jugables, biomas y amenazas).
- **Sin microtransacciones ni anuncios:** no encajan con un roguelite de partidas cortas y dañarían la experiencia.
- **Costes estimados (versión premium):** desarrollo del equipo, assets de audio, cuota de Steam (100 \$), marketing en redes.
- **Ingresos:** venta directa, DLC y donaciones.

## 13. Alcance por entregas

| Entrega     | Contenido                                                                                                                                                                                                                                                                                                                                             |
|-------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Alfa**    | Construcción en zonas con túneles fijos · 5 salas · hormigas autónomas (hambre, energía) · 3 castas · comida, hojas, hongo, biomasa · cría · Devorar y herencia (2 genes + 1 tara) · expedición simple · invierno como final · 4 pantallas                                                                                                            |
| **Beta**    | Panel de sala · arte de hormigas y UI propios · Cámara del Festín obligatoria para devorar · Túneles pintados · higiene y lealtad · Ophiocordyceps, invasores, inundación y derrumbe · Descanso, Basurero y Cuarentena · más genes con piezas visibles · eventos de expedición · Mirmecodex persistente · Dra. Coleóptera · arte y sonido definitivos |
| **Release** | Balance · más genes y recetas · localización ES/EN · pulido · campaña de marketing                                                                                                                                                                                                                                                                    |
| **Futuro**  | Mapa procedural · combate táctico estilo Mewgenics · fases evolutivas · evolución final o jefe · campaña · otras especies (DLC)                                                                                                                                                                                                                       |

### 13.1 Estado de la alfa (v0.1-alpha)

**Implementado y jugable:** construcción en 8 zonas con 3 estratos · 5 salas · túneles fijos con rutas A\* · hormigas autónomas con hambre y energía · 3 castas elegidas al criar · producción hojas → hongo → comida · reina que pone huevos · Devorar con mutación y taras (2 genes + 1 tara) · herencia por casta al devorar la reina · expedición con hojas y presas · invierno como final · victoria y derrota · menú, créditos, partida y Game Over · HUD completo.

**Recortado respecto al plan inicial (pasa a la beta):** túneles pintados por el jugador · eventos de texto en las expediciones · panel de sala · piezas de cuerpo por gen (en la alfa: tinte + icono) · higiene, lealtad y eventos · Mirmecodex y Dra. Coleóptera · sonido.

**Conocido:** hormigas y UI con placeholders · falta la ilustración de la Cámara del Festín · la biomasa se acumula pero aún no tiene uso · las obreras asignadas a la Despensa no producen (el hongo se convierte en comida solo; en la beta lo harán ellas) · al terminar una obra, las constructoras quedan sin sala hasta que se les asigna una · balance provisional (partida de unos 7 min a ×1).

### 13.2 Estado de la beta (v0.2-beta)

**Implementado y jugable:** hormiguero que se excava casilla a casilla (rejilla fina con 3 estratos y túneles de bordes suaves) · modo Construcción con plano azul, pincel de túneles, fantasma de sala y lupa táctil · salas de tamaño fijo con puertas laterales y coste que sube con cada sala · superficie y entrada · navegación por rejilla con A\* · obreras que excavan por su cuenta y obreras ociosas que esperan en la Sala Real o la entrada · reparto de las obreras por el suelo de las salas · Despensa con obreras · expediciones que salen y vuelven por la entrada, de tamaño libre y con botín que escala · registro de sucesos con avisos de colonia, genética y economía · estadísticas (ritmo de recursos, previsión de invierno y población) · burbuja de estado sobre cada hormiga e icono de estado sobre cada sala · panel de sala · lista de hormigas · menú del Festín con descubrimiento de especies · tamaños mínimos de botón y texto para móvil.

**Previsto para la beta y aún no implementado:** arte de hormigas y UI propios, y arte definitivo de túneles, salas, superficie e iconos · higiene y lealtad · Ophiocordyceps, invasores, inundación y derrumbe · Sala de Descanso, Basurero y Cuarentena · más genes con piezas visibles · eventos de expedición por zonas · Mirmecodex persistente · Dra. Coleóptera · sonido.

**Conocido:** piezas de arte provisionales en túneles, salas, superficie e iconos · la biomasa se acumula pero aún no tiene uso · balance provisional.

## 14. Marketing y redes sociales

- **Canales:** itch.io, X (Twitter), YouTube y portfolio en GitHub Pages; todos enlazados desde Linktree.
- **Identidad:** usuario `gutixstudio` en todas las redes.
- **Contenido:** gifs de mutaciones ("¿qué pasa si mi hormiga se come a…?"), datos curiosos de hormigas reales que inspiran cada gen, devlogs cortos de progreso.
- **Campaña de lanzamiento** (release): dos semanas de publicaciones diarias con cuenta atrás, tráiler y presentación de genes.

## 15. Equipo

**Gutix (GutixStudio):** Ayoze González, Naroa Jiménez, Iñigo García, Alonso Cancela, Ignacio Perea y Nicolás Roson.

| Área                                     | Contenido                                                                              |
|------------------------------------------|----------------------------------------------------------------------------------------|
| Producción, UI y web                     | Planificación, tablero, pantallas, HUD, itch.io, redes y portfolio                     |
| Arquitectura, construcción e integración | Contratos base, escena, cámara, navegación, construcción e integración de sistemas     |
| IA de hormigas                           | Percepción, cerebro (FSM jerárquica), necesidades, selección y panel de hormiga        |
| Genética                                 | Genes, genoma por hormiga y por casta, Festín, herencia y efectos de mutación          |
| Economía, cría y partida                 | Producción, reina, Guardería, castas, estaciones, invierno y expediciones              |
| Arte                                     | Ilustraciones de salas, fondo e iconos, e integración del arte en Unity                |
| Pruebas y control de calidad             | Pruebas de juego de la alfa (IA, mutaciones, economía y balance) y registro de errores |

El trabajo se reparte por tareas en el tablero y se revisa de forma cruzada (pull requests); el detalle individual se recoge en el post mortem de cada entrega.

Metodología Scrum con sprints de 1 semana, tablero en GitHub Projects y comunicación por Microsoft Teams.

## 16. Referencias

- Juegos: Fallout Shelter (Bethesda), Mewgenics (Edmund McMillen y Tyler Glaiel), Spore (Maxis), Los Sims (Maxis).
