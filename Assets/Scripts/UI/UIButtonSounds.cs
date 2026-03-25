using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class UIButtonSounds : MonoBehaviour, IPointerEnterHandler
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(PlayClickSound);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && button.interactable)
        {
            PlayHoverSound();
        }
    }

    private void PlayClickSound()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.buttonClickSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonClickSFX);
        }
    }

    private void PlayHoverSound()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.buttonHoverSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.buttonHoverSFX);
        }
    }
}
