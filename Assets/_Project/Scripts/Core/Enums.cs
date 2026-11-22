namespace Myrmutation.Core
{
    public enum GameState { Playing, Paused, Victory, GameOver }
    public enum Season { Spring, Summer, Autumn, Winter }
    public enum ResourceType { Food, Leaves, Fungus, Biomass }
    public enum CasteType { Minor, Media, Major }
    public enum RoomType { Royal, Nursery, Storage, FungusGarden, Feast }
    public enum BodySlot { None, Head, Antennae, Thorax, Abdomen, Legs }
    public enum StatType { Strength, Speed, HungerRate, EnergyRate, CarryCapacity, WorkRate }
    public enum DeathCause { Starvation, Eaten, Combat, Infection, Other }
}
