using UnityEngine;

public class HourGlass : Item {
    private readonly int add;

    public HourGlass(Sprite icon, int seconds, ItemKind kind) : base(icon, kind) {
        add = seconds;
    }

    public override string Description => $"+{add}s on the clock";
    public override void Use() => GameManager.Instance.AddTime(add);
}