using UnityEngine;

public class Stopwatch : Item {
    private readonly float freezeSeconds;

    public Stopwatch(Sprite icon, int seconds, ItemKind kind) : base(icon, kind) {
        freezeSeconds = seconds;
    }

    public override string Description => $"Freezes the timer for {freezeSeconds:0}s";

    public override void Use() {
        GameManager.Instance?.PauseTimerForSeconds(freezeSeconds);
    }
}