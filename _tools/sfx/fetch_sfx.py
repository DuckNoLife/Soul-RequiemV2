# 17/11 (§294): tải bộ âm ElevenLabs và chuyển sang .ogg cho Unity (Resources/Audio/SFX).
# python3 fetch_sfx.py <urls.json> <sfx_manifest.json> <thư mục ra>
#   urls.json = {"pl_swing": ["https://...content.mp3?...", ...], ...}  (lấy bằng creative_get_generations — link ký hạn vài giờ)
# Âm ngắn: cắt khoảng lặng đầu / cuối, mono 44.1 kHz, chuẩn hoá đỉnh về -1 dBFS, fade-out 15 ms chống tiếng "tách".
# Âm lặp (loop=true trong manifest: nền âm, lưỡi cưa): giữ nguyên độ dài + stereo (không cắt, để còn liền mạch), chỉ chuẩn hoá đỉnh.
# Tên file: takes = 1 -> <id>.ogg ; takes > 1 -> <id>_1.ogg, <id>_2.ogg ... (Sfx.cs gom biến thể theo hậu tố số).
# Cần mạng tới elevenlabsusercontent.com (Settings > Capabilities trên claude.ai) và ffmpeg có libvorbis.
import sys, os, json, re, subprocess, urllib.request

URLS, MANIFEST, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
urls = json.load(open(URLS))
man = {s['id']: s for s in json.load(open(MANIFEST))['sounds']}
os.makedirs(OUT, exist_ok=True)
tmp = os.path.join(OUT, '_mp3'); os.makedirs(tmp, exist_ok=True)


def peak_db(path):
    r = subprocess.run(['ffmpeg', '-hide_banner', '-nostats', '-i', path, '-af', 'volumedetect', '-f', 'null', '-'],
                       capture_output=True, text=True)
    m = re.search(r'max_volume: (-?[\d.]+) dB', r.stderr)
    return float(m.group(1)) if m else 0.0


def convert(src, dst, loop):
    if loop:
        chain = []
        ch = ['-ac', '2']
    else:
        trim = 'silenceremove=start_periods=1:start_threshold=-55dB:start_silence=0.02'
        chain = [trim, 'areverse', trim, 'areverse']
        ch = ['-ac', '1']
    mid = dst + '.wav'
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', src] +
                   (['-af', ','.join(chain)] if chain else []) + ch + ['-ar', '44100', mid], check=True)
    gain = -1.0 - peak_db(mid)
    post = [f'volume={gain:.2f}dB']
    if not loop:
        dur = float(subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', mid],
                                   capture_output=True, text=True).stdout.strip() or 0)
        if dur > 0.05: post.append(f'afade=t=out:st={max(0.0, dur - 0.015):.3f}:d=0.015')
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', mid, '-af', ','.join(post),
                    '-c:a', 'libvorbis', '-q:a', '6', dst], check=True)
    os.remove(mid)


done = []
for sid, lst in urls.items():
    if sid not in man: print('không có trong manifest:', sid); continue
    loop = bool(man[sid].get('loop'))
    multi = len(lst) > 1 or man[sid].get('takes', 1) > 1
    for i, u in enumerate(lst, 1):
        name = f'{sid}_{i}' if multi else sid
        mp3 = os.path.join(tmp, name + '.mp3')
        with urllib.request.urlopen(u, timeout=60) as r, open(mp3, 'wb') as f: f.write(r.read())
        convert(mp3, os.path.join(OUT, name + '.ogg'), loop)
        done.append(name)
        print('xong', name, 'lặp' if loop else '')
print(len(done), 'file')
