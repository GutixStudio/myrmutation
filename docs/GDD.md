# MYRMUTATION · Game Design Document

**Estudio:** Gutix (GutixStudio)
**Género:** gestión de colonia + roguelite · 2D vista lateral
**Plataforma:** Web (WebGL) · itch.io · Chrome y Firefox · PC, tablet y móvil
**Motor:** Unity 6000.0.57f1 LTS
**Asignatura:** Juegos para Web y Redes Sociales · Grado en Diseño y Desarrollo de Videojuegos · URJC · Curso 2025-26

## Control de versiones
| Versión | Fecha | Autor | Cambios |
|---|---|---|---|
| 0.1 | 06/10/2026 | Gutix | Primer borrador completo del GDD (entrega Alfa) |

---

## Índice
1. [Concepto](#1-concepto)
2. [Ficha técnica](#2-ficha-técnica)
3. [Público objetivo](#3-público-objetivo)
4. [Narrativa](#4-narrativa)
5. [Mecánicas](#5-mecánicas)
6. [Controles](#6-controles)
7. [Pantallas y flujo](#7-pantallas-y-flujo)
8. [Interfaz (HUD)](#8-interfaz-hud)
9. [Arte](#9-arte)
10. [Sonido](#10-sonido)
11. [Tecnología](#11-tecnología)
12. [Monetización y modelo de negocio](#12-monetización-y-modelo-de-negocio)
13. [Alcance por entregas](#13-alcance-por-entregas)
14. [Marketing y redes sociales](#14-marketing-y-redes-sociales)
15. [Equipo](#15-equipo)
16. [Referencias](#16-referencias)

---

## 1. Concepto

### 1.1 Pitch
> Gestiona un hormiguero de **Glotonas**, una especie de hormiga que evoluciona **comiéndose** a otras criaturas… y a sí misma. Excava salas, cría a tus obreras, cultiva hongos y decide a quién devorar para que tu colonia herede los mejores rasgos antes de que llegue el invierno.

### 1.2 Introducción
La receta del éxito de las hormigas es la colaboración: solas son inútiles, pero juntas consiguen logros increíbles. Construyen su hábitat, crían ganado, practican la agricultura y mantienen relaciones simbióticas. Y también hacen la guerra.

Algunas especies son tan letales que las demás han tenido que especializarse durante millones de años para sobrevivir: cabezas que taponan la entrada del nido, huidas masivas con las larvas a cuestas, mandíbulas que cortan hojas…

Las **Glotonas** encontraron un atajo: en lugar de esperar a que la evolución las especialice, **se comen la especialización ajena**.

### 1.3 Pilares de diseño
1. **Cada hormiga importa.** Pocas hormigas, cada una con nombre, casta, necesidades y genes.
2. **Mutar es una decisión, no un premio.** Devorar cuesta una hormiga, puede salir mal (taras) y lo que hereda la reina lo heredan todas.
3. **La colonia vive sola.** El jugador decide el qué; las hormigas deciden el cuándo y el cómo.
4. **Partidas cortas y distintas.** Roguelite: cada colonia evoluciona de forma diferente.

### 1.4 Referencias
| Juego | Qué tomamos |
|---|---|
| **Fallout Shelter** | Gestión de salas en corte lateral, asignación de habitantes |
| **Mewgenics** | Herencia de rasgos, taras, rejugabilidad, humor negro cartoon |
| **Spore** | Cambios visibles en el cuerpo por partes; fases evolutivas (futuro) |
| **Los Sims** | Personajes autónomos con necesidades y rutinas |

### 1.5 Originalidad
La gestión de colonias es un género conocido; lo que diferencia a Myrmutation es el **canibalismo mutagénico en dos niveles** (individuo → linaje) y que las mutaciones cambian **el cuerpo y el comportamiento** de las hormigas, no solo sus números.

---

## 2. Ficha técnica
| | |
|---|---|
| Título | Myrmutation |
| Género | Gestión / simulación de colonia + roguelite |
| Vista | 2D lateral (sección de hormiguero) |
| Jugadores | 1 |
| Duración de partida | 10–15 min (alfa) · 30–45 min (release) |
| Plataforma | Navegador (WebGL) en PC, tablet y móvil |
| Distribución | itch.io |
| Idiomas | Español (alfa) · Español e inglés (release) |
| Clasificación orientativa | PEGI 7 (violencia cartoon entre insectos, sin sangre) |

---

## 3. Público objetivo
- **Principal:** jugadores de 16–35 años de juegos de gestión y roguelites (Fallout Shelter, Mewgenics, RimWorld, Oxygen Not Included) que buscan partidas cortas.
- **Secundario:** jugadores casuales de navegador y móvil atraídos por el estilo cartoon y el tema curioso (hormigas reales).
- **Perfil:** les gusta experimentar con combinaciones, ver crecer un sistema y contar sus partidas ("mi colonia de mieleras gigantes…").

---

## 4. Narrativa

### 4.1 Premisa
Las Glotonas parecen hormigas normales: misma jerarquía (una reina y sus obreras) y misma forma de construir el nido. Pero practican el canibalismo. La **Dra. Coleóptera**, una escarabajo científica algo excéntrica, descubre que estas hormigas son capaces de **absorber el material genético de lo que comen** y desarrollar rasgos de otras especies. Decide estudiarlas… guiándote a ti, la mente de la colonia.

### 4.2 Personajes
| Personaje | Rol |
|---|---|
| **La reina** | Corazón de la colonia. Si muere, termina la partida. Hereda los genes de las mutantes que devora y los transmite a sus crías. |
| **Las obreras Glotonas** | Cada una con nombre, casta y genes. Trabajan, comen, descansan… y a veces son devoradas. |
| **Dra. Coleóptera** | Narradora y tutorial. Comenta con humor lo que hace el jugador y escribe el **Mirmecodex**, su cuaderno de investigación. *(beta)* |

### 4.3 El Mirmecodex *(beta)*
Cuaderno de la doctora. Cada gen descubierto se registra con una ilustración, una descripción y un comentario suyo. Se conserva entre partidas y permite empezar nuevas colonias con 1–2 genes ya descubiertos.

### 4.4 Tono
Cartoon desenfadado. El canibalismo y las mutaciones se tratan con humor, sin sangre ni gore: las hormigas tienen ojos grandes y expresivos, y las víctimas desaparecen con un efecto de "glup".

### 4.5 Arco narrativo *(pendiente para la beta)*
La investigación de la doctora avanza partida a partida a través del Mirmecodex. Se plantea un misterio de fondo sobre el origen de la capacidad de las Glotonas, que conecta con el final evolutivo previsto para versiones futuras.

---

## 5. Mecánicas

### 5.1 Bucle de juego
1. **Construir:** excavar salas en las zonas del hormiguero.
2. **Criar:** la reina pone huevos; el jugador decide cómo alimentar a cada larva y eso fija su casta.
3. **Asignar:** colocar a cada obrera en una sala; ella decide cuándo trabajar, comer o descansar.
4. **Producir:** hojas → hongo → comida.
5. **Explorar:** enviar expediciones que traen hojas y presas.
6. **Mutar:** devorar para ganar genes; la reina hereda y transmite.
7. **Sobrevivir:** llegar al final del invierno.

### 5.2 Estructura de partida (roguelite)
- **Victoria:** sobrevivir hasta el final del invierno (4 estaciones).
- **Derrota (Game Over):** muere la reina (hambre, ataque o, en la beta, rebelión) o el jugador pulsa **Salir**.
- **Tiempo:** real con pausa y velocidades ×1, ×2 y ×3. Cada estación dura varios días de juego.
- **Meta-progresión** *(beta)*: el Mirmecodex conserva los genes descubiertos; se pueden elegir 1–2 genes iniciales para la reina.

### 5.3 Construcción
- El mapa tiene **zonas prediseñadas** donde se pueden construir salas. El jugador **elige qué sala va en cada zona**.
- Construir requiere un **mínimo de obreras** asignadas; la **velocidad** depende de la suma de su fuerza. Las mutaciones de fuerza se notan al construir.
- **Estratos:** tierra → arcilla → roca. Abajo hace más calor en invierno, pero hay riesgo de inundación *(beta)*; arriba se excava más rápido, pero los invasores llegan antes *(beta)*.
- **Túneles:** en la alfa son fijos. En la beta el jugador los **pinta** sobre una rejilla para conectar las salas.

| Sala | Función | Entrega |
|---|---|---|
| **Sala Real** | Vive la reina; pone huevos y devora mutantes para heredar sus genes | Alfa |
| **Guardería** | Huevo → larva → pupa → adulta; aquí se elige la casta | Alfa |
| **Despensa** | Almacena comida; las hormigas vienen a comer | Alfa |
| **Sala de hongos** | Convierte hojas en hongo, y el hongo en comida | Alfa |
| **Cámara del Festín** | Donde se devora para mutar | Alfa |
| **Sala de Descanso** | Recuperar energía y acicalarse | Beta |
| **Basurero** | Restos y cadáveres; reduce infecciones | Beta |
| **Cuarentena** | Aísla hormigas infectadas | Beta |
| **Solárium** | Acelera la cría, pero la expone | Release |

### 5.4 Hormigas: necesidades y comportamiento
Cada hormiga es un **agente autónomo**: el jugador asigna su puesto y ella decide según sus necesidades.

| Necesidad | Efecto | Entrega |
|---|---|---|
| **Hambre** | Va a la Despensa a comer; si llega al máximo, muere | Alfa |
| **Energía** | Va a descansar y luego vuelve al trabajo | Alfa |
| **Higiene** | Si baja, aumenta el riesgo de infección | Beta |
| **Lealtad / estrés** | Baja con el canibalismo y el hambre; a 0 hay rebelión | Beta |

**IA:** arquitectura en tres capas (percepción, decisión y actuación). La decisión es una **máquina de estados jerárquica** con prioridades (Emergencia > Necesidad > Rutina). **Los genes modifican el comportamiento**: por ejemplo, una hormiga *Glotona* come el doble. Este sistema se desarrolla también para la asignatura de Comportamiento de Personajes.

### 5.5 Castas
Como en las hormigas reales, la casta **no depende de los genes, sino de la alimentación de la larva**: cuanta más comida, mayor casta.

| Casta | Coste de cría | Destaca en | Flojea en |
|---|---|---|---|
| **Menor** | Bajo | Cría, cultivo de hongos | Construcción |
| **Media** | Medio | Recolección, expediciones | — |
| **Mayor** (soldado) | Alto | Construcción, defensa | Cría, cultivo |

La asignación es libre, pero cada casta rinde distinto en cada sala (**afinidad**). La interfaz muestra el rendimiento al asignar.

### 5.6 Recursos
| Recurso | Cómo se obtiene | Para qué |
|---|---|---|
| **Comida** | Del hongo y de las expediciones | Alimentar a la reina, las larvas y las obreras |
| **Hojas** | Expediciones | Cultivar hongo |
| **Hongo** | Sala de hongos (a partir de hojas) | Se convierte en comida |
| **Biomasa genética** | Devorar hormigas o presas | Mutaciones (más usos en la beta). **Nunca da comida.** |

### 5.7 Mutación: canibalismo en dos niveles
**Nivel 1 · Individuo:** una obrera devora a otra hormiga o a una presa y **muta ella sola**. Es el experimento.

| Víctima | Resultado |
|---|---|
| Hormiga propia con genes | Copia uno de sus genes (80 %) · tara (20 %) |
| Hormiga propia sin genes | Nada (60 %) · tara (40 %) |
| Presa de expedición | Su gen (70 %) · tara (20 %) · nada (10 %) |

**Nivel 2 · Linaje:** la **reina devora a una mutante** y sus genes (y taras) pasan al **genoma de la casta** de esa mutante. **Todas las crías futuras** de esa casta nacen con ellos. Con el tiempo, las castas se diferencian visualmente: es un polimorfismo construido por el jugador.

**Diseño de genes:**
1. Cada gen **cambia una regla**, no solo un porcentaje.
2. Se ve **en el cuerpo** (hormiga modular de 5 ranuras: cabeza, antenas, tórax, abdomen, patas).
3. Se ve **en el comportamiento**.
4. Mutar es un **evento**: efecto visual, mensaje y entrada en el Mirmecodex.
5. Las **taras** también se ven.

**Genes de la alfa:**
| Gen | Origen | Ranura | Efecto |
|---|---|---|---|
| **Patas de corredora** | Hormiga del desierto (*Cataglyphis*) | Patas | Velocidad +50 %, se cansa un 25 % antes |
| **Mandíbula cortadora** | Cortadora de hojas (*Atta*) | Cabeza | Fuerza +30 %, carga +1 |
| **Glotona** (tara) | Mutación fallida | — | Hambre ×2 y come doble ración |

**Genes previstos (beta y release):** Abdomen-despensa (*Myrmecocystus*), Cabeza-tapón (*Pheidole obtusospinosa*), Instinto de evacuación (*Pheidole desertorum*), Glándula ácida (*Formica*), Ojos compuestos (*Harpegnathos*), Alas, Mandíbula-trampa (*Odontomachus*), Aguijón (*Solenopsis*), Coraza (*Cephalotes*), Seda tejedora (*Oecophylla*)… y **recetas** que combinan dos genes en uno nuevo.

### 5.8 Expediciones
- **Alfa:** se envían 1–3 hormigas; tras un tiempo vuelven con hojas o con una presa (fuente de genes). No hay expediciones en invierno.
- **Beta:** se elige una zona (pradera, charca, nido rival…) y durante la expedición aparecen 1–3 **eventos de texto** con decisiones; algunas opciones solo aparecen si el grupo tiene ciertos genes.

### 5.9 Estaciones y eventos
| Evento | Descripción | Entrega |
|---|---|---|
| **Invierno** | Marca el final. Más consumo, sin expediciones | Alfa |
| **Ophiocordyceps** | Hongo parásito que llega con las expediciones, controla a la hormiga y se contagia. Se contrarresta con cuarentena, higiene y basurero | Beta |
| **Invasores** | Arañas u hormigas rivales entran en el nido; las hormigas interrumpen su rutina para defenderse | Beta |
| **Inundación** | Las lluvias o tocar el nivel freático inundan las salas profundas | Beta |
| **Derrumbe** | Excavaciones extensas pueden colapsar | Beta |

### 5.10 Dificultad y progresión
- **Aprendizaje:** la partida empieza con la reina y pocas obreras; las salas se desbloquean de forma natural (primero Despensa y Guardería, luego Hongos y Festín). En la beta, la Dra. Coleóptera guía los primeros pasos.
- **Tensión:** el invierno obliga a acumular comida; devorar da poder, pero cuesta hormigas.
- **Rejugabilidad:** genes aleatorios, presas aleatorias, taras y, en la beta, eventos, genes iniciales y Mirmecodex.

---

## 6. Controles
Un único esquema **táctil y de ratón** para PC, tablet y móvil.

| Acción | Ratón | Táctil |
|---|---|---|
| Mover la cámara | Arrastrar | Arrastrar con un dedo |
| Zoom | Rueda | Pellizcar |
| Seleccionar hormiga / sala | Clic Izquierdo | Tocar |
| Construir | Clic Izq en zona vacía → elegir sala | Tocar zona vacía → elegir sala |
| Asignar hormiga a sala | Botón en el panel de la hormiga | Igual |
| Devorar | Botón *Devorar* en el panel → elegir víctima | Igual |
| Pausa y velocidad | Botones del HUD | Igual |
| *(Beta)* Pintar túneles | En modo excavación, arrastrar | Un dedo pinta, dos dedos mueven la cámara |

---

## 7. Pantallas y flujo
```
Menú principal ──Nueva partida──► Partida ──(gana/pierde/Salir)──► Game Over ──► Menú principal
      │
      └──Contactar──► Créditos / Contacto ──Volver──► Menú principal
```
| Pantalla | Contenido |
|---|---|
| **Menú principal** | Logo, *Nueva partida*, *Contactar* |
| **Créditos / Contacto** | Equipo y roles, enlaces a redes, assets de terceros, botón *Volver* |
| **Partida** | Hormiguero + HUD |
| **Game Over** | Resultado (victoria o derrota), resumen de la partida, volver al menú. Aparece al perder, al ganar y al pulsar *Salir* |

---

## 8. Interfaz (HUD)
- **Arriba:** recursos (comida, hojas, hongo, biomasa), día y estación.
- **Esquina:** pausa, ×1, ×2, ×3 y *Salir*.
- **Panel de hormiga** (al tocarla): nombre, casta, genes (iconos), barras de hambre y energía, asignar a sala, *Devorar*.
- **Panel de sala** (al tocarla): tipo, trabajadoras, producción y progreso de construcción.
- Tamaños de botón y texto pensados para móvil.

---

## 9. Arte
- **Estilo:** cartoon de contorno oscuro grueso, colores planos con un tono de sombra y uno de brillo, ojos grandes y expresivos.
- **Hormiga modular:** piezas separadas (cabeza, ojo, mandíbula, antenas, tórax, pecíolo, abdomen) montadas en Unity. Las **castas** reutilizan las mismas piezas a distinta escala. Las **patas** se dibujan por código (líneas animadas), lo que permite mutaciones de patas sin arte adicional.
- **Mutaciones:** cada gen visual sustituye la pieza de su ranura (beta). En la alfa se indican con color, icono y texto flotante.
- **Escenario:** corte lateral de terreno con estratos (tierra, arcilla, roca) y superficie con hierba. Salas como cámaras ovaladas reconocibles de un vistazo.
- **Paleta:** tierras cálidas para el entorno, terracota para las Glotonas, morado como color de la mutación.
- **UI:** formas redondeadas tipo piedra y hoja. En la alfa se usa un pack CC0 (Kenney) adaptado.

---

## 10. Sonido
- **Música:** ambiente subterráneo tranquilo y algo juguetón (percusión suave, marimba, bajo), que se vuelve más tenso en invierno.
- **Efectos:** pasos de hormiga, excavar, construir completado, comer, "glup" al devorar, mutación (sonido mágico y gracioso), nacimiento, alerta, victoria y derrota.
- **UI:** clics y confirmaciones.
- **Fuentes:** librerías libres (Freesound, Kenney, OpenGameArt) con licencia compatible, generadores (jsfxr) y grabaciones propias. Todo acreditado en *Créditos*.
- **Alfa:** efectos básicos si da tiempo. Beta: música y efectos completos.

---

## 11. Tecnología
| | |
|---|---|
| Motor | Unity 6000.0.57f1 LTS, URP 2D |
| Plataforma | WebGL (compresión Gzip + Decompression Fallback) en itch.io |
| Control de versiones | Git + GitHub (organización GutixStudio), Git LFS para assets, ramas `main` / `develop` / `feature/*`, PR revisadas |
| Arquitectura | `EventBus` para comunicación desacoplada; `GameManager` (tiempo, estados); `ResourceManager`; datos en **ScriptableObjects** (genes, castas, salas) |
| IA | Tres capas: `AntSensor` (percepción), `AntBrain` (decisión, FSM jerárquica intercambiable), `AntActuator` (acción) |
| Navegación | Grafo de puntos de paso entre salas (alfa) → rejilla con A* (beta) |
| Entrada | Input System (ratón y táctil) |
| Persistencia *(beta)* | Mirmecodex y genes desbloqueados en PlayerPrefs (almacenamiento del navegador) |
| Rendimiento | 15–40 hormigas; la IA decide cada 0,2–0,5 s, no en cada frame |

---

## 12. Monetización y modelo de negocio
**Modelo: demo gratuita + versión premium.**
- **Ahora (asignatura):** versión web **gratuita** en itch.io, con opción de **donación** voluntaria ("paga lo que quieras").
- **Futuro:** versión completa **premium** (PC, Steam / itch.io), precio orientativo **7–10 €**: más genes y especies, campaña, combate táctico y modo infinito. La versión web queda como **demo** que alimenta la lista de deseados.
- **DLC** de contenido (nuevas especies jugables, biomas y amenazas).
- **Sin microtransacciones ni anuncios:** no encajan con un roguelite de partidas cortas y dañarían la experiencia.
- **Costes estimados (versión premium):** desarrollo del equipo, assets de audio, cuota de Steam (100 $), marketing en redes.
- **Ingresos:** venta directa, DLC y donaciones.

---

## 13. Alcance por entregas
| Entrega | Contenido |
|---|---|
| **Alfa** | Construcción en zonas con túneles fijos · 5 salas · hormigas autónomas (hambre, energía) · 3 castas · comida, hojas, hongo, biomasa · cría · Devorar y herencia (2 genes + 1 tara) · expedición simple · invierno como final · 4 pantallas |
| **Beta** | Túneles pintados · higiene y lealtad · Ophiocordyceps, invasores, inundación y derrumbe · Descanso, Basurero y Cuarentena · más genes con piezas visibles · eventos de expedición · Mirmecodex persistente · Dra. Coleóptera · arte y sonido definitivos |
| **Release** | Balance · más genes y recetas · localización ES/EN · pulido · campaña de marketing |
| **Futuro** | Mapa procedural · combate táctico estilo Mewgenics · fases evolutivas · evolución final o jefe · campaña · otras especies (DLC) |

---

## 14. Marketing y redes sociales
- **Canales:** itch.io, X (Twitter), YouTube y portfolio en GitHub Pages; todos enlazados desde Linktree.
- **Identidad:** usuario `gutixstudio` en todas las redes.
- **Contenido:** gifs de mutaciones ("¿qué pasa si mi hormiga se come a…?"), datos curiosos de hormigas reales que inspiran cada gen, devlogs cortos de progreso.
- **Campaña de lanzamiento** (release): dos semanas de publicaciones diarias con cuenta atrás, tráiler y presentación de genes.

---

## 15. Equipo
| Rol | Responsable |
|---|---|
| Producción, UI y web | *(nombre)* |
| Arquitectura y construcción | *(nombre)* |
| IA de hormigas | *(nombre)* |
| Genética | *(nombre)* |
| Economía y balance | *(nombre)* |
| Arte | *(nombre)* |

Metodología **Scrum** con sprints de 1 semana, tablero en GitHub Projects y comunicación por Microsoft Teams.

---

## 16. Referencias
- Hölldobler, B. y Wilson, E. O. *The Ants*. Harvard University Press, 1990.
- Morales Urrutia, G. A. et al. "Procesos de desarrollo para videojuegos". *CULCyT*, 1(36):25–39, 2010.
- Juegos: Fallout Shelter (Bethesda), Mewgenics (Edmund McMillen y Tyler Glaiel), Spore (Maxis), Los Sims (Maxis).
- Assets de terceros: Kenney (CC0). *(Completar con los usados.)*
- Uso de IA generativa: Claude (Anthropic) como apoyo en diseño, planificación y documentación, con revisión humana del equipo.
