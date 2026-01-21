public interface IPowerable
{
    bool IsPowered { get; set; }
    void UpdatePowerState(bool powered);
}
