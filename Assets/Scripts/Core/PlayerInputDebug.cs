using UnityEngine;

public class PlayerInputDebug : MonoBehaviour
{
    public HexTile testTile;
    public GameObject signalNodePrefab;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            BuildNodeCommand.Execute(testTile, signalNodePrefab);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TurnManager.Instance.EndTurn();
        }
    }
}
