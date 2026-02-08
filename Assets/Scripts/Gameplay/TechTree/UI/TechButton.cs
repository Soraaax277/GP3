using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TechButton : MonoBehaviour
{
    [Header("Tech to Unlock")] public TechNode tech;
    public Button button;
    public TextMeshProUGUI techDisplay;

    void Start()
    {
        button = this.GetComponent<Button>();
        techDisplay = this.GetComponent<TextMeshProUGUI>();
    }
 
    public void SetNodeText()
    {
        techDisplay.text = tech.techName;
    }
}
