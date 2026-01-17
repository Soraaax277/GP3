using UnityEngine;

public class BuilderUnit : Unit
{
    public int moveRange = 2;

    private void OnMouseDown()
    {
        Debug.Log("Builder clicked!");
    }
}