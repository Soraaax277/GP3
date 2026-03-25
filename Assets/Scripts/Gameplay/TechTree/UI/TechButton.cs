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

        if (button != null)
        {
            if (button.gameObject.GetComponent<UIButtonSounds>() == null)
                button.gameObject.AddComponent<UIButtonSounds>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnTechClicked);
        }

        UpdateNodeVisuals();
    }

    private void InitializeComponents()
    {
        if (button == null) button = GetComponent<Button>();

        // Make the parent button invisible but keep Raycast Target ON so that
        // UIAnimator (also on this GameObject) continues to receive pointer events.
        Image parentButtonImage = GetComponent<Image>();
        if (parentButtonImage != null)
        {
            parentButtonImage.color = Color.clear;
            parentButtonImage.raycastTarget = true;
        }

        if (targetImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != this.gameObject)
                {
                    targetImage = img;
                    break;
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

        if (tech == null || button == null || targetImage == null) return;

        PlayerData humanPlayer = (GameManager.Instance != null && GameManager.Instance.players.Count > 0) 
            ? GameManager.Instance.players[0] : null;

        if (humanPlayer == null) return;

        if (tech.IsUnlockedBy(humanPlayer))
        {
            // Fully unlocked — warm gold tint.
            button.interactable = true;
            targetImage.color = new Color(1f, 0.95f, 0.8f, 1f);
        }
        else if (tech.IsResearchingBy(humanPlayer))
        {
            // Cost paid, integration in progress — cyan tint to signal activity.
            // The info panel will show the exact turn countdown when selected.
            button.interactable = true;
            targetImage.color = new Color(0.5f, 0.9f, 1f, 1f);
        }
        else if (tech.CanUnlockFor(humanPlayer))
        {
            // Available to purchase — full white, no tint.
            button.interactable = true;
            targetImage.color = Color.white;
        }
        else
        {
            // Locked — gray/desaturated.
            button.interactable = true;
            targetImage.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        }
    }
}