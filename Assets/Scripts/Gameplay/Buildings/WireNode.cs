using UnityEngine;

public class WireNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile ParentTile { get; private set; }
    public PlayerData owner { get; private set; }
    public bool IsPowered { get; set; }

    private GameObject visual;

    public void Initialize(HexTile tile, PlayerData player)
    {
        ParentTile = tile;
        owner = player;
        tile.placedWire = this;

        CreateVisual();
        UpdatePowerState(false);

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
        }
    }

    void CreateVisual()
    {
        if (transform.childCount > 0)
        {
            visual = transform.GetChild(0).gameObject;
            return;
        }

        visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.transform.SetParent(transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(0.2f, 0.05f, 0.2f);
        
        Destroy(visual.GetComponent<Collider>());
    }

    public void UpdatePowerState(bool powered)
    {
        IsPowered = powered;
        
        if (visual != null)
        {
            Renderer rend = visual.GetComponent<Renderer>();
            rend.material.color = powered ? Color.yellow : Color.gray;
        }
    }
}
