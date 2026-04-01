using UnityEngine;

public class XRayGlasses : Item {
    private readonly int freePeeks;

    public XRayGlasses(Sprite icon, int peeks, ItemKind kind) : base(icon, kind) {
        freePeeks = peeks;
    }

    public override string Description => $"Free peek an omen of your choosing";
    public override void Use() => GameManager.Instance.FreePeek(freePeeks);
}