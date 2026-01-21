public interface IInfrastructure
{
    HexTile ParentTile { get; }
    PlayerData owner { get; }
    void Initialize(HexTile tile, PlayerData player);
}
