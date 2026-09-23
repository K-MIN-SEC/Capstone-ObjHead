using System;
using UnityEngine;

[DisallowMultipleComponent]
public class CommonHeadInventory : MonoBehaviour
{
    public static int SlotCount => Mathf.Clamp(ObjectHeadContent.Load()?.commonInventoryCapacity ?? 6,1,12);
    [SerializeField, Min(1)] private int playerIndex = 1;
    // Unity constructs MonoBehaviours before Resources.Load is legal. Resize from
    // the editable content capacity in EnsureSlots once gameplay is initialized.
    [SerializeField] private CommonHeadType[] slots = Array.Empty<CommonHeadType>();
    [SerializeField] private Sprite[] slotSprites = Array.Empty<Sprite>();

    public event Action InventoryChanged;
    public int PlayerIndex => playerIndex;
    public CommonHeadType[] CaptureSlots(){EnsureSlots();return (CommonHeadType[])slots.Clone();}
    public void ApplyAuthoritativeSlots(CommonHeadType[] values)
    {
        if(ObjectHeadCommonAuthority.CanWrite || values==null || values.Length!=SlotCount)return;
        var catalog=ObjectHeadContent.Load();
        foreach(var value in values)if(value!=CommonHeadType.None && catalog?.Common(value)==null)return;
        EnsureSlots();bool changed=false;
        for(int i=0;i<SlotCount;i++)
        {
            if(slots[i]==values[i])continue;
            slots[i]=values[i];slotSprites[i]=CommonHeadItem.GetDefaultSprite(values[i]);changed=true;
        }
        if(changed)InventoryChanged?.Invoke();
    }

    public int Count
    {
        get
        {
            int count = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (GetSlot(i) != CommonHeadType.None)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public CommonHeadType GetSlot(int slotIndex)
    {
        EnsureSlots();
        return slotIndex >= 0 && slotIndex < SlotCount ? slots[slotIndex] : CommonHeadType.None;
    }

    public Sprite GetSlotSprite(int slotIndex)
    {
        EnsureSlots();
        return slotIndex >= 0 && slotIndex < SlotCount ? slotSprites[slotIndex] : null;
    }

    public void ConfigurePlayer(int ownerPlayerIndex)
    {
        playerIndex = Mathf.Max(1, ownerPlayerIndex);
        gameObject.name = $"P{playerIndex}_CommonHeadInventory";
        EnsureSlots();
    }

    public bool TryAdd(CommonHeadType type, out int slotIndex)
    {
        return TryAdd(type, CommonHeadItem.GetDefaultSprite(type), out slotIndex);
    }

    public bool TryAdd(CommonHeadType type, Sprite sprite, out int slotIndex)
    {
        EnsureSlots();
        slotIndex = -1;
        if(!ObjectHeadCommonAuthority.CanWrite)return false;
        if (type == CommonHeadType.None)
        {
            return false;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] != CommonHeadType.None)
            {
                continue;
            }

            slots[i] = type;
            slotSprites[i] = sprite != null ? sprite : CommonHeadItem.GetDefaultSprite(type);
            slotIndex = i;
            InventoryChanged?.Invoke();
            return true;
        }

        return false;
    }

    public bool TryConsume(int slotIndex, out CommonHeadType type)
    {
        if(!ObjectHeadCommonAuthority.CanWrite){type=CommonHeadType.None;return false;}
        type = GetSlot(slotIndex);
        if (type == CommonHeadType.None)
        {
            return false;
        }

        slots[slotIndex] = CommonHeadType.None;
        slotSprites[slotIndex] = null;
        InventoryChanged?.Invoke();
        return true;
    }

    private void EnsureSlots()
    {
        if (slots == null || slots.Length != SlotCount)
        {
            CommonHeadType[] resized = new CommonHeadType[SlotCount];
            if (slots != null)
            {
                Array.Copy(slots, resized, Mathf.Min(slots.Length, resized.Length));
            }

            slots = resized;
        }

        if (slotSprites == null || slotSprites.Length != SlotCount)
        {
            Sprite[] resizedSprites = new Sprite[SlotCount];
            if (slotSprites != null)
            {
                Array.Copy(slotSprites, resizedSprites, Mathf.Min(slotSprites.Length, resizedSprites.Length));
            }

            slotSprites = resizedSprites;
        }
    }
}
