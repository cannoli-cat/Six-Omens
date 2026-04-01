using UnityEngine;

public struct OmenCardData {
    public readonly Sprite icon;
    public readonly bool isSkull;
    public bool isRevealed;

    public OmenCardData(bool isSkull, Sprite icon) {
        this.isSkull = isSkull;
        this.icon = icon;
        isRevealed = false;
    }
}