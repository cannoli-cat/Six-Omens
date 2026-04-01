using UnityEngine;

public enum ItemKind { Consumable, Passive }
public enum ItemType { Stopwatch, MagnifyingGlass, HourGlass, XRayGlasses, SlightOfHand }

[CreateAssetMenu(menuName = "Items/Item Blueprint")]
public class ItemBlueprint : ScriptableObject {
    public Sprite icon;
    public ItemKind kind = ItemKind.Consumable;
    public ItemType type;
    public int magnitude = 1;   // e.g. seconds to freeze, number of flips, etc.
}