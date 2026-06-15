using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingSelectionManager : MonoBehaviour
{
    public static BuildingSelectionManager Instance { get; private set; }

    private GameObject _currentSelected;
    private int        _lastSelectFrame = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Update()
    {
        if (_currentSelected == null) return;
        if (PauseMenuUI.GameIsPaused) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // If Select() was called this same frame, never immediately deselect.
            if (Time.frameCount == _lastSelectFrame) return;

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit,
                Mathf.Infinity, Physics.AllLayers,
                QueryTriggerInteraction.Collide))
            {
                if (hit.collider.GetComponentInParent<TowerNode>()     != null) return;
                if (hit.collider.GetComponentInParent<SignalNode>()    != null) return;
                if (hit.collider.GetComponentInParent<StructureNode>() != null) return;
            }

            Deselect();
        }
    }

    public void Select(GameObject obj, PlayerData nodeOwner)
    {
        if (obj == null) return;
        _lastSelectFrame = Time.frameCount;

        if (_currentSelected == obj) { Deselect(); return; }

        _currentSelected = obj;

        // Hex highlight is only shown for the current human player's own buildings.
        // Enemy buildings and AI turns get no tile highlight.
        PlayerData current = TurnManager.Instance?.currentPlayer;
        if (nodeOwner != null && nodeOwner == current && !nodeOwner.isAI)
        {
            SelectionRing.Instance?.Show(obj, new Color(0.2f, 0.6f, 1f));
        }
    }

    public void Deselect()
    {
        if (_currentSelected == null) return;
        SelectionRing.Instance?.Hide();
        _currentSelected = null;
    }

    public void NotifyDestroyed(GameObject obj)
    {
        if (_currentSelected == obj)
        {
            SelectionRing.Instance?.Hide();
            _currentSelected = null;
        }
    }

    public bool IsSelected(GameObject obj) => _currentSelected == obj;
}