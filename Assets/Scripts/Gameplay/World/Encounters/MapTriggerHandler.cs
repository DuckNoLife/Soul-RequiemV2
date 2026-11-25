using System.Collections.Generic;
using System.Globalization;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// The map's "triggers" LAYER - each Tiled object has a `type` (MapSchema.ObjectType) and is handled when Ren walks in (OnEnter) or presses T.
    ///   MapTriggerHandler.cs           setup, Tick (entering / leaving triggers), rest points (campfires), trials, start point, restoring from flags
    ///   MapTriggerHandler.Signs.cs     readable gravestones / signs
    ///   MapTriggerHandler.Pickups.cs   pickups (OnPickup: one branch per `item`)
    ///   MapTriggerHandler.Exits.cs     exits, teleports, turn_back strips
    /// Adding a new trigger type: a constant in MapSchema.ObjectType + one branch in OnEnter.
    /// </summary>
    internal sealed partial class MapTriggerHandler
    {
        private readonly MapEncounterDirector director;
        private readonly List<MapTrigger> triggers;

        // Signs shown automatically on entering (map opening narration, trap tips) - the rest need T up close.
        // + walk_hint (Map 2b / Map 4), roll_hint (Map 4) - trap crossing tips that would otherwise need a T "read" in empty space (nothing drawn there).
        private static readonly HashSet<string> AutoSignNames = new HashSet<string> { "r1_hatch", "r2_spout_hint", "stair_sign", "walk_hint", "roll_hint" };

        // pickups (flask, flask upgrade, blue potion) get a warm blinking glow so they are not invisible; it goes out on pickup
        private readonly Dictionary<MapTrigger, ExitBeacon> pickupGlow = new Dictionary<MapTrigger, ExitBeacon>();
        private readonly HashSet<MapTrigger> containerPickups = new HashSet<MapTrigger>();   // items INSIDE a chest (not drawn in advance)
        private HashSet<string> openFlags;                                                  // chest-opening flag (the when of show/hide_objects)
        private float fullHintAt;                                                           // "flask full" reminder kept sparse
        // the rest point's own marker (rune circle + blue soul flame), lit on entering
        private readonly Dictionary<MapTrigger, ShrineMarker> shrines = new Dictionary<MapTrigger, ShrineMarker>();
        private Vector2 lastPos;                 // position last frame (turn_back strips push back toward where the player came from)
        private bool hasLast;
        private float nextTurnBackHint;

        public MapTriggerHandler(MapEncounterDirector director, List<MapTrigger> triggers)
        {
            this.director = director;
            this.triggers = triggers;
            var psr = director.Player != null ? director.Player.GetComponent<SpriteRenderer>() : null;
            ShrineMarker.Noun = "shrine";
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.Checkpoint)
                {
                    string n = t.props.GetString("rest_noun", "");
                    if (!string.IsNullOrEmpty(n)) ShrineMarker.Noun = n;
                }
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.Checkpoint)
                {
                    shrines[t] = ShrineMarker.Create(t.rect, director.mapRoot, psr != null ? psr.sortingLayerID : 0);
                    // No light rings / sparkles mark respawn points.
                    // Rest points without a real fire / candle in the map -> HIDDEN respawn points: no light, no [T] Rest prompt; only recorded when passing through.
                    if (shrines[t] != null && !shrines[t].TryFlameBounds(out _))
                    {
                        UnityEngine.Object.Destroy(shrines[t].gameObject);
                        shrines[t] = null;
                        silent.Add(t);
                    }
                    if (ShowCheckpointMarkers && t.props.GetBool("marker", false))
                    {
                        // the rest area is enlarged for easier pressing -> its centre is off the candles; the ring goes under the real candle cluster
                        Vector2 foot = new Vector2(t.rect.center.x, t.rect.center.y - 0.12f);
                        float top = t.rect.center.y + 0.35f;
                        if (shrines[t] != null && shrines[t].TryFlameBounds(out var fb)) { foot = new Vector2(fb.center.x, fb.min.y + 0.05f); top = fb.max.y + 0.05f; }
                        var m = NoteMarker.CreateAt(foot, top, t.name);
                        var pulse = m.AddComponent<MarkerPulse>();
                        string lit = "lit_" + t.name;
                        var fl = director.Flags;
                        pulse.hideWhen = () => fl != null && fl.Has(lit);
                        m.SetActive(true);
                    }
                }
            openFlags = null; containerPickups.Clear();
            foreach (var t in triggers)                                  // key / key item definitions for the inventory
                if (t.type == MapSchema.ObjectType.Pickup && t.props.GetString(MapSchema.Prop.Item, "") == "key") KeyRing.Register(t.props);
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Pickup) continue;
                string item = t.props.GetString(MapSchema.Prop.Item, "potion");
                if (item != "potion" && item != "flask_upgrade" && item != "green_tonic" && item != "skill_scroll") continue;
                var glow = ExitBeacon.Create(new Vector3(t.rect.center.x, t.rect.center.y, 0f), new Color(1f, 0.82f, 0.45f), 0.55f);
                glow.name = "Pickup Glow " + t.name;
                pickupGlow[t] = glow;
                // pickups without an image in Tiled (no `hide`) -> draw the item image (a glow alone showed no flask)
                // items LYING IN A CHEST (their `flag` opens the chest through show/hide_objects,
                // e.g. z2b_flask -> z2b_chest_toggle) are NOT drawn in advance; on pickup the item pops out of the chest into Ren (PickupPop). Force it with `show_item` = true / false.
                bool inChest = OpensContainer(t);
                if (inChest) containerPickups.Add(t);
                if (string.IsNullOrEmpty(t.props.GetString(MapSchema.Prop.Hide, "")) && t.props.GetBool("show_item", !inChest))
                {
                    var bodySr = director.Player != null ? director.Player.GetComponentInChildren<SpriteRenderer>() : null;
                    PickupItemVisual.Attach(glow.transform, new Vector2(t.rect.center.x, t.rect.center.y - 0.08f), item,
                                            bodySr != null ? bodySr.sortingLayerID : 0, bodySr != null ? bodySr.sortingOrder : 2);
                }
            }
            // important pickups (property marker = true, e.g. Malric's crank): glow ring + sparkle under the item image
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Pickup || !t.props.GetBool("marker", false)) continue;
                Vector2 foot = t.rect.center; float top = foot.y + 0.3f;
                string hide = t.props.GetString(MapSchema.Prop.Hide, "");
                if (!string.IsNullOrEmpty(hide) && director.mapRoot != null)
                    foreach (var so in director.mapRoot.GetComponentsInChildren<SuperTiled2Unity.SuperObject>(true))
                    {
                        if (so.m_TiledName != hide) continue;
                        var r = so.GetComponentInChildren<SpriteRenderer>(true);
                        if (r != null) { foot = new Vector2(r.bounds.center.x, r.bounds.min.y); top = r.bounds.max.y; }
                        break;
                    }
                var m = NoteMarker.CreateAt(foot, top, t.name);
                m.AddComponent<MarkerPulse>();
                m.SetActive(true);
                pickupMarker[t] = m;
            }
        }

        private readonly Dictionary<MapTrigger, GameObject> pickupMarker = new Dictionary<MapTrigger, GameObject>();
        /// <summary>Hidden rest point (no real fire / candle) - only an automatic respawn point.</summary>
        private readonly HashSet<MapTrigger> silent = new HashSet<MapTrigger>();
        /// <summary>Turn off every "save point" marker (light ring, sparkle, light-the-fire prompt). true turns them back on.</summary>
        private static readonly bool ShowCheckpointMarkers = false;

        public IReadOnlyList<MapTrigger> All => triggers;

        /// <summary>After loading a save: picked-up items (flags picked_*) do not reappear, lit campfires (lit_*) are already blue.</summary>
        public void RestoreFromFlags()
        {
            var flags = director.Flags;
            foreach (var t in triggers)
            {
                if (t.type == MapSchema.ObjectType.Pickup && flags.Has("picked_" + t.name))
                {
                    t.fired = true;
                    if (pickupGlow.TryGetValue(t, out var g) && g != null) UnityEngine.Object.Destroy(g.gameObject);
                    if (pickupMarker.TryGetValue(t, out var pm) && pm != null) UnityEngine.Object.Destroy(pm);
                    string hide = t.props.GetString(MapSchema.Prop.Hide, "");
                    if (!string.IsNullOrEmpty(hide)) HideMapObject(hide);
                    // old save (key picked up before the inventory existed) -> put the key in the inventory
                    if (t.props.GetString(MapSchema.Prop.Item, "") == "key")
                    {
                        string kid = KeyRing.Register(t.props);
                        if (kid.Length > 0 && !KeyRing.Has(flags, kid)) flags.Set(KeyRing.Flag(kid));
                    }
                }
                else if (t.type == MapSchema.ObjectType.Checkpoint && flags.Has("lit_" + t.name) && shrines.TryGetValue(t, out var sh) && sh != null)
                    sh.SetLit();
            }
        }

        /// <summary>Respawn point of trial `stage` (checkpoint with the stage property, lit) - bottom middle edge of the rest area
        /// (in front of the altar, clear of the candles). False if the trial has no point or it is not lit.</summary>
        public bool TryStageSpawn(string stage, out Vector2 pos)
        {
            pos = default;
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Checkpoint || t.props == null) continue;
                if (!HasStage(t, stage)) continue;
                if (!director.Flags.Has("lit_" + t.name)) continue;
                pos = new Vector2(t.rect.center.x, t.rect.yMin + 0.1f);
                return true;
            }
            return false;
        }

        /// <summary>Ren enters an area -> every rest point of that area (matching zone_id, or centre inside the area box) counts as lit:
        /// flag lit_&lt;name&gt; (the minimap fire icon turns blue at once), the real fire / candles flare up. No need to step right into the rest area.</summary>
        public void LightCheckpointsIn(string zoneId, Rect zoneRect)
        {
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Checkpoint) continue;
                if (t.zoneId != zoneId && !zoneRect.Contains(t.rect.center)) continue;
                string f = "lit_" + t.name;
                if (director.Flags.Has(f)) continue;
                director.Flags.Set(f);
                if (shrines.TryGetValue(t, out var sh) && sh != null) sh.Kindle();
            }
        }

        /// <summary>The checkpoint is the respawn point of trial `stage` - the stage property may be a list "a,b" (shared).</summary>
        private static bool HasStage(MapTrigger t, string stage)
        {
            if (t.props == null || string.IsNullOrEmpty(stage)) return false;
            foreach (var s in t.props.GetString(MapSchema.Prop.Stage, "").Split(','))
                if (s.Trim() == stage) return true;
            return false;
        }

        /// <summary>Trial name (stage_name on the trial's checkpoint; otherwise the trial code in upper case).
        /// Checkpoint shared by several trials: stage_name = "a=Name A;b=Name B".</summary>
        public string StageName(string stage)
        {
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.Checkpoint && HasStage(t, stage))
                {
                    string n = t.props.GetString(MapSchema.Prop.StageName, "");
                    if (string.IsNullOrEmpty(n)) continue;
                    if (n.IndexOf('=') < 0) return n;
                    foreach (var part in n.Split(';'))
                    {
                        int eq = part.IndexOf('=');
                        if (eq > 0 && part.Substring(0, eq).Trim() == stage) return part.Substring(eq + 1).Trim();
                    }
                }
            // trial without its own campfire (respawn on the spot of the previous trial) -> its name is listed in another checkpoint's stage_name ("a=Name;b=Name")
            foreach (var t in triggers)
            {
                if (t.type != MapSchema.ObjectType.Checkpoint || t.props == null) continue;
                foreach (var part in t.props.GetString(MapSchema.Prop.StageName, "").Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq > 0 && part.Substring(0, eq).Trim() == stage) return part.Substring(eq + 1).Trim();
                }
            }
            return string.IsNullOrEmpty(stage) ? "Trial" : char.ToUpperInvariant(stage[0]) + stage.Substring(1);
        }

        /// <summary>The player is standing inside the checkpoint area.</summary>
        public bool PlayerAtCheckpoint
        {
            get
            {
                // standing right at the candle cluster (area edge) made [T] read the sign next to it -> rest area enlarged by 0.25 units
                Vector2 p = director.Player != null ? (Vector2)director.Player.position : new Vector2(float.NaN, float.NaN);
                foreach (var t in triggers)
                {
                    if (t.type != MapSchema.ObjectType.Checkpoint || silent.Contains(t)) continue;   // hidden points have no Rest prompt
                    if (t.inside) return true;
                    var r = t.rect;
                    if (p.x >= r.xMin - 0.25f && p.x <= r.xMax + 0.25f && p.y >= r.yMin - 0.25f && p.y <= r.yMax + 0.25f) return true;
                }
                return false;
            }
        }

        /// <summary>Position of player_start (if the map has one).</summary>
        public bool TryGetPlayerStart(out Vector2 pos)
        {
            foreach (var t in triggers)
                if (t.type == MapSchema.ObjectType.PlayerStart) { pos = t.rect.center; return true; }
            pos = default;
            return false;
        }

        /// <summary>Called every frame: detects the player entering a trigger.</summary>
        public void Tick(Vector2 playerPos)
        {
            foreach (var t in triggers)
            {
                bool inside = t.rect.Contains(playerPos);
                // thin turn_back strip - a roll covers more than the strip thickness in one frame, so no
                // frame is ever "inside" the strip. Check the whole movement segment from last frame: crossing the strip = entering it.
                // (steps > 1 unit are ignored: teleports / respawns / cutscenes are not crossings)
                if (!inside && hasLast && t.type == MapSchema.ObjectType.TurnBack && (playerPos - lastPos).sqrMagnitude < 1f
                    && SegmentHitsRect(lastPos, playerPos, t.rect)) inside = true;
                if (!inside && t.type == MapSchema.ObjectType.TurnBack) t.safeSide = SideOf(t.rect, playerPos);   // remember the safe side
                if (inside && !t.inside) OnEnter(t);
                t.inside = inside;
            }
            // REAL position after handling (blocking strips / teleports may have just moved the player) - not playerPos from the start of the frame
            lastPos = director.Player != null ? (Vector2)director.Player.position : playerPos; hasLast = true;
        }

        // ------------------------------------------------------------------ entering a trigger
        private void OnEnter(MapTrigger t)
        {
            switch (t.type)
            {
                case MapSchema.ObjectType.Checkpoint:
                    // the rest area surrounds the campfire (enter from any side) -> respawn where the player just stood (surely walkable),
                    // not at the area centre (the campfire itself has a collider)
                    director.SetRespawnPoint(director.Player != null ? (Vector2)director.Player.position : t.rect.center);
                    director.AddAutoRespawn(director.Player != null ? (Vector2)director.Player.position : t.rect.center);
                    if (shrines.TryGetValue(t, out var shrine) && shrine != null) shrine.Kindle();
                    director.Flags.Set("lit_" + t.name);
                    director.SaveProgress();                          // lighting a campfire = saving the game
                    string noun = t.props.GetString("rest_noun", "shrine"); if (string.IsNullOrEmpty(noun)) noun = "shrine";
                    ShrineMarker.RestLabel = "Rest at the " + noun;
                    // only ONE prompt: the HUD's [T] Rest at the campfire box. First time: that box gets an explanation line (ShrineMarker.Notice), no Hints.Show
                    if (ShowCheckpointMarkers && !director.Flags.HasAll("tut_shrine"))   // no "lit - you wake at the last one you lit" line (respawn is automatic, nearest to where he fell)
                    {
                        director.Flags.Set("tut_shrine");
                        ShrineMarker.Notice = char.ToUpperInvariant(noun[0]) + noun.Substring(1) + " lit - if you fall, you wake at the last one you lit. Resting restores health and flasks.";
                        ShrineMarker.NoticeUntil = Time.time + 7f;
                    }
                    break;
                case MapSchema.ObjectType.Sign:
                    // only control tips / map opening narration show automatically; gravestones, signs, corpses... need T up close (RegisterSigns)
                    // auto narration shows only ONCE (respawning at the start point would otherwise replay the falling-into-the-crypt line)
                    if (AutoSign(t) && !director.Flags.Has("seen_" + t.name))
                    {
                        director.Flags.Set("seen_" + t.name);
                        Hints.Show(t.props.GetString(MapSchema.Prop.HintText, ""), 4f);
                    }
                    break;
                case MapSchema.ObjectType.Pickup:
                    // NO pickup on walking in - come close and press T (RegisterPickups). `auto` = true keeps the walk-in style if needed.
                    if (t.props.GetBool("auto", false)) OnPickup(t);
                    break;
                case MapSchema.ObjectType.Portal:
                    OnPortal(t);
                    break;
                case MapSchema.ObjectType.Exit:
                    // `interact` exits do not trigger on walking in (stepping into a hole by accident; already standing in the area before pulling
                    // the rope gives no new "enter" -> you would have to step out and back in). T is handled through RegisterExits.
                    if (!t.props.GetBool(MapSchema.Prop.Interact, false)) OnExit(t);
                    break;
                case MapSchema.ObjectType.TurnBack:
                    OnTurnBack(t);
                    break;
            }
        }
    }
}
