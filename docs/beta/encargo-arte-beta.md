# Myrmutation · Arte pendiente para la beta (Naroa)

Todas las tareas F abiertas en GitHub, ordenadas por prioridad, con medidas y nombres exactos.
Lo que no llegue se queda con placeholders generados por código: nada bloquea a la programación.

## Reglas comunes
- **PNG con fondo transparente**, contorno oscuro grueso `#2a1a12`, colores planos + 1 tono de sombra + 1 de brillo, mismo estilo que las salas de la alfa.
- **Nombres exactos** (en minúsculas, con guion bajo). Si un nombre cambia, el juego no lo encuentra.
- **Medida base del juego:** 160 px = 1 unidad de Unity. El terreno es una rejilla de casillas de 40 px y un túnel mide **3 casillas = 120 px** de ancho.
- **Hormigas y piezas de túnel se dibujan al doble de tamaño** (se importan a 320 px por unidad) para que tengan detalle al hacer zoom. Las medidas de abajo ya están al tamaño en que hay que dibujarlas.
- Hormigas y objetos, **mirando a la derecha**.
- Carpetas: `Assets/_Project/Art/…` (indicada en cada apartado).

## Orden de prioridad
| # | Issue | Qué | Piezas | Por qué en este orden |
|---|---|---|---|---|
| 1 | #102 F9 | Túnel | 24 | Sin él, la construcción nueva (lo principal de la beta) no se ve |
| 2 | #38 F2 | Hormiga base y reina | 10 | Pendiente de la alfa; los iconos y las mutaciones se apoyan en ella |
| 3 | #111 F10 | Salas con puertas laterales | 5 | Las salas de la alfa no encajan con los túneles nuevos |
| 4 | #41 F8 | Hoja y presa | 2 | Las expediciones de la beta salen y vuelven cargando |
| 5 | #112 F11 | Superficie, entrada y estratos | 8 | Fondo del hormiguero nuevo |
| 6 | #113 F12 | Iconos de estado | 21 | Claridad: qué hace cada hormiga y qué falla |
| 7 | #40 F7 | Iconos de genes | 3 | Panel de hormiga y registro de sucesos |
| 8 | #114 F13 | Iconos de construcción y terrones | 9 | Modo Construcción |
| 9 | #43 F4 | UI propia (opcional) | 11 | Sustituye el pack de Kenney |
| 10 | #42 F5 | Logo | 2 | Menú, itch.io y redes |
| 11 | #44 F6 | Capturas y gif | 3–5 + 1 | Al final, con la beta ya montada |

---

## 1. Túnel · #102 F9 · `Art/Tiles/` · 24 PNG
Cada pieza es el cuadrado **entre 4 casillas**: sus esquinas son tierra o túnel y la pieza dibuja el borde según cuáles estén excavadas. El juego elige la pieza sola.

**15 piezas de 80 × 80 px.** Nombre: `ms_` + 4 dígitos con el orden **arriba-izquierda, arriba-derecha, abajo-derecha, abajo-izquierda** (1 = excavada):

| Tipo | Piezas | Qué dibujar |
|---|---|---|
| Una esquina excavada | `ms_1000`, `ms_0100`, `ms_0010`, `ms_0001` | Un cuarto de círculo de hueco en esa esquina |
| Dos del mismo lado | `ms_1100`, `ms_0011`, `ms_1001`, `ms_0110` | Medio cuadrado de hueco con el borde recto por el centro |
| Dos en diagonal | `ms_1010`, `ms_0101` | Pasillo diagonal que **une** las dos esquinas |
| Tres excavadas | `ms_0111`, `ms_1011`, `ms_1101`, `ms_1110` | Todo hueco salvo un cuarto de círculo de tierra en la esquina que falta |
| Las cuatro | `ms_1111`, `ms_1111_b`, `ms_1111_c` | Interior lleno sin bordes; las variantes con algún detalle (piedrita, raicilla) **sin tocar los bordes** |

