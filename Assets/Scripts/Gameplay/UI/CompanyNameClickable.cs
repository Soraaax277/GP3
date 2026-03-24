using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class CompanyNameClickable : MonoBehaviour, IPointerClickHandler
{
    private void Start()
    {
        // Ensure the text has Raycast Target enabled
        var text = GetComponent<TextMeshProUGUI>();
        if (text != null) text.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameStatusUI.Instance != null)
        {
            GameStatusUI.Instance.OpenRenameInput();
        }
    }
}
