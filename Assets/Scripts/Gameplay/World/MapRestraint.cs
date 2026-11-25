using UnityEngine;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// MAP RESTRAINTS - reads map properties (Tiled ▸ Map Properties):
    ///   disarm_until      = flags ("a,b"): until set, Ren has no sword (cannot slash) - e.g. Map 4: m4_katana (taken back in the guard room)
    ///   mantra_seal_until = flags: until set, Mantras are sealed (no skills) - e.g. Map 4: m4_mantras_free (Pell removes the seal)
    ///   disarm_text / mantra_seal_text = text shown when Ren tries to slash / use a skill while restrained.
    /// Added automatically by MapEncounterDirector when the map has either property. Every 0.2 s it sets PlayerStatus.Disarmed / MantrasBound.
    /// Leaving the map (another scene without the properties) -> the new PlayerStatus is not restrained.
    /// While disarmed Ren wears the "sacrificial robe" - the Animator switches to Resources/Player/RenRobe (idle breathing + walk, built with
    /// Soul Requiem ▸ Player ▸ Ren Robe Animator (Map 4)); once the sword flag is set (or the map is left) -> the original controller returns.
    /// The flask is taken together with the sword (PlayerStatus.FlaskTaken = Disarmed).
    /// </summary>
    public sealed class MapRestraint : MonoBehaviour
    {
        private string disarmUntil = "", sealUntil = "", disarmText = "", sealText = "";
        private MapEncounterDirector dir;
        private float next;
        private Animator anim;
        private RuntimeAnimatorController normalCtrl, robeCtrl;
        private bool robeOn;

        /// <summary>Set the map's restraint flags (get the sword + flask + normal outfit back, unseal Mantras), then apply at once.</summary>
        internal string DebugLift()
        {
            if (dir == null || dir.Flags == null) return "";
            string done = "";
            foreach (var list in new[] { disarmUntil, sealUntil })
                foreach (var f in list.Split(','))
                    if (f.Trim().Length > 0 && !dir.Flags.Has(f.Trim())) { dir.Flags.Set(f.Trim()); done += (done.Length > 0 ? ", " : "") + f.Trim(); }
            next = 0f;
            return done;
        }

        public void Setup(MapEncounterDirector director, string disarm, string seal, string dText, string sText)
        {
            dir = director;
            disarmUntil = disarm ?? ""; sealUntil = seal ?? "";
            disarmText = dText ?? ""; sealText = sText ?? "";
        }

        private void Update()
        {
            if (Time.time < next || dir == null || dir.Flags == null || dir.Player == null) return;
            next = Time.time + 0.2f;
            var st = PlayerStatus.Of(dir.Player);
            if (st == null) return;
            st.Disarmed = disarmUntil.Length > 0 && !dir.Flags.HasAll(disarmUntil);
            st.FlaskTaken = st.Disarmed;                     // the flask is taken with the sword and returned at the same time (flag disarm_until)
            st.MantrasBound = sealUntil.Length > 0 && !dir.Flags.HasAll(sealUntil);
            if (disarmText.Length > 0) st.DisarmText = disarmText;
            if (sealText.Length > 0) st.MantraSealText = sealText;
            SetRobe(st.Disarmed);
        }

        /// <summary>Swap Ren's look: robe while disarmed, normal outfit once the sword is back. Only swaps on a state change (swapping the controller resets the Animator).</summary>
        private void SetRobe(bool on)
        {
            if (on == robeOn) return;
            if (anim == null)
            {
                var pc = dir.Player.GetComponentInChildren<PlayerController>();
                anim = pc != null ? pc.GetComponent<Animator>() : dir.Player.GetComponentInChildren<Animator>();
                if (anim == null) return;
            }
            if (on)
            {
                if (robeCtrl == null) robeCtrl = Resources.Load<RuntimeAnimatorController>("Player/RenRobe");
                if (robeCtrl == null) { Debug.LogWarning("MapRestraint: Resources/Player/RenRobe is missing - run Soul Requiem > Player > Ren Robe Animator (Map 4)."); robeOn = true; return; }
                normalCtrl = anim.runtimeAnimatorController;
                anim.runtimeAnimatorController = robeCtrl;
            }
            else if (normalCtrl != null && anim.runtimeAnimatorController == robeCtrl)
                anim.runtimeAnimatorController = normalCtrl;
            robeOn = on;
        }

        private void OnDestroy()
        {
            if (dir == null || dir.Player == null) return;
            var st = dir.Player.GetComponent<PlayerStatus>();
            if (st != null) { st.Disarmed = false; st.MantrasBound = false; st.FlaskTaken = false; }
            if (robeOn) SetRobe(false);
        }
    }
}
