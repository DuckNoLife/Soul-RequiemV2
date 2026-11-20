# -*- coding: utf-8 -*-
"""
19/11 (§338): TIẾNG BÍP LỜI THOẠI kiểu Undertale — tổng hợp bằng numpy (không dùng PixelLab: AI không làm được tiếng bíp 30–80 ms gọn, đều).
Mỗi người nói một "giọng bíp" riêng (cao độ + dạng sóng + độ dài), 3 bản lệch cao độ nhẹ để không đều như máy.
DialogueVoice phát 1 tiếng mỗi chữ cái vừa hiện (bỏ dấu cách / dấu câu), giãn cách tối thiểu theo giọng.
  python3 make_blips.py   -> Assets/Resources/Audio/SFX/bl_<giọng>_<n>.wav (44.1 kHz mono 16-bit, đỉnh -6 dBFS)
Độ to thật trong game: gain trong manifest_v2.py (bl_*, tầng 2, ~0.3).
Thiết kế: KHÔNG dùng sóng sin ngân dài (nghe như chuông — người dùng không thích); mọi tiếng tắt nhanh (decay ngắn), lọc thông thấp cho đỡ chói.
"""
import os, wave
import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../Assets/Resources/Audio/SFX")
rng = np.random.default_rng(338)

def osc(kind, f, t, duty=0.5):
    ph = (f * t) % 1.0 if np.isscalar(f) else np.cumsum(f) / SR % 1.0
    if kind == "square": return np.where(ph < duty, 1.0, -1.0)
    if kind == "saw": return 2 * ph - 1
    if kind == "tri": return 1 - 4 * np.abs(ph - 0.5)
    return np.sin(2 * np.pi * ph)

def lowpass(x, fc):
    a = np.exp(-2 * np.pi * fc / SR); y = np.zeros_like(x); s = 0.0
    for i, v in enumerate(x):
        s = (1 - a) * v + a * s; y[i] = s
    return y

def bandnoise(n, fc, q=1.5):
    w = rng.standard_normal(n)
    return lowpass(w, fc * (1 + 1 / q)) - lowpass(w, fc / (1 + 1 / q))

def env(n, att_ms, dec_ms):
    t = np.arange(n) / SR
    a = np.clip(t / (att_ms / 1000), 0, 1)
    d = np.exp(-np.maximum(t - att_ms / 1000, 0) / (dec_ms / 1000))
    e = a * d
    fo = min(n // 4, int(0.006 * SR)); e[-fo:] *= np.linspace(1, 0, fo)
    return e

def blip(f0, ms, kind="square", duty=0.5, att=2, dec=None, lp=3500, drop=0.0, vib=0.0, noise=0.0, noise_fc=1500, detune=0.0, sub=0.0, knock=0.0):
    n = int(SR * ms / 1000); t = np.arange(n) / SR
    f = f0 * (1 - drop * (t / (ms / 1000))) * (1 + vib * np.sin(2 * np.pi * 7 * t))
    x = osc(kind, f * np.ones(n), t, duty)
    if detune: x = 0.5 * x + 0.5 * osc(kind, f * (1 + detune) * np.ones(n), t, duty)
    if sub: x = x + sub * osc("tri", f * 0.5 * np.ones(n), t)
    if noise: x = (1 - noise) * x + noise * 3 * bandnoise(n, noise_fc)
    if knock:  # gõ gỗ: nhiễu ngắn ở đầu
        k = bandnoise(n, 900, 2.5) * np.exp(-t / 0.008) * 4
        x = x + knock * k
    x = lowpass(x, lp) * env(n, att, dec or ms * 0.45)
    return x

def save(name, x):
    x = x / max(np.abs(x).max(), 1e-6) * 10 ** (-6 / 20)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())

# giọng: (tần số Hz, ms, tham số) — chú thích = tính cách
VOICES = {
    "ren":            (165, 48, dict(kind="square", duty=0.32, lp=2400)),                      # trầm, điềm tĩnh
    "todd":           (205, 50, dict(kind="square", duty=0.4, lp=2600, vib=0.02)),             # già hiền, hơi run
    "villager_m":     (215, 44, dict(kind="square", duty=0.45, lp=2800)),
    "villager_f":     (370, 40, dict(kind="tri", lp=3800, sub=0.2)),
    "elder":          (150, 56, dict(kind="tri", lp=2200, vib=0.03, noise=0.12, noise_fc=1200)),  # ông lão khàn
    "goblin_child":   (640, 32, dict(kind="tri", lp=4500, drop=0.06)),                         # bé, rụt rè
    "goblin":         (430, 36, dict(kind="saw", lp=3200, drop=0.18, noise=0.1, noise_fc=2200)),  # the thé, cợt nhả
    "grok":           (82, 72, dict(kind="saw", lp=1100, noise=0.25, noise_fc=500, sub=0.4)),   # tù trưởng quỷ khổng lồ, gầm gừ
    "pell":           (250, 46, dict(kind="tri", lp=2600, vib=0.035, noise=0.18, noise_fc=1800)),  # học giả goblin già, khàn run
    "aldous":         (128, 58, dict(kind="square", duty=0.35, lp=1700, noise=0.15, noise_fc=900)),  # người gác mộ, sỏi đá
    "hooded":         (110, 60, dict(kind="tri", lp=2000, noise=0.55, noise_fc=1400)),          # thì thầm
    "isolde":         (300, 52, dict(kind="tri", lp=3000, detune=0.012)),                       # quý tộc lạnh lùng
    "morrow":         (178, 62, dict(kind="saw", lp=1900, detune=0.018, sub=0.15)),             # giọng xướng kinh (hợp xướng)
    "lantern_knight": (112, 58, dict(kind="square", duty=0.25, lp=1500, detune=0.03)),          # sau mũ sắt, nghẹt
    "ent":            (72, 80, dict(kind="tri", lp=900, knock=0.6)),                            # cây cổ thụ, gõ gỗ
    "narrate":        (900, 18, dict(kind="tri", lp=2600, noise=0.5, noise_fc=2500, dec=6)),    # lời kể: tiếng tách khẽ như máy chữ
    "default":        (240, 44, dict(kind="square", duty=0.5, lp=2800)),                        # người nói chưa có giọng riêng
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, (f0, ms, kw) in VOICES.items():
        for i, k in enumerate((1.0, 1.035, 0.966), 1):
            save("bl_%s_%d" % (name, i), blip(f0 * k, ms, **kw))
    print("wrote", len(VOICES) * 3, "blips ->", os.path.normpath(OUT))
