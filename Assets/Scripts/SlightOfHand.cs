using UnityEngine;

public class SlightOfHand : Item {
    private int freeFlips;
    
    public SlightOfHand(Sprite icon, int freeFlips, ItemKind kind) : base(icon, kind) {
        this.freeFlips = freeFlips;
    }

    public override string Description => "Free flip an omen of your choosing";

    public override void Use() {
        GameManager.Instance.FreeFlip(freeFlips);
    }
}