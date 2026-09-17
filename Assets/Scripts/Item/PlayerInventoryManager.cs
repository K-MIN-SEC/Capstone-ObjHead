using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInventoryManager : MonoBehaviour
{
    private readonly Dictionary<int, CommonHeadInventory> inventories = new Dictionary<int, CommonHeadInventory>();

    public void ConfigurePlayers(int playerCount)
    {
        inventories.Clear();
        for (int player = 1; player <= Mathf.Max(1, playerCount); player++)
        {
            GetOrCreateInventory(InventoryOwner(player));
        }
    }

    public CommonHeadInventory GetInventory(int playerIndex)
    {
        if (playerIndex <= 0)
        {
            return null;
        }

        int owner = InventoryOwner(playerIndex);

        if (inventories.TryGetValue(owner, out CommonHeadInventory inventory) && inventory != null)
        {
            return inventory;
        }

        RefreshInventories();
        return inventories.TryGetValue(owner, out inventory) ? inventory : GetOrCreateInventory(owner);
    }

    public CommonHeadInventory GetInventoryFor(GameObject character)
    {
        ObjectHeadTeamMember member = character != null ? character.GetComponent<ObjectHeadTeamMember>() : null;
        return member != null ? GetInventory(member.PlayerIndex) : null;
    }

    private static int InventoryOwner(int playerIndex)
    {
        // Common heads belong to the alliance. In FFA each player is their own
        // alliance; in 2v2 both allies therefore resolve to the same inventory.
        return Mathf.Max(1, ObjectHeadMatchRules.Alliance(playerIndex));
    }

    private CommonHeadInventory GetOrCreateInventory(int allianceId)
    {
        if (inventories.TryGetValue(allianceId, out CommonHeadInventory existing) && existing != null)
        {
            return existing;
        }

        GameObject inventoryObject = new GameObject($"Team{allianceId}_CommonHeadInventory");
        inventoryObject.transform.SetParent(transform, false);
        CommonHeadInventory inventory = inventoryObject.AddComponent<CommonHeadInventory>();
        inventory.ConfigurePlayer(allianceId);
        inventoryObject.name = $"Team{allianceId}_CommonHeadInventory";
        inventories[allianceId] = inventory;
        return inventory;
    }

    private void RefreshInventories()
    {
        inventories.Clear();
        CommonHeadInventory[] found = GetComponentsInChildren<CommonHeadInventory>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
            {
                inventories[found[i].PlayerIndex] = found[i];
            }
        }
    }
}
