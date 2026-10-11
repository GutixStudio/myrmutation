# Myrmutation

**Gestión de colonia + roguelite en 2D (vista lateral).** Dirige un hormiguero de *Glotonas*, una especie que evoluciona **comiéndose** a otras criaturas… y a sí misma. Excava salas, cría obreras, cultiva hongos y decide a quién devorar para que tu colonia herede los mejores rasgos antes de que llegue el invierno.

Proyecto de **Gutix (GutixStudio)** para *Juegos para Web y Redes Sociales* · Grado en Diseño y Desarrollo de Videojuegos · URJC · 2025-26.

| | |
|---|---|
| Versión actual | **v0.1-alpha** (entrega Alfa) |
| Motor | Unity **6000.0.57f1** LTS · URP 2D · Input System |
| Plataforma | WebGL (Chrome y Firefox · PC, tablet y móvil) |
| Diseño | [GDD](docs/GDD.md) · [IA de las hormigas](docs/IA_Hormigas.md) |
| Enlaces | [docs/enlaces.txt](docs/enlaces.txt) |

## Cómo jugar (alfa)
- **Cámara:** arrastrar para moverla · rueda o pellizco para el zoom.
- **Construir:** tocar una zona vacía → elegir sala (hacen falta obreras libres).
- **Hormigas:** tocar una para ver su panel → *Asignar a sala*, *Devorar*, *Comer presa*, *Apuntar a expedición*.
- **Guardería:** pestaña inferior → elegir la casta de cada larva (Menor, Media o Mayor).
- **Expedición:** pestaña inferior → elegir 1–3 hormigas → *Enviar*.
- **Tiempo:** pausa, ×1, ×2 y ×3. Se gana al sobrevivir al invierno; se pierde si muere la reina.

## Abrir el proyecto
1. Instalar **Unity 6000.0.57f1** con *Web Build Support* (misma versión exacta; no actualizar el proyecto).
2. Clonar con Git LFS:
   ```bash
   git clone https://github.com/GutixStudio/myrmutation.git
   cd myrmutation
   git lfs install
   ```
3. Unity Hub → *Add project from disk* → carpeta del repositorio.
4. Abrir `Assets/_Project/Scenes/MainMenu.unity` y pulsar Play.

## Estructura
```
Assets/_Project/
├── Core/          Contratos base: EventBus, GameManager, ResourceManager, Ant, Room y datos (ScriptableObjects)
├── Scripts/
│   ├── Ants/      IA de las hormigas: AntActuator, AntSensor, AntBrain, AntNeeds, selección y panel
│   ├── Building/  Cámara, zonas, navegación (waypoints + A*) y construcción
│   ├── Colony/    Reina y Guardería
│   ├── Economy/   Producción y reglas de las estaciones
│   ├── Expedition/Expediciones
│   ├── Genetics/  Genoma, Festín y efectos de mutación
│   └── UI/        Pantallas, HUD y paneles
├── Data/          Castas, genes y salas (ScriptableObjects)
├── Prefabs/       Hormiga, zonas y paneles de UI
├── Art/           Ilustraciones y placeholders
└── Scenes/        MainMenu · Game · Credits · GameOver (+ Test/ con las escenas de prueba de cada sistema)
```

## Flujo de trabajo
- Ramas: `main` (entregas, con tag por versión) · `develop` (integración) · `feature/<ID>-<nombre>` (una por tarea).
- Toda la integración en `develop` va por **pull request** revisada.
- Tablero, issues y sprints en GitHub Projects. Comunicación del equipo por Discord.

## Equipo
Ayoze González · Naroa Jiménez · Alonso Cancela · Ignacio Perea · Nicolás Rosón · Iñigo García
