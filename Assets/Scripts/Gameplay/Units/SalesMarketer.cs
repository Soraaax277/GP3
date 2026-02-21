using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class SalesMarketer : Unit
{
    public int denyRange = 2;
    public float denyChance = 0.35f;
    public int denyAmount = 5;

    private GameObject rangeIndicator;

    public override void Initialize(HexTile spawnTile, PlayerData player)
    {
        base.Initialize(spawnTile, player);
        CreateRangeIndicator();
        ShowRange(true);
    }

    public override void ReceiveStatUpgrade(string statName, float amount)
    {
        base.ReceiveStatUpgrade(statName, amount);

        if (statName == "DenyRange")
        {
            denyRange += (int)amount;
            if (rangeIndicator != null)
            {
                float visualRadius = denyRange * GridManager.Instance.hexSize;
                rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);
            }
            Debug.Log($"SalesMarketer: Deny range increased to {denyRange}");
        }
        else if (statName == "DenyChance")
        {
            denyChance += amount;
            Debug.Log($"SalesMarketer: Deny chance increased by {amount * 100}% (now {denyChance * 100}%)");
        }
        else if (statName == "DenyAmount")
        {
            denyAmount += (int)amount;
            Debug.Log($"SalesMarketer: Deny amount increased by {(int)amount} (now {denyAmount})");
        }
        else if (statName == "Actions")
        {
            // SalesMarketer doesn't have action charges like other units
            // Actions for SalesMarketer might enable multiple denies per turn
            Debug.Log($"SalesMarketer received +{(int)amount} Actions (passive unit)");
        }
    }

    public override void OnTurnStart(PlayerData activePlayer)
    {
        base.OnTurnStart(activePlayer);
    }

    void CreateRangeIndicator()
    {
        rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rangeIndicator.transform.SetParent(transform);
        rangeIndicator.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        rangeIndicator.transform.localRotation = Quaternion.identity;

        float visualRadius = denyRange * GridManager.Instance.hexSize;
        rangeIndicator.transform.localScale = new Vector3(visualRadius * 2f, 0.01f, visualRadius * 2f);

        Renderer rend = rangeIndicator.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Sprites/Default"));
        rend.material.color = new Color(0.5f, 0f, 1f, 0.25f);

        Destroy(rangeIndicator.GetComponent<Collider>());
    }

    public void ShowRange(bool show)
    {
        if (rangeIndicator != null)
            rangeIndicator.SetActive(show);
    }

    private void OnMouseEnter() { ShowRange(true); }
    private void OnMouseExit() { ShowRange(false); }
}