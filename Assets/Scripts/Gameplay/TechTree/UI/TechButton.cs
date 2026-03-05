using UnityEngine;
using UnityEngine.UI;

public class TechButton : MonoBehaviour
{
    [Header("Tech to Unlock")]
    public TechNode tech;

    [Header("UI References")]
    public Button button;

    [Tooltip("The Image component on a Child object that holds the colored sprite. This is what will be tinted.")]
    public Image targetImage;

    void Start()
    {
        InitializeComponents();

        // Add Listener to click
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnTechClicked);
        }

        UpdateNodeVisuals();
    }

    // Helper to ensure components are found even if Start() hasn't run yet
    private void InitializeComponents()
    {
        if (button == null) button = GetComponent<Button>();

        // Make the parent button invisible but keep Raycast Target ON so that
        // UIAnimator (also on this GameObject) continues to receive pointer events.
        Image parentButtonImage = GetComponent<Image>();
        if (parentButtonImage != null)
        {
            parentButtonImage.color = Color.clear;
            parentButtonImage.raycastTarget = true; // MUST stay true for pointer events to work
        }

        // Find child image if not assigned manually
        if (targetImage == null)
        {
            // Try to find an image in children that isn't the parent button image itself
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach(var img in images)
            {
                if(img.gameObject != this.gameObject)
                {
                    targetImage = img;
                    break; // Found first child image
                }
            }
        }
    }

    private void OnTechClicked()
    {
        if (TechTreeWindowManager.Instance != null && tech != null)
        {
            TechTreeWindowManager.Instance.SelectTechNode(tech);
        }
    }

    public void UpdateNodeVisuals()
    {
        InitializeComponents();

        // Ensure we have necessary components before proceeding
        if (tech == null || button == null || targetImage == null) return;

        // Ensure we check the HUMAN player's status (Player 0)
        PlayerData humanPlayer = (GameManager.Instance != null && GameManager.Instance.players.Count > 0) 
            ? GameManager.Instance.players[0] : null;

        if (humanPlayer == null) return;

        // Apply logic to tint the child image sprite
        if (tech.IsUnlockedBy(humanPlayer))
        {
            button.interactable = true;
            targetImage.color = new Color(1f, 0.95f, 0.8f, 1f); 
        }
        else if (tech.CanUnlockFor(humanPlayer))
        {
            button.interactable = true;
            targetImage.color = Color.white; 
        }
        else
        {
            button.interactable = true;
            // Gray tint desaturates and darkens the colored sprite underneath
            targetImage.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        }
    }
}