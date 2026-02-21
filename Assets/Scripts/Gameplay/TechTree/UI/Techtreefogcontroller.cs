// WIP
// This script manages the fog layers on the Tech Tree UI, revealing eras and gates as the player progresses.
// Kung mapagana niyo go lang - charles

/*using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class TechTreeFogController : MonoBehaviour
{
    public static TechTreeFogController Instance;

    // -----------------------------------------------------------------------
    //  ERA 1 — INDUSTRIAL 
    // -----------------------------------------------------------------------
    [Header("Era 1 — Industrial Fog")]
    [SerializeField] private RawImage era1FogPartial;
    [SerializeField] private TechButton era1GateButton;
    [SerializeField] private List<TechButton> era1AllButtons;

    // -----------------------------------------------------------------------
    //  ERA 2 — EIGHTIES
    // -----------------------------------------------------------------------
    [Header("Era 2 — Eighties Fog")]
    [SerializeField] private RawImage era2FogFull;
    [SerializeField] private RawImage era2FogPartial;
    [SerializeField] private TechButton era2GateButton;
    [SerializeField] private List<TechButton> era2AllButtons;

    // -----------------------------------------------------------------------
    //  ERA 3 — RETRO
    // -----------------------------------------------------------------------
    [Header("Era 3 — Retro Fog")]
    [SerializeField] private RawImage era3FogFull;
    [SerializeField] private RawImage era3FogPartial;
    [SerializeField] private TechButton era3GateButton;
    [SerializeField] private List<TechButton> era3AllButtons;

    // -----------------------------------------------------------------------
    //  ERA 4 — FUTURISTIC
    // -----------------------------------------------------------------------
    [Header("Era 4 — Futuristic Fog")]
    [SerializeField] private RawImage era4FogFull;
    [SerializeField] private RawImage era4FogPartial;
    [SerializeField] private TechButton era4GateButton;
    [SerializeField] private List<TechButton> era4AllButtons;

    // -----------------------------------------------------------------------
    //  SCROLL & ANIMATION
    // -----------------------------------------------------------------------
    [Header("Scroll")]
    [SerializeField] private ScrollRect mainScrollRect;
    [SerializeField] private float scrollPeekOffset = 0.025f;

    [Header("Animation")]
    [SerializeField] private float dissolveDuration = 1.2f;

    // -----------------------------------------------------------------------
    //  INTERNALS
    // -----------------------------------------------------------------------
    private static readonly int DissolveID = Shader.PropertyToID("_Dissolve");

    private Material mat_era1Partial;
    private Material mat_era2Full, mat_era2Partial;
    private Material mat_era3Full, mat_era3Partial;
    private Material mat_era4Full, mat_era4Partial;

    private enum EraFogState { FullyFogged, GateRevealed, FullyClear }
    private EraFogState[] eraStates = new EraFogState[4]
    {
        EraFogState.GateRevealed, 
        EraFogState.FullyFogged,
        EraFogState.FullyFogged,
        EraFogState.FullyFogged
    };

    private bool _initialised = false;

    private void Awake()
    {
        Instance = this;
        CreateMaterialInstances();
    }

    private void CreateMaterialInstances()
    {
        mat_era1Partial = InstantiateMat(era1FogPartial);
        mat_era2Full    = InstantiateMat(era2FogFull);
        mat_era2Partial = InstantiateMat(era2FogPartial);
        mat_era3Full    = InstantiateMat(era3FogFull);
        mat_era3Partial = InstantiateMat(era3FogPartial);
        mat_era4Full    = InstantiateMat(era4FogFull);
        mat_era4Partial = InstantiateMat(era4FogPartial);
    }

    private Material InstantiateMat(RawImage img)
    {
        if (img == null || img.material == null) return null;
        var instance = new Material(img.material);
        instance.name = img.material.name + "_Instance";
        img.material  = instance;
        return instance;
    }

    private void Start()
    {
        if (mainScrollRect != null)
            mainScrollRect.onValueChanged.AddListener(OnScrollChanged);
    }

    private void OnDestroy()
    {
        if (mainScrollRect != null)
            mainScrollRect.onValueChanged.RemoveListener(OnScrollChanged);
    }

    public void InitialiseFog()
    {
        eraStates[0] = IsEraComplete(era1AllButtons) ? EraFogState.FullyClear : EraFogState.GateRevealed;
        eraStates[1] = IsEraComplete(era2AllButtons) ? EraFogState.FullyClear :
                       IsGateUnlocked(era2GateButton) ? EraFogState.GateRevealed :
                       IsEraComplete(era1AllButtons) ? EraFogState.GateRevealed :
                       EraFogState.FullyFogged;
        eraStates[2] = IsEraComplete(era3AllButtons) ? EraFogState.FullyClear :
                       IsGateUnlocked(era3GateButton) ? EraFogState.GateRevealed :
                       IsEraComplete(era2AllButtons) ? EraFogState.GateRevealed :
                       EraFogState.FullyFogged;
        eraStates[3] = IsEraComplete(era4AllButtons) ? EraFogState.FullyClear :
                       IsGateUnlocked(era4GateButton) ? EraFogState.GateRevealed :
                       IsEraComplete(era3AllButtons) ? EraFogState.GateRevealed :
                       EraFogState.FullyFogged;

        ApplyEra1Fog(eraStates[0], animate: false);
        ApplyEraFog(era2FogFull, mat_era2Full, era2FogPartial, mat_era2Partial, eraStates[1], animate: false);
        ApplyEraFog(era3FogFull, mat_era3Full, era3FogPartial, mat_era3Partial, eraStates[2], animate: false);
        ApplyEraFog(era4FogFull, mat_era4Full, era4FogPartial, mat_era4Partial, eraStates[3], animate: false);

        _initialised = true;
    }

    public void RefreshFog()
    {
        if (!_initialised) { InitialiseFog(); return; }
        EvaluateEra1();
        EvaluateEra2();
        EvaluateEra3();
        EvaluateEra4();
    }

    private void OnScrollChanged(Vector2 _)
    {
        if (mainScrollRect == null) return;
        float max = GetMaxNormalizedScroll();
        if (mainScrollRect.horizontalNormalizedPosition > max)
        {
            mainScrollRect.horizontalNormalizedPosition = max;
            mainScrollRect.velocity = Vector2.zero;
        }
    }

    public float GetMaxNormalizedScroll()
    {
        if (eraStates[3] == EraFogState.FullyClear)   return 1.0f;
        if (eraStates[3] == EraFogState.GateRevealed) return 0.75f + scrollPeekOffset;
        if (eraStates[2] == EraFogState.FullyClear)   return 0.75f + scrollPeekOffset;
        if (eraStates[2] == EraFogState.GateRevealed) return 0.50f + scrollPeekOffset;
        if (eraStates[1] == EraFogState.FullyClear)   return 0.50f + scrollPeekOffset;
        if (eraStates[1] == EraFogState.GateRevealed) return 0.25f + scrollPeekOffset;
        if (eraStates[0] == EraFogState.FullyClear)   return 0.25f + scrollPeekOffset;
        return scrollPeekOffset;
    }

    private void EvaluateEra1()
    {
        EraFogState next = IsEraComplete(era1AllButtons) ? EraFogState.FullyClear : EraFogState.GateRevealed;
        if (next == eraStates[0]) return;
        eraStates[0] = next;
        ApplyEra1Fog(next, animate: true);
    }

    private void EvaluateEra2()
    {
        bool era1Done     = IsEraComplete(era1AllButtons);
        bool gateUnlocked = IsGateUnlocked(era2GateButton);
        bool allUnlocked  = IsEraComplete(era2AllButtons);

        EraFogState next = allUnlocked  ? EraFogState.FullyClear  :
                           gateUnlocked ? EraFogState.GateRevealed :
                           era1Done     ? EraFogState.GateRevealed :
                                          EraFogState.FullyFogged;
        if (next == eraStates[1]) return;
        eraStates[1] = next;
        ApplyEraFog(era2FogFull, mat_era2Full, era2FogPartial, mat_era2Partial, next, animate: true);
    }

    private void EvaluateEra3()
    {
        bool era2Done     = IsEraComplete(era2AllButtons);
        bool gateUnlocked = IsGateUnlocked(era3GateButton);
        bool allUnlocked  = IsEraComplete(era3AllButtons);

        EraFogState next = allUnlocked  ? EraFogState.FullyClear  :
                           gateUnlocked ? EraFogState.GateRevealed :
                           era2Done     ? EraFogState.GateRevealed :
                                          EraFogState.FullyFogged;
        if (next == eraStates[2]) return;
        eraStates[2] = next;
        ApplyEraFog(era3FogFull, mat_era3Full, era3FogPartial, mat_era3Partial, next, animate: true);
    }

    private void EvaluateEra4()
    {
        bool era3Done     = IsEraComplete(era3AllButtons);
        bool gateUnlocked = IsGateUnlocked(era4GateButton);
        bool allUnlocked  = IsEraComplete(era4AllButtons);

        EraFogState next = allUnlocked  ? EraFogState.FullyClear  :
                           gateUnlocked ? EraFogState.GateRevealed :
                           era3Done     ? EraFogState.GateRevealed :
                                          EraFogState.FullyFogged;
        if (next == eraStates[3]) return;
        eraStates[3] = next;
        ApplyEraFog(era4FogFull, mat_era4Full, era4FogPartial, mat_era4Partial, next, animate: true);
    }

    private void ApplyEra1Fog(EraFogState state, bool animate)
    {
        switch (state)
        {
            case EraFogState.GateRevealed:
                SetDissolve(mat_era1Partial, 0f, era1FogPartial);
                break;

            case EraFogState.FullyClear:
                if (animate) StartCoroutine(DissolveOut(mat_era1Partial, era1FogPartial));
                else         SetDissolve(mat_era1Partial, 1f, era1FogPartial);
                break;
        }
    }

    private void ApplyEraFog(RawImage fogFull, Material matFull,
                              RawImage fogPartial, Material matPartial,
                              EraFogState state, bool animate)
    {
        switch (state)
        {
            case EraFogState.FullyFogged:
                SetDissolve(matFull,    0f, fogFull);
                SetDissolve(matPartial, 1f, fogPartial);
                break;

            case EraFogState.GateRevealed:
                SetDissolve(matPartial, 0f, fogPartial); 
                if (animate) StartCoroutine(DissolveOut(matFull, fogFull));
                else         SetDissolve(matFull, 1f, fogFull);
                break;

            case EraFogState.FullyClear:
                if (animate)
                {
                    StartCoroutine(DissolveOut(matFull, fogFull));
                    StartCoroutine(DissolveOut(matPartial, fogPartial));
                }
                else
                {
                    SetDissolve(matFull,    1f, fogFull);
                    SetDissolve(matPartial, 1f, fogPartial);
                }
                break;
        }
    }

    // --- HELPERS ---

    private void SetDissolve(Material mat, float value, RawImage img)
    {
        if (mat != null) 
        {
            mat.SetFloat(DissolveID, value);
            if (img != null) img.SetMaterialDirty(); // Force UI update immediately
        }
    }

    private IEnumerator DissolveOut(Material mat, RawImage img)
    {
        if (mat == null || img == null) yield break;

        float elapsed = 0f;
        float start   = mat.GetFloat(DissolveID);

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.unscaledDeltaTime; 
            mat.SetFloat(DissolveID, Mathf.Lerp(start, 1f, Mathf.Clamp01(elapsed / dissolveDuration)));
            img.SetMaterialDirty(); // <--- THIS FORCES THE CANVAS TO REDRAW EVERY FRAME
            yield return null;
        }

        mat.SetFloat(DissolveID, 1f);
        img.SetMaterialDirty();
    }

    // --- BUTTON LOGIC HELPERS ---

    private bool IsGateUnlocked(TechButton gateBtn)
    {
        return gateBtn != null && gateBtn.IsUnlocked;
    }

    private bool IsEraComplete(List<TechButton> buttons)
    {
        if (buttons == null || buttons.Count == 0) return false;
        foreach (var btn in buttons)
        {
            if (btn != null && !btn.IsUnlocked) return false;
        }
        return true;
    }
}*/