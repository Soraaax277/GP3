public interface IPowerable
{
    bool IsPowered { get; set; }
    bool IsTechnicianActivated { get; set; }
    void UpdatePowerState(bool powered);
}
