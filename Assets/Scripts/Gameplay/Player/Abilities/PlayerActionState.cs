using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// The player's current "action" (slash, stomp, skill) — so it can be cancelled at the right moment:
    /// each action takes a token (<see cref="Begin"/>); a roll / heavy hit / death calls <see cref="Interrupt"/>, invalidating the old token,
    /// and a coroutine waiting for its hit frame checks <see cref="Alive"/> and stops itself.
    /// <see cref="LockUntil"/> = when the player may move again (stands still while slashing / stomping).
    /// </summary>
    internal sealed class PlayerActionState
    {
        private readonly Health health;
        private int id;

        public float LockUntil;

        public PlayerActionState(Health health) { this.health = health; }

        public bool IsLocked => Time.time < LockUntil;

        public int Begin() => ++id;
        public void Interrupt() => id++;
        public bool Alive(int actionId) => actionId == id && !health.IsDead && !health.IsStunned;
        public void Lock(float seconds) => LockUntil = Mathf.Max(LockUntil, Time.time + seconds);
    }
}
