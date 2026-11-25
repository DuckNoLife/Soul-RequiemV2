using System.Collections.Generic;
using System.Globalization;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// EXITS / TELEPORTS: exit (to another scene, may need T, may wait for the area to be cleared), portal (teleport within the map),
    /// turn_back (blocking strip: Ren says a line and is pushed back while flags `requires` are missing). Area-bound exits appear once the area is cleared (RevealExits).
    /// </summary>
    internal sealed partial class MapTriggerHandler
    {
        /// <summary>Side of p relative to the strip (along the thin axis): -1 below / left, +1 above / right.</summary>
        private static float SideOf(Rect r, Vector2 p) =>
            r.width >= r.height ? (p.y < r.center.y ? -1f : 1f) : (p.x < r.center.x ? -1f : 1f);

        /// <summary>Does segment a -> b cross rectangle r (Liang-Barsky).</summary>
        private static bool SegmentHitsRect(Vector2 a, Vector2 b, Rect r)
        {
            Vector2 d = b - a;
            float t0 = 0f, t1 = 1f;
            float[] p = { -d.x, d.x, -d.y, d.y };
            float[] q = { a.x - r.xMin, r.xMax - a.x, a.y - r.yMin, r.yMax - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(p[i]) < 1e-7f) { if (q[i] < 0f) return false; continue; }
                float t = q[i] / p[i];
                if (p[i] < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            return true;
        }

        /// <summary>Exits with `interact` = true -> come close and press T (text `verb`, default "Enter"; range `range`).
        /// Hidden while flags are missing / the area is not cleared (a nearby unlocking mechanism gets the T key), unless `show_locked` = true
        /// (then pressing T shows locked_text).</summary>
        public void RegisterExits()
        {
            InteractionSystem.Ensure();
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Exit || t.props == null || !t.props.GetBool(MapSchema.Prop.Interact, false)) continue;
                var exit = t;
                bool showLocked = t.props.GetBool("show_locked", false);
                var r = t.rect;
                InteractionSystem.Add(new Interactable
                {
                    id = t.name, verb = t.props.GetString(MapSchema.Prop.Verb, "Enter"), rect = r, clue = false,
                    range = t.props.GetFloat(MapSchema.Prop.Range, 0.55f),
                    anchor = new Vector2(r.center.x, Mathf.Min(r.yMax, r.center.y + 0.45f)),
                    available = () => showLocked || ExitOpen(exit),
                    use = () => OnExit(exit),
                });
            }
        }

        /// <summary>Soft blocking strip (e.g. the village exit before reading Todd's contract): flags missing -> push the player out of the strip
        /// back toward where he came from (along the strip's thin side) and show locked_text. With the flags set, pass through normally.</summary>
        private void OnTurnBack(MapTrigger t)
        {
            if (director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, ""))) return;
            var r = t.rect;
            var pl = director.Player;
            Vector2 cur = pl.position;
            // only move along the strip's THIN side, keep the other coordinate; no last-frame position yet (just entered the map) or an unusually thick strip
            // (> 2 tiles) -> no push - avoids flinging the player far away (a wrongly sized strip covering the start point)
            if (!hasLast || Mathf.Min(r.width, r.height) > 0.7f) return;
            Vector2 p = cur;
            const float gap = 0.12f;
            // push back to the remembered SAFE SIDE (using lastPos failed after rolling through: the next frame's lastPos was on the far side -> pushed to the far side)
            float side = t.safeSide != 0f ? t.safeSide : SideOf(r, lastPos);
            if (r.width >= r.height) p.y = side < 0f ? r.yMin - gap : r.yMax + gap;
            else p.x = side < 0f ? r.xMin - gap : r.xMax + gap;
            var rb = pl.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = p; rb.linearVelocity = Vector2.zero; }
            pl.position = new Vector3(p.x, p.y, pl.position.z);
            lastPos = p;                                                   // next frame's movement segment starts from the pushed-back spot
            t.inside = false;
            if (Time.time >= nextTurnBackHint)
            {
                // the character talks to himself (overhead line) instead of a hint line; empty `say` uses locked_text
                string say = t.props.GetString("say", "");
                if (string.IsNullOrEmpty(say)) say = t.props.GetString(MapSchema.Prop.LockedText, "Not yet.");
                SpeechBubble.Show(pl.position + new Vector3(0f, 0.82f, 0f), say, 3.2f).Follow(pl);
                nextTurnBackHint = Time.time + 3.4f;
                Debug.Log($"[TurnBack] {t.name}: missing flags '{t.props.GetString(MapSchema.Prop.Requires, "")}' -> turned back. Current flags: {string.Join(", ", director.Flags.All)}");
            }
        }

        private void OnPortal(MapTrigger t)
        {
            if (!director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, "")))
            {
                Hints.Show(t.props.GetString(MapSchema.Prop.LockedText, "The way is blocked."), 2.5f);
                return;
            }
            var to = t.props.GetString(MapSchema.Prop.Target, "").Split(',');
            if (to.Length == 2 &&
                float.TryParse(to[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float tx) &&
                float.TryParse(to[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ty))
            {
                // fade to black before moving (no camera pan across the map, the destination area is far away like a separate map)
                SceneTransition.Warp(director.Player, director.TileToWorld(tx, ty),
                    t.props.GetString(MapSchema.Prop.HintText, ""), t.props.GetFloat(MapSchema.Prop.Shake, 0f));
            }
        }

        private void OnExit(MapTrigger t)
        {
            string target = t.props.GetString(MapSchema.Prop.Target, "");
            if (!ExitOpen(t))
            {
                Hints.Show(t.props.GetString(MapSchema.Prop.LockedText, "The way is sealed. Defeat the guardian first."), 2.5f);
                return;
            }
            if (!SceneTransition.CanGo(target))
            {
                Hints.Show($"The road ahead is not built yet ({target}).", 3f);
                Debug.LogWarning($"[Map] Scene '{SceneTransition.SceneName(target)}' is not in Build Settings.");
                return;
            }
            Debug.Log($"[Map] Exit '{t.name}' -> {target}");
            if (SceneTransition.Busy) return;
            // arrival spot in the destination map (Tiled tile) - e.g. climbing up from a cellar lands right at the hole, not at the main map's player_start
            var at = t.props.GetString(MapSchema.Prop.ArriveAt, "").Split(',');
            float ax = 0f, ay = 0f;
            bool hasAt = at.Length == 2 &&
                float.TryParse(at[0], NumberStyles.Float, CultureInfo.InvariantCulture, out ax) &&
                float.TryParse(at[1], NumberStyles.Float, CultureInfo.InvariantCulture, out ay);
            GameSession.SetArrival(hasAt, hasAt ? new Vector2(ax, ay) : Vector2.zero);
            // departure text changes by flag - with `depart_when` (flags) set, use `depart_text_when` (e.g. the drill yard: before the test = "DESERTER!", after = a farewell)
            string depart = t.props.GetString(MapSchema.Prop.DepartText, "The stone groans... and gives way beneath you!");
            string departWhen = t.props.GetString("depart_when", "");
            if (!string.IsNullOrEmpty(departWhen) && director.Flags.HasAll(departWhen)) depart = t.props.GetString("depart_text_when", depart);
            SceneTransition.Go(director.Player, target,
                depart,
                SceneManager.GetActiveScene().name,
                t.props.GetBool(MapSchema.Prop.Quake, true),
                t.props.GetString(MapSchema.Prop.ArriveTitle, ""), t.props.GetString(MapSchema.Prop.ArriveSubtitle, ""));
        }

        /// <summary>Exit opens once its area is cleared (e.g. boss defeated) - or the exit is not tied to any area.</summary>
        public bool ExitOpen(MapTrigger t) =>
            (string.IsNullOrEmpty(t.zoneId) || director.IsZoneClearedOrMissing(t.zoneId)) &&
            director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, ""));   // the exit may require flags (e.g. grok_tusk)

        /// <summary>Exits with `collapse = true` -> collapse scene (RingCollapse), then the map change; otherwise wait 1.8 s.</summary>
        private System.Collections.IEnumerator CollapseThenExit(string exitName)
        {
            MapTrigger exit = null;
            foreach (var t in triggers) if (t.type == MapSchema.ObjectType.Exit && t.name == exitName) { exit = t; break; }
            if (exit == null || !exit.props.GetBool("collapse", false)) { yield return ExitAfter(exitName, 1.8f); yield break; }
            yield return new WaitForSeconds(0.6f);                          // time to read the text line + Ren's exclamation
            Vector2 c = new Vector2(float.NaN, 0f);
            var parts = exit.props.GetString("collapse_center", "").Split(',');
            if (parts.Length == 2 &&
                float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cy)) c = new Vector2(cx, cy);
            if (float.IsNaN(c.x) && QuestTracker.Instance != null) c = QuestTracker.Instance.PlayerTile;
            yield return RingCollapse.Play(director, c);
            OnExit(exit);
            if (!SceneTransition.Busy) CutsceneState.End();                  // map change failed: restore the HUD
        }

        /// <summary>Leave the map at once through exit `exitName` (story scene).</summary>
        public void ExitNow(string exitName)
        {
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.Exit && t.name == exitName) { OnExit(t); return; }
            Debug.LogWarning($"[Map] ExitNow: exit '{exitName}' not found");
        }

        private System.Collections.IEnumerator ExitAfter(string exitName, float delay)
        {
            yield return new WaitForSeconds(delay);
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.Exit && t.name == exitName) { OnExit(t); yield break; }
            Debug.LogWarning($"[Map] then_exit: exit '{exitName}' not found");
        }

        /// <summary>An area with an exit was just cleared: tell the player + blinking beacon at the exit.</summary>
        public void RevealExits(string zoneId)
        {
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Exit || t.zoneId != zoneId) continue;
                if (!director.Flags.HasAll(t.props.GetString(MapSchema.Prop.Requires, ""))) continue;   // not shown until the flags are set
                Hints.Show(t.props.GetString(MapSchema.Prop.OpenText, "The altar cracks open. Something hollow lies beneath..."), 4f);
                ExitBeacon.Create(new Vector3(t.rect.center.x, t.rect.center.y, 0f), new Color(0.75f, 1f, 0.45f), 1.1f);
            }
        }
    }
}
