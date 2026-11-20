#if UNITY_EDITOR || DEVELOPMENT_BUILD   // công cụ chẩn đoán — không có trong bản phát hành
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using SoulRequiem.Core;
using SoulRequiem.Gameplay;

namespace SoulRequiem.DevTools
{
    /// <summary>
    /// Công cụ chẩn đoán (chỉ chạy khi có file Logs/claude_preview/animlog.on):
    /// ghi mỗi khung hình trạng thái Animator + sprite + flipX + vị trí của mọi quái vào Logs/claude_preview/anim_log.csv.
    /// Xóa file animlog.on để tắt.
    /// </summary>
    public class AnimDebugLogger : MonoBehaviour
    {
        static readonly string[] Names = { "Idle", "Move", "Attack", "Attack2", "Windup", "Recovery", "Spin", "SpinEnd", "Cast", "Death" };
        static readonly Dictionary<int, string> HashToName = new Dictionary<int, string>();
        StringBuilder sb = new StringBuilder();
        string path;
        float t0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string flag = Path.Combine(Application.dataPath, "../Logs/claude_preview/animlog.on");
            if (!File.Exists(flag)) return;
            var go = new GameObject("AnimDebugLogger");
            DontDestroyOnLoad(go);
            go.AddComponent<AnimDebugLogger>();
        }

        void Start()
        {
            foreach (var n in Names) HashToName[Animator.StringToHash(n)] = n;
            path = Path.Combine(Application.dataPath, "../Logs/claude_preview/anim_log.csv");
            sb.AppendLine("t,id,name,state,normTime,sprite,flipX,x,y,color");
            t0 = Time.time;
        }

        void LateUpdate()
        {
            float t = Time.time - t0;
            foreach (var ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                var anim = ai.GetComponent<Animator>();
                var sr = ai.GetComponent<SpriteRenderer>();
                string st = "-"; float nt = 0f;
                if (anim != null && anim.runtimeAnimatorController != null)
                {
                    var info = anim.GetCurrentAnimatorStateInfo(0);
                    st = HashToName.TryGetValue(info.shortNameHash, out var n) ? n : info.shortNameHash.ToString();
                    nt = info.normalizedTime;
                }
                var p = ai.transform.position;
                sb.Append(t.ToString("F3")).Append(',').Append(ai.GetInstanceID()).Append(',').Append(ai.name.Replace(",", ""))
                  .Append(',').Append(st).Append(',').Append(nt.ToString("F3")).Append(',')
                  .Append(sr != null && sr.sprite != null ? sr.sprite.name : "null").Append(',')
                  .Append(sr != null && sr.flipX ? 1 : 0).Append(',').Append(p.x.ToString("F3")).Append(',').Append(p.y.ToString("F3"))
                  .Append(',').Append(sr != null ? ColorUtility.ToHtmlStringRGBA(sr.color) : "-").AppendLine();
            }
            if (sb.Length > 200000) Flush();
        }

        void Flush()
        {
            File.AppendAllText(path, sb.ToString());
            sb.Clear();
        }

        void OnApplicationQuit() => Flush();
        void OnDestroy() { if (sb.Length > 0) Flush(); }
    }
}
#endif