Obligatorio para que encajen:
- El borde del hueco **cruza cada lado de la pieza por su punto medio (a 40 px) y en perpendicular**.
- Contorno de **16 px**, **por dentro del hueco**, color `#2a1a12`.
- Lo que no es hueco, **transparente**.
- El interior (tierra oscura excavada) es igual en todas.

Además:
- `dig_crack_1`, `dig_crack_2`, `dig_crack_3` · **240 × 240 px**: grieta poca, media y casi rota, con terrones, fondo transparente. Van encima del bloque que se está excavando.
- `edge_root_1`, `edge_root_2`, `edge_stone_1`, `edge_stone_2` · **80 × 80 px**: raicillas y piedras que el juego pone al azar en los bordes.

Guía: `docs/beta/guia/` (las 15 piezas en placeholder y un túnel montado).

## 2. Hormiga base y reina · #38 F2 · `Art/Ant/` · 10 PNG
Piezas sueltas que se montan en Unity; las **patas van por código**. Medida de referencia: la **hormiga Media montada mide unos 320 × 120 px sin patas** (en el juego, 1 × 0,375 unidades).

| Archivo | Qué es | Tamaño orientativo |
|---|---|---|
| `ant_head.png` | Cabeza redonda algo achatada, sin ojo, mandíbula ni antena; brillo arriba a la izquierda | ~90 px de diámetro |
| `ant_eye.png` | Blanco del ojo, óvalo vertical grande (≈⅓ de la cabeza) | ~30 × 38 px |
| `ant_pupil.png` | Pupila negra con un punto de brillo (aparte, para moverla y parpadear) | ~14 px |
| `ant_mandible.png` | Mandíbula curva tipo gancho, más oscura que el cuerpo | ~35 px |
| `ant_antenna.png` | Antena acodada con bolita en la punta; pivote en la base | ~70 px |
| `ant_thorax.png` | Tórax, óvalo horizontal pequeño; marcar los 3 puntos de las patas | ~80 × 50 px |
| `ant_petiole.png` | Pecíolo, cintura pequeña en tono sombra | ~25 px |
| `ant_abdomen.png` | Abdomen, el óvalo grande trasero, con 3 rayas de segmentos y brillo (≈40 % del largo) | ~130 × 95 px |
| `queen_abdomen.png` | Abdomen de reina, ~1,7× más grande y alargado, más oscuro y rojizo | ~220 × 140 px |
| `queen_crown.png` | Corona dorada de 3 puntas con una gema, va sobre la cabeza normal | ~50 × 35 px |

- Y una **imagen de referencia** con la hormiga montada y los puntos de unión marcados (cuello, cintura, 3 patas, base de la antena).
- **Límite de altura:** la casta Mayor se escala ×1,25, así que **con patas no puede pasar de 96 px de alto en el juego** (≈190 px a tamaño de dibujo), porque tiene que caber por el túnel.
- Las castas reutilizan las mismas piezas a distinta escala y tinte: dibújalas en el **color base terracota**, no en gris.

## 3. Salas con puertas laterales · #111 F10 · `Art/Rooms/` · 5 PNG
Mismos nombres y mismo contenido que en la alfa, con **medidas nuevas y puertas**.

| Archivo | Lienzo | Qué dibujar |
|---|---|---|
| `room_royal.png` | **1080 × 600 px** | Cama de hojitas, toque dorado o morado |
| `room_nursery.png` | **840 × 480 px** | Huevos y una larva, paredes más claras y cálidas |
| `room_storage.png` | **840 × 480 px** | Semillas, migas y gotas de néctar contra las paredes |
| `room_fungus.png` | **840 × 480 px** | Hongo blanco esponjoso sobre hojas troceadas |
| `room_feast.png` | **840 × 480 px** | «Mesa» de piedra, restos de caparazón, chispas de mutación; sin gore |

- **Dos puertas, abajo a la izquierda y abajo a la derecha:** cada una ocupa los **120 px inferiores** del lateral y llega hasta el borde del lienzo, como un túnel horizontal que entra. Contorno de 8 px por encima, igual que en los túneles.
- **Suelo plano a 8 px del borde inferior**, en todo el ancho: ahí caminan las hormigas.
- Interior **opaco**, exterior **transparente**. La forma de la cámara es libre (óvalo, cúpula…) respetando puertas y suelo.
- Guías: `guia_sala_21x12.png` y `guia_sala_real_27x15.png`.
- `room_empty.png` **ya no hace falta**.

