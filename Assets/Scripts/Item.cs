using UnityEngine;

public abstract class Item {
    public readonly Sprite icon;
    public readonly ItemKind kind;
    public abstract string Description { get; }

    protected Item(Sprite icon, ItemKind kind) {
        this.icon = icon;
        this.kind = kind;
    }

    public abstract void Use();
}