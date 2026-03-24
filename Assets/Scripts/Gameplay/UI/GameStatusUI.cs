using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameStatusUI : MonoBehaviour
{
    public static GameStatusUI Instance;

    [Header("Main Stats UI")]
    public TextMeshProUGUI influenceText;
    public TextMeshProUGUI goldText;      
    public TextMeshProUGUI researchText;  
    
    [Header("Game Info UI")]
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI eraText;
    public TextMeshProUGUI turnStaterText; 

    [Header("Tech Tree Panel UI")]
    public TextMeshProUGUI techPanelGoldText;
    public TextMeshProUGUI techPanelResearchText;

    [Header("Company Name UI")]
    public TextMeshProUGUI companyNameText;
    public TMP_InputField companyRenameInput;

    // Internal cache to track changes so we don't rebuild strings every frame
    private string _cachedCompanyName = "";
    private int _cachedGold = -1;
    private int _cachedRP = -1;
    private int _cachedInfluence = -1;
    private int _cachedTurn = -1;
    private PlayerData _cachedPlayer = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (companyRenameInput != null)
        {
            companyRenameInput.onEndEdit.AddListener(SubmitRename);
            companyRenameInput.gameObject.SetActive(false);

            // KEY FIX: Tell the layout system to completely ignore this input field
            // so it never pushes or shifts any other element when active.
            var le = companyRenameInput.GetComponent<LayoutElement>();
            if (le == null) le = companyRenameInput.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
    }

    private void Start()
    {
        UpdateUI(true);
    }

    private void Update()
    {
        CheckForResourceChanges();
    }

    private void CheckForResourceChanges()
    {
        if (TurnManager.Instance == null) return;

        if (TurnManager.Instance.currentTurn != _cachedTurn)
        {
            UpdateTurnInfo();
            _cachedTurn = TurnManager.Instance.currentTurn;
        }

        PlayerData humanPlayer = GetHumanPlayer();
        
        if (humanPlayer != null)
        {
            // Update Company Name if it changed elsewhere (e.g. Load Game)
            if (humanPlayer.playerName != _cachedCompanyName)
            {
                if (companyNameText != null) companyNameText.text = humanPlayer.playerName;
                _cachedCompanyName = humanPlayer.playerName;
            }

            int income = EconomyManager.Instance != null ? EconomyManager.Instance.GetProjectedGoldIncome(humanPlayer) : 0;
            int upkeep = EconomyManager.Instance != null ? EconomyManager.Instance.GetProjectedUpkeep(humanPlayer) : 0;
            int net = income - upkeep;
            int rpIncome = EconomyManager.Instance != null ? EconomyManager.Instance.GetProjectedRPIncome(humanPlayer) : 0;

            if (humanPlayer.resources != _cachedGold)
            {
                string netSign = net >= 0 ? "+" : "";
                string goldString = $"{humanPlayer.resources} ({netSign}{net}/t)";
                if (goldText != null) goldText.text = goldString;
                if (techPanelGoldText != null) techPanelGoldText.text = goldString;
                _cachedGold = humanPlayer.resources;
            }

            if (humanPlayer.researchPoints != _cachedRP)
            {
                string rpString = $"{humanPlayer.researchPoints} (+{rpIncome}/t)";
                if (researchText != null) researchText.text = rpString;
                if (techPanelResearchText != null) techPanelResearchText.text = rpString;
                _cachedRP = humanPlayer.researchPoints;
            }

            int currentInfluence = humanPlayer.GetTotalInfluence();
            if (currentInfluence != _cachedInfluence)
            {
                if (influenceText != null) influenceText.text = $"{currentInfluence}";
                _cachedInfluence = currentInfluence;
            }
        }

        if (TurnManager.Instance.currentPlayer != _cachedPlayer)
        {
            UpdateTurnStater();
            _cachedPlayer = TurnManager.Instance.currentPlayer;
        }
    }

    // --- COMPANY RENAMING ---

    public void OpenRenameInput()
    {
        PlayerData p = GetHumanPlayer();
        if (p == null || companyRenameInput == null || companyNameText == null) return;

        companyRenameInput.text = p.playerName;

        // Make the real text INVISIBLE (not disabled!) so its layout slot stays
        // and nothing shifts. The input field (which ignores layout) floats over it.
        Color c = companyNameText.color;
        companyNameText.color = new Color(c.r, c.g, c.b, 0f);

        // Snap the input field to exactly cover the text
        RectTransform textRect  = companyNameText.GetComponent<RectTransform>();
        RectTransform inputRect = companyRenameInput.GetComponent<RectTransform>();
        if (textRect != null && inputRect != null)
        {
            inputRect.pivot           = textRect.pivot;
            inputRect.anchorMin       = textRect.anchorMin;
            inputRect.anchorMax       = textRect.anchorMax;
            inputRect.anchoredPosition = textRect.anchoredPosition;
            inputRect.sizeDelta       = textRect.sizeDelta;
        }

        companyRenameInput.gameObject.SetActive(true);
        CameraController.IsTyping = true; // Block WASD camera movement while typing
        companyRenameInput.ActivateInputField();
    }

    public void SubmitRename(string newName)
    {
        PlayerData p = GetHumanPlayer();
        if (p != null && !string.IsNullOrWhiteSpace(newName))
        {
            string cappedName = newName.Trim();
            if (cappedName.Length > 20) cappedName = cappedName.Substring(0, 20);

            p.playerName = cappedName;
            if (companyNameText != null) companyNameText.text = cappedName;
            _cachedCompanyName = cappedName;
        }

        // Restore text visibility and hide input
        if (companyNameText != null)
        {
            Color c = companyNameText.color;
            companyNameText.color = new Color(c.r, c.g, c.b, 1f);
        }
        if (companyRenameInput != null)
        {
            companyRenameInput.gameObject.SetActive(false);
            CameraController.IsTyping = false; // Re-enable WASD camera movement
        }
    }

    // Call this to force a full redraw (e.g. on Load Game)
    public void UpdateUI(bool force = false)
    {
        if (force)
        {
            _cachedGold = -1;
            _cachedRP = -1;
            _cachedInfluence = -1;
            _cachedTurn = -1;
            _cachedPlayer = null;
        }
        CheckForResourceChanges();
    }

    private void UpdateTurnInfo()
    {
        if (turnText != null)
            turnText.text = $"Turn: {TurnManager.Instance.currentTurn} / {TurnManager.MAX_TURNS}";

        if (eraText != null)
        {
            string eraName = FormatEraName(TurnManager.Instance.currentEra);
            eraText.text = $"Era: {eraName}";
        }
    }

    private void UpdateTurnStater()
    {
        if (turnStaterText == null || TurnManager.Instance.currentPlayer == null) return;

        bool isAI = TurnManager.Instance.currentPlayer.isAI;
        turnStaterText.text = isAI ? "Enemy's Turn" : "Player's Turn";
        
        // Bonus: Change color for better feedback
        turnStaterText.color = isAI ? Color.red : Color.green;
    }

    private string FormatEraName(TurnManager.GameEra era)
    {
        switch (era)
        {
            case TurnManager.GameEra.Industrial: return "Industrial";
            case TurnManager.GameEra.EarlyEighties: return "Early 80's";
            case TurnManager.GameEra.Retro: return "Retro";
            case TurnManager.GameEra.Futuristic: return "Futuristic";
            default: return "Unknown";
        }
    }

    private PlayerData GetHumanPlayer()
    {
        if (TurnManager.Instance == null) return null;
        
        var players = TurnManager.Instance.GetPlayers();
        if (players == null) return null;

        foreach (var p in players)
        {
            if (!p.isAI) return p;
        }
        
        return null;
    }
}