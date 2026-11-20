# 19/11 (§338): nghe thử tiếng bíp — mô phỏng đúng luật của DialogueVoice (55 chữ/s, 1 bíp / chữ cái, giãn cách theo giọng) -> blip_demo.wav
import wave, random, numpy as np, os
SFX = "../../Assets/Resources/Audio/SFX"; SR = 44100
GAP = {"grok": .09, "ent": .1, "lantern_knight": .075, "morrow": .075, "aldous": .07, "elder": .07, "hooded": .075, "goblin": .045, "goblin_child": .05, "narrate": .04}
GAIN = 0.3
LINES = [("ren", "The ground... it's moving. Like something down there is turning in its sleep."),
         ("goblin", "Hah! Tell your headman - Grok wants his grain!"),
         ("villager_f", "Help! The farm is burning!"),
         ("todd", "We can't pay you much. But end Grok, and Millbrook will owe you everything."),
         ("grok", "Useless maggots! Get out of my sight!"),
         ("goblin_child", "...for me?"),
         ("pell", "Careful, young one. The old stones remember."),
         ("isolde", "You came all this way to die in my garden?"),
         ("morrow", "Sing with us, nameless one."),
         ("narrate", "For 1,176 years, one kingdom has stood where three once warred.")]
def ld(p):
    w = wave.open(p); return np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(float) / 32768
out = np.zeros(int(SR * 60)); t = 0.3
for voice, line in LINES:
    takes = [ld(os.path.join(SFX, "bl_%s_%d.wav" % (voice, i))) for i in (1, 2, 3)]
    last = -9
    for i, ch in enumerate(line):
        tc = t + i / 55.0
        if ch.isalnum() and tc - last >= GAP.get(voice, .055):
            last = tc; b = random.choice(takes) * GAIN; s = int(tc * SR); out[s:s + len(b)] += b
    t += len(line) / 55.0 + 0.9
out = out[:int((t + 0.2) * SR)]
with wave.open("blip_demo.wav", "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((np.clip(out, -1, 1) * 32767).astype("<i2").tobytes())
print("blip_demo.wav", round(t, 1), "s")
