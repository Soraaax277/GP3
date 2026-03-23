using UnityEngine;
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
    // These update in the background even if the panel is closed/hidden
    public TextMeshProUGUI techPanelGoldText;
    public TextMeshProUGUI techPanelResearchText;

    // Internal cache to track changes so we don't rebuild strings every frame
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
    }

    private void Start()
    {
        // Initial force update to populate zeros
        UpdateUI(true);
    }

    private void Update()
    {
        // This runs every frame to catch "Spending" immediately
        CheckForResourceChanges();
    }

    private void CheckForResourceChanges()
    {
        if (TurnManager.Instance == null) return;

        // 1. Handle Turn/Era Changes
        if (TurnManager.Instance.currentTurn != _cachedTurn)
        {
            UpdateTurnInfo();
            _cachedTurn = TurnManager.Instance.currentTurn;
        }

        // 2. Handle Resource Changes (Real-time spending/income)
        PlayerData humanPlayer = GetHumanPlayer();
        
        if (humanPlayer != null)
        {
            // Calculate projections
            int income = EconomyManager.Instance != null ? EconomyManager.Instance.GetProjectedGoldIncome(humanPlayer) : 0;
            int upkeep = EconomyManager.Instance != null ? EconomyManager.Instance.GetProjectedUpkeep(humanPlayer) : 0;
            int net = income - upkeep;
            
            int rpIncome = EconomyManager.Instance != null ? EconomyManager.Instance.GetProjectedRPIncome(humanPlayer) : 0;

            // 1. Check Gold
            if (humanPlayer.resources != _cachedGold)
            {
                string netSign = net >= 0 ? "+" : "";
                string goldString = $"{humanPlayer.resources} ({netSign}{net}/t)";
                
                // Update Main HUD
                if (goldText != null) goldText.text = goldString;
                
                // Update Tech Tree Panel (works even if panel is hidden)
                if (techPanelGoldText != null) techPanelGoldText.text = goldString;
                
                _cachedGold = humanPlayer.resources;
            }

            // 2. Check Research Points
            if (humanPlayer.researchPoints != _cachedRP)
            {
                string rpString = $"{humanPlayer.researchPoints} (+{rpIncome}/t)";

                // Update Main HUD
                if (researchText != null) researchText.text = rpString;

                // Update Tech Tree Panel (works even if panel is hidden)
                if (techPanelResearchText != null) techPanelResearchText.text = rpString;

                _cachedRP = humanPlayer.researchPoints;
            }

            // 3. Check Influence
            int currentInfluence = humanPlayer.GetTotalInfluence();
            if (currentInfluence != _cachedInfluence)
            {
                if (influenceText != null) influenceText.text = $"{currentInfluence}";
                _cachedInfluence = currentInfluence;
            }
        }

        // 4. Handle Current Turn Player (Who is acting now)
        if (TurnManager.Instance.currentPlayer != _cachedPlayer)
        {
            UpdateTurnStater();
            _cachedPlayer = TurnManager.Instance.currentPlayer;
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