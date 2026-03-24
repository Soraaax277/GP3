using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompanyNameUIFix : MonoBehaviour
{
    private void OnEnable()
    {
        // 1. Hide the white background on start/enable
        Image bg = GetComponent<Image>();
        if (bg != null) bg.enabled = false;

        // 2. Hide the "Enter text..." placeholder so it's clean
        TMP_InputField input = GetComponent<TMP_InputField>();
        if (input != null && input.placeholder != null)
        {
            input.placeholder.gameObject.SetActive(false);
        }

        // 3. Ensure the text color matches the light theme
        if (input != null && input.textComponent != null)
        {
            input.textComponent.color = Color.white;
            input.textComponent.alignment = TextAlignmentOptions.Left;
        }
    }
}
