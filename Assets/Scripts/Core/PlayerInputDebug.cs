using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputDebug : MonoBehaviour
{
    public HexTile testTile;
    public GameObject signalNodePrefab;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            BuildNodeCommand.Execute(testTile, signalNodePrefab);
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TurnManager.Instance.EndTurn();
        }
    }
}
