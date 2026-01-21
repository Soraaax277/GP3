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

    public PlayerData owner { get; private set; }
    public SignalNode parentNode { get; private set; }
    public HexTile tile;
    public int range = 3;
    public TowerState state { get; private set; }
    private GameObject rangeIndicator;

    void IInfrastructure.Initialize(HexTile hexTile, PlayerData player) => Initialize(hexTile, player, null);

    public void Initialize(HexTile hexTile, PlayerData player, SignalNode parent = null)
    {
        tile = hexTile;
        owner = player;
        parentNode = parent;
        tile.placedTower = this;

        if (parentNode != null)
            parentNode.towersPlacedCount++;

        TurnManager.Instance.RegisterTower(this);

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
            t.AddInfluence(owner, t.baseInfluence);
            Debug.Log($"{t.name} gained +{t.baseInfluence} influence for {owner.playerName} from tower");
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.NotifyStatusChanged();
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
            SetRangeColor(new Color(1f, 0.5f, 0f, 0.25f));
            if (wasPowered) RemoveInfluence();
        }
    }

    void RemoveInfluence()
    {
        var tilesInRange = GridManager.Instance.GetTilesInRange(tile, 1);

        foreach (HexTile t in tilesInRange)
        {
            t.RemoveInfluence(owner, t.baseInfluence);
            Debug.Log($"{t.name} lost influence for {owner.playerName} from tower (power cut)");
        }

        if (TurnManager.Instance != null)
            TurnManager.Instance.NotifyStatusChanged();
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

    public void Repair()
    {
        if (state != TowerState.Destroyed) return;

        state = TowerState.Built;
        ApplyInfluence();

        if (PowerGridManager.Instance != null)
        {
            PowerGridManager.Instance.RefreshGrid();
        }

        Debug.Log("Tower has been repaired and is now operational!");
    }

    void DestroyTower()
    {
        state = TowerState.Destroyed;
        ShowRange(false);
        Debug.Log("Tower has been destroyed and needs repair!");
    }
}