## 4. Hoja y presa · #41 F8 · `Art/Items/` · 2 PNG
| Archivo | Tamaño | Qué es |
|---|---|---|
| `item_leaf.png` | ~100 × 70 px | Trozo de hoja verde en media luna con nervio. La hormiga la lleva sobre la cabeza al volver de expedición |
| `item_prey.png` | ~110 × 90 px | Insecto genérico redondito boca arriba con ojos en X; cartoon, sin gore |

## 5. Superficie, entrada y estratos · #112 F11 · `Art/Environment/` · 13 PNG
| Archivo | Tamaño | Qué es |
|---|---|---|
| `bg_soil.png` | 512 × 512 | Tierra marrón, **repetible sin cortes** en las 4 direcciones |
| `bg_clay.png` | 512 × 512 | Arcilla rojiza, repetible |
| `bg_rock.png` | 512 × 512 | Roca gris, repetible |
| `bg_edge_soil_clay.png` | 512 × 96 | Transición ondulada tierra → arcilla, repetible en horizontal |
| `bg_edge_clay_rock.png` | 512 × 96 | Transición arcilla → roca, repetible en horizontal |
| `surface_grass.png` | 512 × 120 | Línea de hierba sobre la tierra, repetible en horizontal; las hormigas caminan por encima |
| `surface_sky.png` | 1024 × 480 | Cielo con tallos o plantas lejanas, repetible en horizontal |
| `nest_mound.png` | 680 × 200 | Montículo de tierra con el cráter en la cima (17 × 5 casillas a 40 px). Laderas de 45°; **cráter de 120 px de ancho, centrado arriba**, por donde sale el pozo. Fondo transparente, base recta abajo |
| `tree_1.png` … `tree_3.png` | libre | Árboles de la superficie, fondo transparente, base del tronco abajo y centrada (se estiran al ancho de su sitio manteniendo la proporción) |
| `bush_1.png`, `bush_2.png` | libre | Matorrales, igual que los árboles |
| `grass_tuft_1.png` | libre | Mata de hierba suelta, igual que los árboles |

Los estratos son el fondo y tienen que ser **más claros que el interior de los túneles**, para que se lea bien lo excavado.

## 6. Iconos de estado · #113 F12 · `Art/Icons/Status/` · 21 PNG
**Tienen que leerse a 32 px**: formas muy simples.

| Archivo | Tamaño | Qué es |
|---|---|---|
| `bubble.png` | 96 × 96 | Bocadillo blanco con pico abajo, donde va el icono |
| `st_hungry.png` | 64 × 64 | Hambre: cuenco vacío |
| `st_tired.png` | 64 × 64 | Cansada: Zzz |
| `st_exhausted.png` | 64 × 64 | Agotada: Zzz en rojo |
| `st_digging.png` | 64 × 64 | Excavando: mandíbula con un terrón |
| `st_working.png` | 64 × 64 | Trabajando: engranaje u hoja |
| `st_eating.png` | 64 × 64 | Comiendo: miga mordida |
| `st_carrying.png` | 64 × 64 | Cargando: hoja a la espalda |
| `st_lost.png` | 64 × 64 | Sin camino: interrogación |
| `room_no_access.png` | 64 × 64 | Sala sin acceso: túnel tachado |
| `room_no_workers.png` | 64 × 64 | Sin obreras: silueta de hormiga con un + |
| `room_no_input.png` | 64 × 64 | Sin hojas: hoja tachada |
| `room_full.png` | 64 × 64 | Llena: caja rebosando |
| `log_birth.png` | 48 × 48 | Nacimiento: huevo |
| `log_death.png` | 48 × 48 | Muerte: hormiga con ojos en X (sin gore) |
| `log_build.png` | 48 × 48 | Construcción: pala |
| `log_mutation.png` | 48 × 48 | Mutación: hélice morada |
| `log_expedition.png` | 48 × 48 | Expedición: hoja |
| `log_warning.png` | 48 × 48 | Aviso: exclamación |
| `log_season.png` | 48 × 48 | Estación: copo de nieve |
| `log_info.png` | 48 × 48 | Información: «i» |

