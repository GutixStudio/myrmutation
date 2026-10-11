using Myrmutation.Core.Data;

namespace Myrmutation.Core
{
    // Eventos del juego. Se pueden AÑADIR eventos nuevos al final (avisando en Teams).
    // No cambiar los existentes sin acuerdo del equipo.

    public struct GameStateChanged { public GameState State; public GameStateChanged(GameState s) { State = s; } }
    public struct DayPassed { public int Day; public DayPassed(int d) { Day = d; } }
    public struct SeasonChanged { public Season Season; public int SeasonIndex; public SeasonChanged(Season s, int i) { Season = s; SeasonIndex = i; } }
    public struct ResourceChanged { public ResourceType Type; public int Amount; public ResourceChanged(ResourceType t, int a) { Type = t; Amount = a; } }
    public struct RoomBuilt { public Room Room; public RoomBuilt(Room r) { Room = r; } }
    public struct AntSpawned { public Ant Ant; public AntSpawned(Ant a) { Ant = a; } }
    public struct AntDied { public Ant Ant; public DeathCause Cause; public AntDied(Ant a, DeathCause c) { Ant = a; Cause = c; } }
    public struct AntSelected { public Ant Ant; public AntSelected(Ant a) { Ant = a; } }
    public struct AntMutated { public Ant Ant; public GeneData Gene; public AntMutated(Ant a, GeneData g) { Ant = a; Gene = g; } }
    public struct GeneInherited { public CasteType Caste; public GeneData Gene; public GeneInherited(CasteType c, GeneData g) { Caste = c; Gene = g; } }
}
