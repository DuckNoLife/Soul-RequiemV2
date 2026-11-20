using System.Collections;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// 16/11 (§237): boss quỳ ở mức máu cuối, người chơi chọn — chém thêm một nhát là kết liễu (cờ `slainFlag`, boss chết bình thường,
    /// rơi di vật), hoặc nhấn [T] "Spare her" để tha (cờ `spareFlag`, boss tan thành bầy dơi, khu vẫn tính là đã dọn).
    /// Gắn lên boss lúc boss quỳ (IsoldeSkills gọi <see cref="Begin"/>). Dùng lại cho các boss tha được về sau (Candidate VI, Absalom).
    /// </summary>
    public class BossMercy : MonoBehaviour
    {
        public string spareFlag = "mercy_3", slainFlag = "isolde_slain";
        public string verb = "Spare her";
        public string kneelHint = "She kneels in the ash. Strike to end her - or [T] spare her.";
        public string spareLine = "Go. Before I change my mind.";
        public string bossReply = "...You are a stranger thing than any Hierophant.";
        public string spareVfx = "m3_intro_bats";
        public float grace = 1.0f;

        private EnemyAI ai;
        private Health hp;
        private bool resolved;

        public static BossMercy Begin(EnemyAI ai, string spareFlag, string slainFlag)
        {
            var m = ai.gameObject.AddComponent<BossMercy>();
            m.ai = ai; m.hp = ai.Health; m.spareFlag = spareFlag; m.slainFlag = slainFlag;
            return m;
        }

        private IEnumerator Start()
        {
            if (ai == null) ai = GetComponent<EnemyAI>();
            if (hp == null) hp = GetComponent<Health>();
            ai.Held = true;
            ai.Desired = Vector2.zero;
            ai.PlayState("Stagger");
            hp.Invulnerable = true;                                   // nhát vừa đánh gục không kết liễu luôn
            hp.Floor = 0f;
            hp.Died += OnDied;
            Hints.Show(kneelHint, 6f);
            InteractionSystem.Add(new Interactable
            {
                id = "mercy_" + name, verb = verb, clue = false, range = 0.55f,
                rect = new Rect((Vector2)transform.position - new Vector2(0.3f, 0.05f), new Vector2(0.6f, 0.7f)),
                anchor = (Vector2)transform.position + new Vector2(0f, 0.95f),
                available = () => !resolved && hp != null && !hp.IsDead,
                use = () => StartCoroutine(Spare()),
            });
            yield return new WaitForSeconds(grace);
            if (hp != null && !resolved) hp.Invulnerable = false;
        }

        private void Update()
        {
            if (resolved || ai == null) return;
            ai.Held = true;                                           // giữ quỳ, không bị Think của boss kéo đi
            ai.Desired = Vector2.zero;
        }

        private void OnDied(Health h)
        {
            if (resolved) return;
            resolved = true;
            var dir = MapEncounterDirector.Instance;
            if (dir != null && dir.Flags != null) dir.Flags.Set(slainFlag);
        }

        private IEnumerator Spare()
        {
            if (resolved) yield break;
            resolved = true;
            hp.Invulnerable = true;
            var dir = MapEncounterDirector.Instance;
            CutsceneState.Begin();
            var player = dir != null ? dir.Player : null;
            if (player != null) SpeechBubble.Show(player.position + new Vector3(0f, 0.85f, 0f), spareLine, 2.6f).Follow(player);
            yield return new WaitForSeconds(2.6f);
            SpeechBubble.Show(transform.position + new Vector3(0f, 0.95f, 0f), bossReply, 3.2f, new Color(1f, 0.55f, 0.6f), Subtitles.NameOf(transform)); // 19/11 (§336): tên -> có giọng
            yield return new WaitForSeconds(3.2f);
            if (dir != null && dir.Flags != null) dir.Flags.Set(spareFlag);
            var sr = GetComponent<SpriteRenderer>();
            SpriteVfx.Spawn(spareVfx, transform.position + new Vector3(0f, 0.3f, 0f), 0.9f,
                            sr != null ? sr.sortingLayerID : 0, sr != null ? sr.sortingOrder + 2 : 10, false, 14f);
            // bầy con (dơi) của bà tan theo bà
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                if (e != null && e != ai && Vector2.Distance(e.transform.position, transform.position) < 9f) Destroy(e.gameObject);
            CutsceneState.End();
            Destroy(gameObject, 0.15f);                              // khu đếm quái null = đã hết -> khu dọn xong như khi hạ boss
        }
    }
}
