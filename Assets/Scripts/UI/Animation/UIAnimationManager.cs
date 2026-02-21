using UnityEngine;

public class UIAnimationManager : MonoBehaviour
{
    public static UIAnimationManager Instance;

    [Header("Global Defaults")]
    public UITheme defaultButtonTheme;
    public UITheme defaultTechButtonTheme;
    public UITheme defaultWindowTheme;
    public UITheme defaultShutterTheme; 
    public UITheme defaultSlideTheme; 
    public UITheme defaultPopUpTheme; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
}