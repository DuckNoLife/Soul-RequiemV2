# 17/11 (§294): gắn âm thanh (SoulRequiem.Core.Sfx / SfxHooks) vào các hệ sẵn có — mỗi chỗ chèn một dòng.
# python3 patch_sfx_hooks.py <thư mục Assets/Scripts> <thư mục sao lưu>
# Mỗi mẫu tìm phải khớp ĐÚNG 1 lần (trong phạm vi chỉ định), giữ kiểu xuống dòng của file (CRLF/LF).
# File đã có dấu "§294" thì bỏ qua (chạy lại an toàn).
import sys, os, shutil

ROOT, BAK = sys.argv[1], sys.argv[2]
CHECK = BAK == '--check'   # chỉ kiểm, không ghi
S = 'SoulRequiem.Core.Sfx'
M = '   // 17/11 (§294): âm'

# (file, [(sau_dấu_mốc hoặc None, chuỗi_cũ, chuỗi_mới)])  — chuỗi dùng \n, tự đổi sang CRLF nếu file dùng CRLF
PATCHES = {
 'Gameplay/Combat/Health.cs': [
  (None, "                Blocked?.Invoke(knockbackDir);\n",
         "                Blocked?.Invoke(knockbackDir);\n                SfxHooks.Blocked(this);" + M + "\n"),
  (None, "                Parried?.Invoke(amount, knockbackDir);\n",
         "                Parried?.Invoke(amount, knockbackDir);\n                SfxHooks.Parried(this);" + M + "\n"),
  (None, "            if (!IsDead) Hit?.Invoke(this, amount, knockbackDir, knockbackForce);\n",
         "            SfxHooks.Hit(this);" + M + "\n            if (!IsDead) Hit?.Invoke(this, amount, knockbackDir, knockbackForce);\n"),
  (None, "            if (Current <= 0f) Died?.Invoke(this);\n",
         "            if (Current <= 0f) { SfxHooks.Died(this); Died?.Invoke(this); }" + M + "\n"),
 ],
 'Gameplay/Combat/SpriteVfx.cs': [
  ("public static SpriteVfx Spawn(string name", "            if (f.Length == 0) return null;\n",
   "            if (f.Length == 0) return null;\n            SfxHooks.Vfx(name, pos);" + M + "\n"),
 ],
 'Gameplay/Combat/Projectile.cs': [
  ("public void Launch(", "            damage = dmg;\n",
   "            damage = dmg;\n            SfxHooks.Launched(this);" + M + "\n"),
 ],
 'Gameplay/Player/Abilities/PlayerMelee.cs': [
  ("private IEnumerator Strike(", "            if (!actions.Alive(id)) { Hide(id); yield break; }",
   "            " + S + ".Play(last ? \"pl_swing_heavy\" : \"pl_swing\", owner.transform.position);" + M + "\n            if (!actions.Alive(id)) { Hide(id); yield break; }"),
 ],
 'Gameplay/Player/Abilities/PlayerDodge.cs': [
  (None, "            anim.Dash(facing);\n",
   "            anim.Dash(facing);\n            " + S + ".Play(\"pl_dodge\", owner.transform.position);" + M + "\n"),
 ],
 'Gameplay/Player/Abilities/PlayerFlask.cs': [
  (None, "            anim.Drink();\n",
   "            anim.Drink();\n            " + S + ".Play(\"flask_drink\", owner.transform.position);" + M + "\n"),
 ],
 'Gameplay/Player/Weapons/IaiStance.cs': [
  (None, "        private void LateUpdate()\n",
   "        private bool wasHolding;" + M + ": tụ lực Iaijutsu\n\n        private void LateUpdate()\n"),
  (None, "            bool holding = combat.IaiHolding;\n",
   "            bool holding = combat.IaiHolding;\n            if (holding && !wasHolding) " + S + ".PlayAt(\"iai_charge\", transform);" + M + "\n            wasHolding = holding;\n"),
  (None, "            if (holding && full && !wasFull) StartCoroutine(Star(o + new Vector3(0f, h, 0f), 1f));\n",
   "            if (holding && full && !wasFull) { StartCoroutine(Star(o + new Vector3(0f, h, 0f), 1f)); " + S + ".PlayAt(\"iai_ready\", transform); }" + M + "\n"),
  (None, "            else if (holding && past && !wasMark) StartCoroutine(Star(o + new Vector3(0f, px + inner * m, 0f), 0.55f));\n",
   "            else if (holding && past && !wasMark) { StartCoroutine(Star(o + new Vector3(0f, px + inner * m, 0f), 0.55f)); " + S + ".PlayAt(\"iai_ready\", transform, 0.45f); }" + M + "\n"),
  ("public void Release(Vector2 dir, float power)", "            alpha = 0f; shown = 0f;\n",
   "            alpha = 0f; shown = 0f;\n            " + S + ".PlayAt(\"iai_release\", transform);" + M + "\n"),
 ],
 'Gameplay/Player/PlayerController.cs': [
  (None, "        private void FixedUpdate()\n",
   "        private float stepIn;" + M + ": tiếng bước chân\n        private const float StepEvery = 0.34f;\n\n        private void FixedUpdate()\n"),
  (None, "            rb.MovePosition(rb.position + lastStep);\n",
   "            rb.MovePosition(rb.position + lastStep);\n"
   "            if (move.sqrMagnitude > 0.04f && Time.timeScale > 0f)" + M + "\n"
   "            {\n"
   "                stepIn -= Time.fixedDeltaTime;\n"
   "                if (stepIn <= 0f) { stepIn = StepEvery; " + S + ".Play(\"footstep\", rb.position); }\n"
   "            }\n"
   "            else stepIn = 0.08f;\n"),
 ],
 'Gameplay/Enemies/EnemyVfx.cs': [
  ("public void Swing(Vector2 dir)", "            if (!Has(swing)) return;\n",
   "            " + S + ".Play(\"en_swing\", transform.position);" + M + "\n            if (!Has(swing)) return;\n"),
 ],
 'Gameplay/World/ShrineMarker.cs': [
  ("public void Kindle()", "            lit = true;\n",
   "            lit = true;\n            " + S + ".Play(\"checkpoint\", transform.position);" + M + "\n"),
 ],
 'Gameplay/World/Encounters/MapTriggerHandler.cs': [
  (None, "                    if (kit != null) { kit.AddPotion(1); Hints.Show(\"Picked up a potion.\", 2.5f); }\n",
   "                    if (kit != null) { kit.AddPotion(1); Hints.Show(\"Picked up a potion.\", 2.5f); " + S + ".Play2D(\"pickup_item\"); }" + M + "\n"),
 ],
 'Gameplay/World/KeyRing.cs': [
  ("public static void Give(", "            f.Set(Flag(id));\n",
   "            f.Set(Flag(id));\n            { var ki = Find(id); " + S + ".Play2D(ki != null && ki.kind == \"Document\" ? \"pickup_note\" : \"pickup_item\"); }" + M + "\n"),
 ],
 'Gameplay/Player/Weapons/WeaponRing.cs': [
  ("public static void Give(", "            if (f.Has(Flag(id))) return;\n",
   "            if (f.Has(Flag(id))) return;\n            " + S + ".Play2D(\"pickup_weapon\");" + M + "\n"),
 ],
 'Gameplay/World/QuestTracker.cs': [
  (None, "                if (StepIndex >= 0 && idx > StepIndex) StepChangedAt = Time.time;\n",
   "                if (StepIndex >= 0 && idx > StepIndex) { StepChangedAt = Time.time; " + S + ".Play2D(\"quest_update\"); }" + M + "\n"),
 ],
 'Gameplay/World/Cutscene/DialogueBox.cs': [
  ("internal static void Next()", "            if (!LineDone) { Shown = Line.Length; return; }\n",
   "            " + S + ".Play2D(\"dialog_next\");" + M + "\n            if (!LineDone) { Shown = Line.Length; return; }\n"),
 ],
 'Gameplay/World/Cutscene/BossIntro.cs': [
  (None, "            CardStart = Time.time; CardLength = seconds;\n",
   "            CardStart = Time.time; CardLength = seconds;\n            " + S + ".Play2D(\"boss_intro\");" + M + "\n"),
 ],
 'Gameplay/World/Mechanics/MapMechanics.cs': [
  (None, "                use = () => { if (ctx.ActiveConsole == null) m.Use(); },\n",
   "                use = () => { if (ctx.ActiveConsole == null) { SfxHooks.MechanismUsed(m); m.Use(); } }," + M + "\n"),
 ],
 'Gameplay/World/Mechanics/Effects/ToggleMechanism.cs': [
  ("public override void OnFlagsChanged()", "            Done = true;\n",
   "            Done = true;\n            SfxHooks.Toggled(this);" + M + "\n"),
 ],
 'Gameplay/World/Mechanics/Hazards/LauncherMechanism.cs': [
  ("private void Shoot()", "            if (fly.Length == 0) return;\n",
   "            if (fly.Length == 0) return;\n            " + S + ".Play(\"trap_arrow\", Rect.center);" + M + "\n"),
 ],
 'Gameplay/World/Mechanics/Hazards/PlateMechanism.cs': [
  (None, "            if (pressed && fireAt < 0f && Time.time >= readyAt) fireAt = Time.time + Float(\"delay\", 0f);\n",
   "            if (pressed && fireAt < 0f && Time.time >= readyAt) { fireAt = Time.time + Float(\"delay\", 0f); " + S + ".Play(\"trap_plate\", Rect.center); }" + M + "\n"),
 ],
 'Gameplay/World/Mechanics/Hazards/RollerMechanism.cs': [
  (None, "            if (go && startAt < 0f && now >= readyAt) { startAt = now; hit.Clear(); playerHit = false; }\n",
   "            if (go && startAt < 0f && now >= readyAt) { startAt = now; hit.Clear(); playerHit = false; if (logSr != null) " + S + ".PlayAt(\"trap_roller\", logSr.transform); }" + M + "\n"),
 ],
 'Gameplay/World/Mechanics/Hazards/SpikeMechanism.cs': [
  (None, "        public override void Tick(Vector2 playerPos, bool inside)\n",
   "        private int[] spikeWas;" + M + ": pha trước của từng dải gai\n\n        public override void Tick(Vector2 playerPos, bool inside)\n"),
  (None, "                int p = Phase(k, out float e);\n",
   "                int p = Phase(k, out float e);\n"
   "                if (spikeWas == null || spikeWas.Length != rects.Length) spikeWas = new int[rects.Length];" + M + "\n"
   "                if (p == 2 && spikeWas[k] != 2) " + S + ".Play(\"trap_spikes\", rects[k].center);\n"
   "                spikeWas[k] = p;\n"),
 ],
 'Gameplay/World/Mechanics/Hazards/BladeMechanism.cs': [
  (None, "            sr.transform.position = pos;\n",
   "            sr.transform.position = pos;\n            if (Saw) " + S + ".Loop(this, \"trap_saw\", sr.transform);" + M + " (thôi gọi 0.3 s là tắt)\n"),
 ],
 'UI/RequiemMenus.cs': [
  (None, "            layer.Add(dim);\n",
   "            layer.Add(dim);\n"
   "            layer.RegisterCallback<NavigationMoveEvent>(_ => " + S + ".Play2D(\"ui_move\"), TrickleDown.TrickleDown);" + M + "\n"
   "            layer.RegisterCallback<NavigationSubmitEvent>(_ => " + S + ".Play2D(\"ui_confirm\"), TrickleDown.TrickleDown);\n"
   "            layer.RegisterCallback<ClickEvent>(_ => " + S + ".Play2D(\"ui_confirm\"), TrickleDown.TrickleDown);\n"),
  ("private void Push(VisualElement panel)", "            FocusFirst(panel);\n",
   "            FocusFirst(panel);\n            " + S + ".Play2D(stack.Count <= 1 ? \"ui_open\" : \"ui_confirm\");" + M + "\n"),
  ("private void Pop()", "            if (stack.Count == 0) return;\n",
   "            if (stack.Count == 0) return;\n            " + S + ".Play2D(\"ui_back\");" + M + "\n"),
 ],
}

