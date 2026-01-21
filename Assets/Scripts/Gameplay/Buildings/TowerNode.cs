using UnityEngine;

public class TowerNode : MonoBehaviour, IInfrastructure, IPowerable
{
    public HexTile ParentTile => tile;
    public bool IsPowered { get; set; }
    public enum TowerState
    {
        Unbuilt,
        Built,
        Destroyed
    }

    public HexTile tile;
    public int range = 3;

    public TowerState state { get; private set; }

    private GameObject rangeIndicator;

    public void Initialize(HexTile hexTile)
    {
        tile = hexTile;
        tile.placedTower = this;

        state = TowerState.Unbuilt;

        CreateRangeIndicator();
        ShowRange(false);

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
        }
    }

    public void CreatePreview()
    {
        CreateRangeIndicator();
        ShowRange(true);
    }

    void CreateRangeIndicator()
    {
        if (rangeIndicator != null) return;

        rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeIndicator.transform.SetParent(transform);
        rangeIndicator.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        rangeIndicator.transform.localRotation = Quaternion.identity;

        float visualRadius = range * GridManager.Instance.hexSize;

        rangeIndicator.transform.localScale =
            new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);

        Renderer rend = rangeIndicator.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        Destroy(rangeIndicator.GetComponent<Collider>());
    }

    public void Build()
    {
        if (state == TowerState.Built)
            return;

        state = TowerState.Built;

        ApplyInfluence();

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
        }

        Debug.Log("Tower built and now operational (pending power)");
    }

    public bool IsBuilt()
    {
        return state == TowerState.Built;
    }

    public bool IsDestroyed()
    {
        return state == TowerState.Destroyed;
    }

    public void SetRangeColor(Color color)
    {
        if (rangeIndicator == null) return;
        rangeIndicator.GetComponent<Renderer>().material.color = color;
    }

    public void ShowRange(bool show)
    {
        if (rangeIndicator != null)
            rangeIndicator.SetActive(show);
    }

    private void OnMouseEnter()
    {
        if (state == TowerState.Built)
            ShowRange(true);
    }

    private void OnMouseExit()
    {
        if (state == TowerState.Built)
            ShowRange(false);
    }

    void ApplyInfluence()
    {
        if (!IsPowered && state == TowerState.Built)
        {
            Debug.Log($"{name} is unpowered - influence not applied");
            return;
        }

        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, 1);

        foreach (HexTile t in tilesInRange)
        {
            t.influence += t.baseInfluence;
            Debug.Log($"{t.name} gained +{t.baseInfluence} influence from tower");
        }
    }

    public void UpdatePowerState(bool powered)
    {
        bool wasPowered = IsPowered;
        IsPowered = powered;

        if (state != TowerState.Built) return;

        if (powered)
        {
            SetRangeColor(new Color(0f, 1f, 0f, 0.25f));
            if (!wasPowered) ApplyInfluence();
        }
        else
        {
            SetRangeColor(new Color(1f, 0.5f, 0f, 0.25f)); // Orange for unpowered
            if (wasPowered) RemoveInfluence();
        }
    }

    void RemoveInfluence()
    {
        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, 1);

        foreach (HexTile t in tilesInRange)
        {
            t.influence -= t.baseInfluence;
            Debug.Log($"{t.name} lost influence from tower (power cut)");
        }
    }

    public void CheckForDestruction()
    {
        if (state != TowerState.Built)
            return;

        if (Random.value < 0.1f)
        {
            DestroyTower();
        }
    }

    void DestroyTower()
    {
        state = TowerState.Destroyed;
        ShowRange(false);
        Debug.Log("Tower has been destroyed and needs repair!");
    }
}
