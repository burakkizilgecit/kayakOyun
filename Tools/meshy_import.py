"""Meshy çıktılarını Unity projesine yerleştirir.
ArtSource/Meshy/<ad>/<ad>_rigged.fbx  -> Assets/Resources/Avatars/Meshy/<Ad>.fbx (+ gömülü dokusu <Ad>_tex.jpg)
ArtSource/Meshy/<ad>/<ad>_arise.fbx   -> Assets/Art/Meshy/<Ad>_Arise.fbx   (klip editörde .anim'e çıkarılır)
ArtSource/Meshy/<ad>/<ad>_cheer.fbx   -> Assets/Art/Meshy/<Ad>_Cheer.fbx
Kullanım: python meshy_import.py chef panda knight
"""
import os, shutil, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'ArtSource', 'Meshy')
AVATARS = os.path.join(ROOT, 'Assets', 'Resources', 'Avatars', 'Meshy')
ANIMS = os.path.join(ROOT, 'Assets', 'Art', 'Meshy')


def embedded_jpeg(fbx):
    """FBX ikili dosyasındaki gömülü JPEG (Video düğümünün 'Content' özelliği: 'R' + uzunluk + veri)."""
    d = open(fbx, 'rb').read()
    i = d.find(b'\xff\xd8\xff')
    while i >= 0:
        if d[i - 5] == ord('R'):
            n = int.from_bytes(d[i - 4:i], 'little')
            return d[i:i + n]
        i = d.find(b'\xff\xd8\xff', i + 1)
    return None


def main(names):
    os.makedirs(AVATARS, exist_ok=True)
    os.makedirs(ANIMS, exist_ok=True)
    for n in names:
        cap = n[0].upper() + n[1:]
        shutil.copyfile(os.path.join(SRC, n, n + '_rigged.fbx'), os.path.join(AVATARS, cap + '.fbx'))
        jpg = embedded_jpeg(os.path.join(SRC, n, n + '_rigged.fbx'))
        if jpg:
            open(os.path.join(AVATARS, cap + '_tex.jpg'), 'wb').write(jpg)
        for clip in ('arise', 'cheer', 'walk'):
            src = os.path.join(SRC, n, n + '_' + clip + '.fbx')
            if os.path.exists(src):
                shutil.copyfile(src, os.path.join(ANIMS, cap + '_' + clip.capitalize() + '.fbx'))
        print(cap, 'doku:', len(jpg) if jpg else 'YOK')


if __name__ == '__main__':
    main(sys.argv[1:])
