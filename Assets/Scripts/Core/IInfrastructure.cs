public interface IInfrastructure
{
    HexTile ParentTile { get; }
    void Initialize(HexTile tile);
}