ok = True
for rel, edits in PATCHES.items():
    p = os.path.join(ROOT, rel)
    raw = open(p, 'rb').read()
    s = raw.decode('utf-8')
    if '§294' in s:
        print('bỏ qua (đã vá)', rel); continue
    nl = '\r\n' if '\r\n' in s else '\n'
    t = s.replace('\r\n', '\n')
    for anchor, old, new in edits:
        start = 0
        if anchor:
            start = t.find(anchor)
            if start < 0: print('THIẾU mốc', rel, anchor); ok = False; break
            i = t.find(old, start)
            # chỉ nhận nếu nằm trong method của mốc: không có 'void ' / 'IEnumerator ' khai báo khác chen giữa (ước lượng)
            if i < 0: print('THIẾU', rel, repr(old[:60])); ok = False; break
        else:
            if t.count(old) != 1: print('KHÔNG DUY NHẤT', rel, t.count(old), repr(old[:60])); ok = False; break
            i = t.find(old)
        t = t[:i] + new + t[i + len(old):]
    else:
        if CHECK: print('khớp', rel, len(edits), 'chỗ'); continue
        os.makedirs(os.path.join(BAK, os.path.dirname(rel)), exist_ok=True)
        shutil.copy2(p, os.path.join(BAK, rel))
        open(p, 'wb').write(t.replace('\n', nl).encode('utf-8'))
        print('vá', rel, len(edits), 'chỗ', 'CRLF' if nl == '\r\n' else 'LF')
        continue
    print('DỪNG ở', rel); break
print('XONG' if ok else 'CÓ LỖI')
