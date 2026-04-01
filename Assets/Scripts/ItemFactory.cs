using UnityEngine;

public static class ItemFactory {
    public static Item Build(ItemBlueprint bp) {
        switch (bp.type) {
            case ItemType.Stopwatch:
                return new Stopwatch(bp.icon, Mathf.Max(1, bp.magnitude), bp.kind);
            case ItemType.MagnifyingGlass:
                return new MagnifyingGlass(bp.icon, Mathf.Max(1, bp.magnitude), bp.kind);
            case ItemType.HourGlass:
                return new HourGlass(bp.icon, Mathf.Max(1, bp.magnitude), bp.kind);
            case ItemType.XRayGlasses:
                return new XRayGlasses(bp.icon, Mathf.Max(1, bp.magnitude), bp.kind);
            case ItemType.SlightOfHand:
                return new SlightOfHand(bp.icon, Mathf.Max(1, bp.magnitude), bp.kind);
            default:
                Debug.LogWarning($"Unhandled ItemType {bp.type}");
                return null;
        }
    }
}