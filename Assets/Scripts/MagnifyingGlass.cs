using UnityEngine;

public class MagnifyingGlass : Item {
    private readonly int flips;

    public MagnifyingGlass(Sprite icon, int flips, ItemKind kind) : base(icon, kind) {
        this.flips = flips;
    }

    public override string Description => $"Flips {flips} hidden omen(s)";
    public override void Use() => GameManager.Instance?.Reveal(flips);
}