## 7. Iconos de genes · #40 F7 · `Art/Icons/` · 3 PNG
**128 × 128 px**, con fondo circular para que se lean pequeños sobre la hormiga.

| Archivo | Qué es |
|---|---|
| `gene_runner.png` | Patas de corredora: pata larga con líneas de velocidad, **fondo verde** |
| `gene_cutter.png` | Mandíbula cortadora: mandíbula dentada sujetando un trozo de hoja, **fondo verde** |
| `flaw_glutton.png` | Glotona (tara): barriga hinchada o boca abierta con baba, **fondo rojo o morado** |

## 8. Construcción y terrones · #114 F13 · 9 PNG
| Archivo | Tamaño | Qué es |
|---|---|---|
| `Art/UI/Build/build_mode.png` | 96 × 96 | Botón Construir: pala o plano enrollado |
| `Art/UI/Build/build_tunnel.png` | 96 × 96 | Herramienta Túnel: pincel con una línea de túnel |
| `Art/UI/Build/build_rooms.png` | 96 × 96 | Herramienta Salas: óvalo de sala con un + |
| `Art/UI/Build/build_erase.png` | 96 × 96 | Borrar: goma |
| `Art/UI/Build/build_confirm.png` | 96 × 96 | ✓ verde |
| `Art/UI/Build/build_cancel.png` | 96 × 96 | ✕ roja |
| `Art/FX/dirt_chunk_1.png`, `_2`, `_3` | 32 × 32 | Terrones que saltan al excavar: tres formas, mismo color tierra |

## 9. UI propia (opcional) · #43 F4 · `Art/UI/` · 11 PNG
Solo si da tiempo; si no, se queda el pack de Kenney recoloreado. Mismos nombres para no tocar código.

| Archivo | Tamaño | Qué es |
|---|---|---|
| `ui_button.png` | ~200 × 70 | Botón tipo piedra lisa, color claro neutro (el juego lo tiñe), **preparado para 9-slice** (esquinas iguales, centro liso) |
| `ui_button_pressed.png` | ~200 × 70 | El mismo, hundido y algo más oscuro |
| `ui_panel.png` | ~400 × 300 | Tablilla de corteza o papel envejecido, 9-slice |
| `res_food.png` | 64 × 64 | Comida: gota de néctar ámbar o miga |
| `res_leaves.png` | 64 × 64 | Hojas: hoja verde entera |
| `res_fungus.png` | 64 × 64 | Hongo: bolita blanca esponjosa |
| `res_biomass.png` | 64 × 64 | Biomasa: gota o hélice de ADN morada |
| `ui_pause.png` | 64 × 64 | Pausa: dos barras |
| `ui_speed1.png` | 64 × 64 | ×1: una flecha |
| `ui_speed2.png` | 64 × 64 | ×2: dos flechas |
| `ui_speed3.png` | 64 × 64 | ×3: tres flechas |

## 10. Logo · #42 F5 · `Art/Branding/` · 2 PNG
- `logo_myrmutation.png`: texto **MYRMUTATION** en tipografía gruesa y redondeada, con algún detalle de hormiga (antenas sobre una letra, una mandíbula en la M…) y un toque morado. Horizontal, fondo transparente, unos 1600 × 500 px.
- `icon_myrmutation.png`: **512 × 512 px**, recorte o versión cuadrada del logo para itch.io y redes.

## 11. Capturas y gif · #44 F6 · `docs/media/`
- 3–5 capturas del juego (1920 × 1080) y 1 gif corto de 5–10 s (una mutación o un túnel excavándose).
- **Mejor al final del sprint Beta 2**, cuando la construcción nueva y el arte ya estén dentro; ahora saldría la alfa.
