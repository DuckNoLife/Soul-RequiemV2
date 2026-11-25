using System.Collections;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using SoulRequiem.Core;

namespace SoulRequiem.Gameplay
{
    /// <summary>
    /// After driving off the raiders (flag zf_cleared): the farmhouse door swings open, old Todd (village elder) steps out, walks to Ren,
    /// thanks him and tells the story (speech), then points to the contract on the notice board. Then flag todd_greeted turns on and Todd returns to his spot.
    /// Tiled data: point "aftermath_door" (triggers layer, type cutscene_point) = doorstep; prop "farm_door_open" = open door image;
    /// NPC Todd (npcs layer, name "todd") has show_when = todd_door (the scene sets this flag after placing Todd at the doorstep -> no sudden pop-in).
    /// Dialogue: properties line1..lineN on the aftermath_door point. Space / Enter: fast-forward.
    /// </summary>
    public class RaidAftermath : MonoBehaviour
    {
        private bool skip;

        private IEnumerator Start()
        {
            yield return null;
            var dir = MapEncounterDirector.Instance;
            if (dir == null || dir.Flags == null || dir.Flags.HasAll("todd_greeted")) yield break;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
                if (so.m_TiledName == "farm_door_open") so.gameObject.SetActive(false);   // door closed until Todd comes out
            while (!dir.Flags.HasAll("zf_cleared")) yield return new WaitForSeconds(0.25f);
            yield return new WaitForSeconds(1.6f);                       // fires out, smoke thins

            SuperObject doorPt = null, doorOpen = null;
            foreach (var so in dir.mapRoot.GetComponentsInChildren<SuperObject>(true))
            {
                if (so.m_TiledName == "aftermath_door") doorPt = so;
                else if (so.m_TiledName == "farm_door_open") doorOpen = so;
            }
            Villager todd = null;
            foreach (var v in FindObjectsByType<Villager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v.name == "NPC todd") { todd = v; break; }
            if (doorPt == null || todd == null || dir.Player == null) { dir.Flags.Set("todd_door"); dir.Flags.Set("todd_greeted"); yield break; }

            var props = doorPt.GetComponent<SuperCustomProperties>();
            Vector3 door = doorPt.transform.position;
            Vector3 home = todd.transform.position;                      // Todd's usual spot (position in Tiled)

            // only plays while Ren is near Todd's house (area cleared with a test key / walked far away -> wait for Ren to come back)
            while (dir.Player != null && ((Vector2)(dir.Player.position - door)).magnitude > 4.5f) yield return new WaitForSeconds(0.25f);

            // door swings open, Todd steps out
            if (doorOpen != null) doorOpen.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            todd.transform.position = door;
            dir.Flags.Set("todd_door");                                  // VillagerDirector shows Todd at the doorstep
            yield return null;
            StartCoroutine(HoldInput(dir));
            CutsceneState.Begin();                                       // HUD fades, does not cover the speech

            var player = dir.Player;
            // Todd stands LEVEL with Ren (on the door side) so the two face each other horizontally -
            // standing diagonally above made Todd face down (S) while Ren kept his old facing
            float side = door.x >= player.position.x ? 1f : -1f;
            Vector2 stand = (Vector2)player.position + new Vector2(side * 0.46f, 0f);
            var combat = player.GetComponent<PlayerCombat>();
            if (combat != null) combat.FacingOverride = new Vector2(side, 0f);   // Ren turns toward the approaching Todd
            todd.WalkTo(stand);
            float give = Time.time + 7f;
            while (!todd.Arrived && Time.time < give && !skip) yield return null;
            // path blocked -> place Todd next to Ren, so they do not talk from across the yard
            float miss = ((Vector2)todd.transform.position - stand).magnitude;
            if (miss > 0.35f && miss < 3f) todd.Warp(stand);
            if (doorOpen != null) doorOpen.gameObject.SetActive(false); // door closes behind him
            todd.FaceTo(player.position);

            // camera pans to between Todd and Ren (nudged up so the speech sits mid-screen) - following Ren left Todd's
            // speech at the top edge, covered by the HP bar / minimap
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            float oldSharp = cam != null ? cam.focusSharpness : 6f;
            if (cam != null)
            {
                cam.focusSharpness = 2.5f;
                cam.Focus((todd.transform.position + player.position) * 0.5f + new Vector3(0f, 0.35f, 0f), Camera.main.orthographicSize);
            }

            // long lines -> dialogue box at the bottom of the screen (name "Todd", typewriter, [T] next line) instead of an overhead bubble that cut off text
            var lines = new System.Collections.Generic.List<string>();
            for (int i = 1; i <= 8; i++)
            {
                string line = props != null ? props.GetString("line" + i, "") : "";
                if (string.IsNullOrEmpty(line)) break;
                lines.Add(line);
            }
            if (lines.Count > 0)
            {
                DialogueBox.Open(todd.speaker.Length > 0 ? todd.speaker : "Todd", lines.ToArray());
                while (DialogueBox.Active)
                {
                    todd.FaceTo(player.position);
                    if (combat != null) combat.FacingOverride = (Vector2)(todd.transform.position - player.position);
                    yield return null;
                }
            }

            if (cam != null) { cam.Release(); cam.focusSharpness = 3f; }
            if (combat != null) combat.FacingOverride = Vector2.zero;
            dir.Flags.Set("todd_greeted");                              // HoldInput releases control
            Hints.Show(props != null ? props.GetString("after_hint", "") : "", 4.5f);
            yield return new WaitForSeconds(0.8f);
            if (cam != null) cam.focusSharpness = oldSharp;
            todd.WalkTo(home);
            give = Time.time + 12f;
            while (!todd.Arrived && Time.time < give) yield return null;
            todd.EndScript();
        }

        private IEnumerator HoldInput(MapEncounterDirector dir)
        {
            while (!dir.Flags.HasAll("todd_greeted"))
            {
                GameInput.Blocked = true;
                // story scenes CANNOT be skipped (only the boss intro card can)
                yield return null;
            }
            GameInput.Blocked = false;
            CutsceneState.End();
        }
    }
}
