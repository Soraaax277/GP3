using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class FogRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    [Tooltip("Assign the RectTransforms of transition nodes that must stay clickable through this fog.")]
    [SerializeField] private List<RectTransform> allowedZones;

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        foreach (var zone in allowedZones)
        {
            if (zone == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(zone, screenPoint, eventCamera))
                return false; // pass through — let the node underneath receive the click
        }
        return true; // fog blocks everything else
    }
}