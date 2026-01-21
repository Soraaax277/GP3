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

    public override void OnTurnStart(PlayerData activePlayer)
    {
        base.OnTurnStart(activePlayer);
        
        if (owner == activePlayer)
        {
            ApplyDenialEffect();
        }
    }

    private void ApplyDenialEffect()
    {
        var tilesInRange = GridManager.Instance.GetTilesInRange(currentTile, denyRange);
        bool anyTriggered = false;

        foreach (HexTile tile in tilesInRange)
        {
            var enemyInfluences = tile.influenceByPlayer
                .Where(kvp => kvp.Key != owner && kvp.Value > 0)
                .ToList();

            if (enemyInfluences.Count > 0)
            {
                if (Random.value < denyChance)
                {
                    anyTriggered = true;
                    foreach (var kvp in enemyInfluences)
                    {
                        tile.RemoveInfluence(kvp.Key, denyAmount);
                        Debug.Log($"[SalesMarketer] Denied {denyAmount} influence of {kvp.Key.playerName} at {tile.name}");
                    }
                }
            }
        }

        if (anyTriggered)
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.NotifyStatusChanged();
            StartCoroutine(FlashIndicator(Color.cyan));
        }
    }

    void CreateRangeIndicator()
    {
        if (rangeIndicator != null) return;

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

    private void OnMouseEnter()
    {
        ShowRange(true);
    }

    private void OnMouseExit()
    {
        ShowRange(true);
    }

    private System.Collections.IEnumerator FlashIndicator(Color flashColor)
    {
        if (rangeIndicator == null) yield break;
        
        Renderer rend = rangeIndicator.GetComponent<Renderer>();
        Color original = rend.material.color;
        
        rangeIndicator.SetActive(true);
        rend.material.color = flashColor;
        
        yield return new WaitForSeconds(0.5f);
        
        rend.material.color = original;
        ShowRange(false);
    }
}
