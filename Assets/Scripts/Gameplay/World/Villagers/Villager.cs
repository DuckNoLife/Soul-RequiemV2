using System.Collections.Generic;
using UnityEngine;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// A villager. Wanders inside area `area` with rest stops; or stands still (`walker` = false).
    /// Directional images (PixelLab, same style as Ren): "idle_E/S/N", "walk_E/S/N" (west = flipped east). A missing direction -> an available one is used.
    /// Has a collision body (Kinematic Rigidbody2D + capsule at the feet): the player cannot walk through; moves with MovePosition.
    /// Has lines: come close and press Interact (T) -> turns to the player and shows the lines one by one.
    /// Created by <see cref="VillagerDirector"/> from the Tiled "npcs" layer.
    /// </summary>
    public sealed class Villager : MonoBehaviour
    {
        public Dictionary<string, Sprite[]> anims = new Dictionary<string, Sprite[]>();
        public Rect area;
        public bool walker = true;
        public float speed = 0.55f;
        public float fps = 8f;
        public string[] lines;
        /// <summary>Name shown in the dialogue box.</summary>
        public string speaker = "";
        /// <summary>Initial facing: (1,0) east, (-1,0) west, (0,-1) south, (0,1) north.</summary>
        public Vector2 facing = Vector2.down;
        /// <summary>Original facing (property `face` on the map) - a standing villager turns back to it after talking.</summary>
        private Vector2 homeFacing = Vector2.down;

        private SpriteRenderer sr;
        private Rigidbody2D rb;
        private Vector2 target;
        private float wait, frameT;
        private int frame;
        private bool moving;
        private Interactable talk;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private ContactFilter2D filter;

        private const float Radius = 0.11f;          // world units (1 map tile = 0.32)
        private const float TalkHeight = 0.78f;

        public void Init(SpriteRenderer reference)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            if (reference != null)
            {
                sr.sortingLayerID = reference.sortingLayerID;
                sr.sortingOrder = reference.sortingOrder;
            }
            sr.spriteSortPoint = SpriteSortPoint.Pivot;

            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = gameObject.AddComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Horizontal;
            float s = Mathf.Max(0.01f, transform.lossyScale.x);
            col.size = new Vector2(0.30f, 0.14f) / s;              // ~ foot width, in world units
            col.offset = new Vector2(0f, 0.05f) / s;

            filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;
            frame = Random.Range(0, 8);
            homeFacing = facing;
            wait = Random.Range(0.3f, 2.5f);
            target = transform.position;
            Apply();

            if (lines != null && lines.Length > 0)
            {
                talk = InteractionSystem.Add(new Interactable
                {
                    id = "npc_" + name, verb = "Talk", clue = false, range = 0.35f,
                    rect = Box(), anchor = (Vector2)transform.position + new Vector2(0f, TalkHeight),
                    available = () => isActiveAndEnabled,
                    use = Say
                });
            }
        }

        private Rect Box() => new Rect((Vector2)transform.position - new Vector2(0.18f, 0.02f), new Vector2(0.36f, 0.62f));

        private void Say()
        {
            if (lines == null || lines.Length == 0 || DialogueBox.Active) return;
            // dialogue box (name + typewriter, [T] next line) instead of a faint line at the bottom; both face each other until it closes
            wait = 9999f;                                       // stand still for the whole conversation
            moving = false;
            var p = FindFirstObjectByType<PlayerController>();
            PlayerCombat pc = null;
            if (p != null)
            {
                Vector2 d = (Vector2)p.transform.position - (Vector2)transform.position;
                if (d.sqrMagnitude > 0.0001f) facing = d.normalized;
                pc = p.GetComponent<PlayerCombat>();
                if (pc != null) pc.LookAt(-d, 600f);
            }
            DialogueBox.Open(speaker, lines, () =>
            {
                wait = Random.Range(1f, 2.5f);
                if (!walker) facing = homeFacing;               // after talking, turn back to the original facing (e.g. S) instead of staring sideways forever
                if (pc != null) pc.StopLook();
            });
        }

        private void Update()
        {
            Animate(Time.deltaTime);
            if (talk != null)
            {
                talk.rect = Box();
                talk.anchor = (Vector2)transform.position + new Vector2(0f, TalkHeight);
                if (talk.glow != null) talk.glow.transform.position = transform.position + new Vector3(0f, 0.3f, 0f);
            }
        }

        // ---- scripted walking (cutscenes): WalkTo -> stop on arrival (or when blocked for over 2 seconds); EndScript -> wander again
        private bool scripted;
        private Vector2 scriptTarget;
        private float stuckFor;
        public bool Scripted => scripted;
        public bool Arrived { get; private set; }

        public void WalkTo(Vector2 p)
        {
            scripted = true; Arrived = false; stuckFor = 0f;
            scriptTarget = p; wait = 0f;
        }

        /// <summary>Place instantly at `p` (e.g. stepping out of a house door / entering from the village edge).</summary>
        public void Warp(Vector2 p)
        {
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            if (rb != null) rb.position = p;
        }

        public void FaceTo(Vector2 worldPoint)
        {
            Vector2 d = worldPoint - (Vector2)transform.position;
            if (d.sqrMagnitude > 0.0001f) facing = d.normalized;
            moving = false;
        }

        public void EndScript()
        {
            scripted = false; moving = false;
            target = transform.position; wait = Random.Range(1f, 2.5f);
        }

        private void ScriptStep(float dt)
        {
            if (Arrived) { moving = false; return; }
            Vector2 p = rb.position, d = scriptTarget - p;
            float dist = d.magnitude;
            if (dist < 0.03f) { Arrived = true; moving = false; return; }
            Vector2 dir = d / dist;
            float step = Mathf.Min(dist, speed * dt);
            if (Blocked(p, dir, step + 0.04f))
            {
                moving = false; stuckFor += dt;
                if (stuckFor > 2f) Arrived = true;                // blocked (e.g. the player is standing there) -> counts as arrived
                return;
            }
            stuckFor = 0f;
            rb.MovePosition(p + dir * step);
            moving = true;
            facing = dir;
        }

        private void FixedUpdate()
        {
            if (rb == null) return;
            if (scripted) ScriptStep(Time.fixedDeltaTime);
            else if (walker) Wander(Time.fixedDeltaTime);
        }

        private void Wander(float dt)
        {
            if (wait > 0f) { wait -= dt; moving = false; if (wait <= 0f) PickTarget(); return; }
            Vector2 p = rb.position, d = target - p;
            float dist = d.magnitude;
            if (dist < 0.03f) { moving = false; wait = Random.Range(1.5f, 4.5f); return; }
            Vector2 dir = d / dist;
            float step = Mathf.Min(dist, speed * dt);
            if (Blocked(p, dir, step + 0.04f)) { moving = false; wait = Random.Range(0.4f, 1.2f); return; }
            rb.MovePosition(p + dir * step);
            moving = true;
            facing = dir;
        }

        private bool Blocked(Vector2 from, Vector2 dir, float len)
        {
            int n = Physics2D.CircleCast(from, Radius, dir, filter, hits, len);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.isTrigger || c.attachedRigidbody == rb) continue;   // ignore its own body
                // scripted walks do NOT treat the player / other villagers as blockers - otherwise Ren standing at the cell door blocked
                // Pell, who stood still for 2 seconds and then "arrived": Pell never walked during the whole scene (no walk animation).
                if (scripted && (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<Villager>() != null)) continue;
                // already OVERLAPPING a collider (e.g. Todd stepping out at the doorstep, feet touching the house wall) -> CircleCast reports a hit at
                // distance 0 even while moving AWAY -> stuck, and the whole talk happened with Todd still at the door. Only count it as blocked when moving INTO the object.
                if (hits[i].distance <= 0f)
                {
                    Vector2 away = from - c.ClosestPoint(from);
                    if (away.sqrMagnitude < 1e-6f || Vector2.Dot(away, dir) > 0f) continue;
                }
                return true;                                                         // walls, trees, water, the player, other villagers
            }
            return false;
        }

        private void PickTarget()
        {
            for (int k = 0; k < 10; k++)
            {
                var t = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
                var c = Physics2D.OverlapCircle(t, Radius + 0.04f);
                if (c != null && !c.isTrigger && c.attachedRigidbody != rb) continue;
                target = t;
                return;
            }
            target = transform.position;
        }

        /// <summary>Direction -> image suffix + flip.</summary>
        private string Dir(out bool flip)
        {
            flip = false;
            if (Mathf.Abs(facing.x) >= Mathf.Abs(facing.y) * 0.8f) { flip = facing.x < 0f; return "E"; }
            return facing.y > 0f ? "N" : "S";
        }

        /// <summary>Directional image set; a missing direction falls back to another (used = direction actually used).</summary>
        private Sprite[] Set(string anim, string dir, out string used)
        {
            used = dir;
            if (anims.TryGetValue(anim + "_" + dir, out var s) && s.Length > 0) return s;
            foreach (var d in new[] { "E", "S", "N" })
                if (anims.TryGetValue(anim + "_" + d, out s) && s.Length > 0) { used = d; return s; }
            return null;
        }

        // ---- ONE-SHOT actions in cutscenes (e.g. the hooded figure's "strike" at the end of Map 3).
        // Plays the whole "<anim>_<E/S/N>" set for the current facing, then back to idle; hold = keep the last frame until ClearPose().
        private Sprite[] oneShot;
        private bool oneShotFlip, oneShotHold;
        private int oneShotFrame;
        private float oneShotT;

        /// <summary>Play action <paramref name="anim"/> once. Returns the duration (seconds); 0 if the images are missing.</summary>
        public float Play(string anim, bool hold, float playFps = 0f)
        {
            string dir = Dir(out bool flip);
            var set = Set(anim, dir, out string used);
            if (set == null) { Debug.LogWarning($"[Villager] {name}: missing images '{anim}'"); return 0f; }
            if (used == "E" && dir != "E") flip = facing.x < 0f;
            oneShot = set; oneShotFlip = flip; oneShotHold = hold;
            oneShotFrame = 0; oneShotT = 0f; moving = false;
            oneShotFps = playFps > 0f ? playFps : fps;
            Apply();
            return set.Length / oneShotFps;
        }
        private float oneShotFps = 8f;

        /// <summary>Stop holding the last frame of a one-shot action.</summary>
        public void ClearPose() { oneShot = null; }

        private void Apply()
        {
            if (oneShot != null)
            {
                sr.flipX = oneShotFlip;
                sr.sprite = oneShot[Mathf.Min(oneShotFrame, oneShot.Length - 1)];
                return;
            }
            string dir = Dir(out bool flip);
            string used = dir;
            var set = (moving ? Set("walk", dir, out used) : null) ?? Set("idle", dir, out used);
            if (set == null) return;
            if (used == "E" && dir != "E") flip = facing.x < 0f;   // no vertical images -> walk sideways toward the current facing side
            sr.flipX = flip;
            sr.sprite = set[frame % set.Length];
        }

        private void Animate(float dt)
        {
            if (oneShot != null)
            {
                oneShotT += dt;
                if (oneShotT >= 1f / oneShotFps)
                {
                    oneShotT = 0f;
                    if (oneShotFrame < oneShot.Length - 1) oneShotFrame++;
                    else if (!oneShotHold) oneShot = null;
                }
            }
            frameT += dt;
            if (frameT >= 1f / fps) { frameT = 0f; frame++; }
            Apply();
        }

        private void OnDisable() { moving = false; }
    }
}
