"""GLB modelini (dokusuyla) önden, yandan, arkadan ve 3/4'ten yazılımsal olarak çizer: Blender'sız hızlı önizleme.
Kullanım: python preview_glb.py model.glb cikti.png
"""
import sys
import numpy as np
import trimesh
from PIL import Image

W = H = 360


def load(path):
    scene = trimesh.load(path, force='scene')
    meshes = []
    for name, geom in scene.geometry.items():
        # sahne düğümlerinin dönüşümünü uygula
        for node in scene.graph.nodes_geometry:
            T, gname = scene.graph[node]
            if gname == name:
                g = geom.copy()
                g.apply_transform(T)
                meshes.append(g)
    return meshes


def colors_for(mesh):
    """Her köşenin rengi (dokudan UV ile örneklenmiş) ve üçgenleri."""
    v = mesh.vertices
    f = mesh.faces
    vis = mesh.visual
    try:
        uv = vis.uv
        img = vis.material.baseColorTexture if hasattr(vis.material, 'baseColorTexture') else vis.material.image
        tex = np.asarray(img.convert('RGB'), dtype=np.float32) / 255.0
        th, tw = tex.shape[:2]
        return v, f, uv, tex, th, tw
    except Exception:
        return v, f, None, None, 0, 0


def render(meshes, yaw_deg):
    img = np.ones((H, W, 3), np.float32) * np.array([0.82, 0.86, 0.92], np.float32)
    zbuf = np.full((H, W), -1e9, np.float32)
    allv = np.concatenate([m.vertices for m in meshes])
    c = (allv.min(0) + allv.max(0)) / 2
    ext = (allv.max(0) - allv.min(0)).max()
    a = np.radians(yaw_deg)
    R = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
    light = np.array([0.4, 0.7, 0.6]); light /= np.linalg.norm(light)
    for m in meshes:
        v, f, uv, tex, th, tw = colors_for(m)
        p = (v - c) @ R.T
        sx = (p[:, 0] / ext * 0.9 + 0.5) * W
        sy = (0.5 - p[:, 1] / ext * 0.9) * H
        sz = p[:, 2]
        n = np.cross(p[f[:, 1]] - p[f[:, 0]], p[f[:, 2]] - p[f[:, 0]])
        nl = np.linalg.norm(n, axis=1, keepdims=True) + 1e-9
        n = n / nl
        shade = 0.45 + 0.55 * np.clip(n @ light, 0, 1)
        for i, tri in enumerate(f):
            if n[i, 2] <= 0:   # arka yüz
                continue
            xs, ys, zs = sx[tri], sy[tri], sz[tri]
            x0, x1 = int(max(xs.min(), 0)), int(min(xs.max() + 1, W))
            y0, y1 = int(max(ys.min(), 0)), int(min(ys.max() + 1, H))
            if x0 >= x1 or y0 >= y1:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
            d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
            if abs(d) < 1e-9:
                continue
            w0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
            w1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
            w2 = 1 - w0 - w1
            inside = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
            if not inside.any():
                continue
            z = w0 * zs[0] + w1 * zs[1] + w2 * zs[2]
            sub = zbuf[y0:y1, x0:x1]
            win = inside & (z > sub)
            if not win.any():
                continue
            sub[win] = z[win]
            if uv is not None:
                u = w0 * uv[tri[0], 0] + w1 * uv[tri[1], 0] + w2 * uv[tri[2], 0]
                vv = w0 * uv[tri[0], 1] + w1 * uv[tri[1], 1] + w2 * uv[tri[2], 1]
                tx = np.clip((u % 1.0) * (tw - 1), 0, tw - 1).astype(int)
                ty = np.clip((1 - (vv % 1.0)) * (th - 1), 0, th - 1).astype(int)
                col = tex[ty, tx]
            else:
                col = np.ones(gx.shape + (3,), np.float32) * 0.8
            reg = img[y0:y1, x0:x1]
            reg[win] = col[win] * shade[i]
    return img


def main():
    src, out = sys.argv[1], sys.argv[2]
    meshes = load(src)
    tris = sum(len(m.faces) for m in meshes)
    views = [render(meshes, yaw) for yaw in (0, 45, 90, 180)]
    sheet = np.concatenate(views, axis=1)
    Image.fromarray((np.clip(sheet, 0, 1) * 255).astype(np.uint8)).save(out)
    print(src, 'üçgen:', tris)


if __name__ == '__main__':
    main()
