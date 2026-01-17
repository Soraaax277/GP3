using UnityEngine;

public static class HologramUtil
{
    public static void MakeHologram(GameObject obj, Color color)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in renderers)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = color;

            r.material = mat;
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
            c.enabled = false;
    }

    public static void MakeSolid(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in renderers)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.white;
            r.material = mat;
        }

        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
            c.enabled = true;
    }

}
