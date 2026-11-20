using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// "Hành động" đang làm của người chơi (chém, dậm, kỹ năng) — để huỷ đúng lúc:
    /// mỗi hành động lấy một mã (<see cref="Begin"/>); lăn / trúng đòn nặng / gục gọi <see cref="Interrupt"/> làm mã cũ hết hiệu lực,
    /// coroutine đang chờ khung trúng đòn kiểm tra <see cref="Alive"/> rồi tự dừng.
    /// <see cref="LockUntil"/> = lúc được đi lại (đang chém / dậm thì đứng yên).
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
