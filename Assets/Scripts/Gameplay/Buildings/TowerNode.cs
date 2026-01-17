using UnityEngine;

public class TowerNode : MonoBehaviour
{
    public enum TowerState
    {
        Unbuilt,
        Built
    }

    public HexTile tile;
    public int range = 3;

    public TowerState state { get; private set; }

    private GameObject rangeIndicator;

    // Called when tower is placed
    public void Initialize(HexTile hexTile)
    {
        tile = hexTile;
        tile.placedTower = this;

        state = TowerState.Unbuilt;

        CreateRangeIndicator();
        ShowRange(false); // IMPORTANT: unbuilt towers show NO range
    }

    // Called only for placement preview
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
        rangeIndicator.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        rangeIndicator.transform.localRotation = Quaternion.identity;

        rangeIndicator.transform.localScale =
            new Vector3(range * 2f, 0.01f, range * 2f);

        Renderer rend = rangeIndicator.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));

        Destroy(rangeIndicator.GetComponent<Collider>());
    }

    // CALLED LATER by builder
    public void Build()
    {
        if (state == TowerState.Built)
            return;

        state = TowerState.Built;

        // Built towers show range
        SetRangeColor(new Color(0f, 1f, 0f, 0.25f));
        ShowRange(true);

        // Signal logic will hook here later
        Debug.Log("Tower built and now operational");
    }

    public bool IsBuilt()
    {
        return state == TowerState.Built;
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
}
