# -*- coding: utf-8 -*-
"""
19/11 (§335): tải + xử lý bộ âm PixelLab -> Assets/Resources/Audio/SFX/<id>[_n].wav
  python3 process_sfx.py urls.json            (urls.json = {"sr_pl_swing_1": "https://...wav", ...} — tên PixelLab có tiền tố sr_)
  python3 process_sfx.py --local <thư mục wav> (đã có file thô, tên = tên PixelLab)
Xử lý từng file (numpy, không cần thư viện âm thanh):
  1. về mono, 16-bit, giữ tần số gốc;
  2. cắt khoảng lặng đầu / cuối (ngưỡng -50 dBFS, chừa 10 ms đầu / 40 ms cuối) -> bấm phím là kêu ngay, không trễ;
  3. fade in 3 ms / fade out 25 ms (không "tách" ở đầu / cuối);
  4. CHUẨN HOÁ ĐỘ TO: RMS cửa sổ 50 ms ở phân vị 95 = -16 dBFS, đỉnh tối đa -1 dBFS.
     -> mọi file to ngang nhau; độ to thật trong game = gain trong Resources/Audio/sfx_mix.json (manifest_v2.py, theo tầng tần suất).
  5. âm lặp (LOOPS) nối mượt: trộn 120 ms cuối vào đầu.
In bảng: id, độ dài, đỉnh, RMS -> soát nhanh file hỏng (im lặng / quá ngắn / méo).
"""
import json, os, sys, wave, urllib.request
import numpy as np

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../Assets/Resources/Audio/SFX")
RAW = os.path.join(os.path.dirname(os.path.abspath(__file__)), "raw")
LOOPS = {"trap_saw", "trap_blade", "cs_story_bed"}

def read(path):
    with wave.open(path, "rb") as w:
        ch, sw, sr, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        b = w.readframes(n)
    if sw == 2: x = np.frombuffer(b, "<i2").astype(np.float32) / 32768.0
    elif sw == 3:
        a = np.frombuffer(b, np.uint8).reshape(-1, 3)
        x = (a[:, 0].astype(np.int32) | (a[:, 1].astype(np.int32) << 8) | (a[:, 2].astype(np.int32) << 16))
        x = np.where(x >= 1 << 23, x - (1 << 24), x).astype(np.float32) / (1 << 23)
    elif sw == 4: x = np.frombuffer(b, "<i4").astype(np.float32) / 2147483648.0
    else: x = (np.frombuffer(b, np.uint8).astype(np.float32) - 128) / 128.0
    if ch > 1: x = x.reshape(-1, ch).mean(axis=1)
    return x, sr

def write(path, x, sr):
    y = np.clip(x, -1, 1)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((y * 32767).astype("<i2").tobytes())

def db(v): return 20 * np.log10(max(v, 1e-9))

def process(x, sr, sid):
    thr = 10 ** (-50 / 20)
    idx = np.where(np.abs(x) > thr)[0]
    if len(idx) == 0: return None
    a = max(0, idx[0] - int(0.010 * sr)); b = min(len(x), idx[-1] + int(0.040 * sr))
    x = x[a:b].copy()
    if sid not in LOOPS:
        fi, fo = int(0.003 * sr), min(int(0.025 * sr), len(x) // 3)
        x[:fi] *= np.linspace(0, 1, fi); x[-fo:] *= np.linspace(1, 0, fo)
    win = max(1, int(0.05 * sr))
    n = len(x) // win
    rms = np.sqrt((x[:n * win].reshape(n, win) ** 2).mean(axis=1)) if n > 0 else np.array([np.sqrt((x ** 2).mean())])
    loud = np.percentile(rms, 95)
    g = 10 ** (-16 / 20) / max(loud, 1e-6)
    pk = np.abs(x).max() * g
    if pk > 10 ** (-1 / 20): g *= 10 ** (-1 / 20) / pk
    x *= g
    if sid in LOOPS and len(x) > int(0.5 * sr):
        c = int(0.12 * sr)
        head, tail = x[:c].copy(), x[-c:]
        k = np.linspace(0, 1, c)
        x = x[:-c].copy(); x[:c] = tail * (1 - k) + head * k
    return x

def target_name(pl_name):
    n = pl_name[3:] if pl_name.startswith("sr_") else pl_name
    return n

def main():
    os.makedirs(OUT, exist_ok=True); os.makedirs(RAW, exist_ok=True)
    if sys.argv[1] == "--local":
        srcs = {os.path.splitext(f)[0]: os.path.join(sys.argv[2], f) for f in os.listdir(sys.argv[2]) if f.endswith(".wav")}
    else:
        urls = json.load(open(sys.argv[1]))
        srcs = {}
        for name, url in sorted(urls.items()):
            p = os.path.join(RAW, name + ".wav")
            if not os.path.exists(p):
                # máy chủ chặn User-Agent mặc định của Python (403) -> tải bằng curl
                import subprocess
                subprocess.run(["curl", "-sSfL", "--retry", "6", "--retry-all-errors", "--retry-delay", "2", "-o", p, url], check=True)
            srcs[name] = p
    bad = []
    for name, p in sorted(srcs.items()):
        x, sr = read(p)
        tn = target_name(name)
        base = tn.rsplit("_", 1)[0] if tn.rsplit("_", 1)[-1].isdigit() else tn
        y = process(x, sr, base)
        if y is None or len(y) < int(0.05 * sr): bad.append(name); print("HỎNG (im lặng / quá ngắn):", name); continue
        write(os.path.join(OUT, tn + ".wav"), y, sr)
        print(f"{tn:28s} {len(y)/sr:5.2f}s  peak {db(np.abs(y).max()):6.1f} dB  rms {db(np.sqrt((y**2).mean())):6.1f} dB  ({sr} Hz)")
    print("xong:", len(srcs) - len(bad), "file; hỏng:", bad)

if __name__ == "__main__":
    main()
