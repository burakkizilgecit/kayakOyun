"""Bir Quaternius karakterini oyun için hazırlar: ten ve göz rengi, yalnızca gerekli animasyonlar,
FBX dışa aktarma ve kart için portre render'ı.
Kullanım: blender -b X.blend --python export_avatar.py -- <fbx_out|-> <png_out> <r> <g> <b> [Malzeme=r,g,b ...]
"""
import bpy, sys, math
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
fbx_out, png_out = args[0], args[1]
skin = tuple(float(v) for v in args[2:5]) + (1.0,)
ink = (0.06, 0.05, 0.08, 1.0)


def set_color(mat, rgba):
    mat.diffuse_color = rgba
    if mat.use_nodes:
        for n in mat.node_tree.nodes:
            if n.type == 'BSDF_PRINCIPLED':
                n.inputs['Base Color'].default_value = rgba


for m in bpy.data.materials:
    if m.name == "Skin":
        set_color(m, skin)
    elif m.name == "Face":
        set_color(m, ink)

# İsteğe bağlı malzeme rengi değişiklikleri (doğrusal renk).
for o in args[5:]:
    name, rgb = o.split("=")
    mat = bpy.data.materials.get(name)
    if mat:
        set_color(mat, tuple(float(v) for v in rgb.split(",")) + (1.0,))

# Yalnızca oyunda kullanılan animasyonlar kalır.
keep = {"SitDown", "Idle", "Victory"}
for a in list(bpy.data.actions):
    if a.name not in keep:
        bpy.data.actions.remove(a)

arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
if not arm.animation_data:
    arm.animation_data_create()

if fbx_out != "-":   # "-": yalnızca portre
  bpy.ops.export_scene.fbx(
      filepath=fbx_out, use_selection=False, object_types={'ARMATURE', 'MESH'},
      add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
      bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.5,
      apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y')

# Portre: oturur poz değil, ayakta bekleme pozunda, hafif yandan.
idle = bpy.data.actions.get("Idle")
if idle:
    arm.animation_data.action = idle
    bpy.context.scene.frame_set(int(idle.frame_range[0]))
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
deps = bpy.context.evaluated_depsgraph_get()
for o in meshes:
    ev = o.evaluated_get(deps)
    for v in ev.to_mesh().vertices:
        w = o.matrix_world @ v.co
        mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
center = (mn + mx) / 2
height = mx.z - mn.z
scene = bpy.context.scene
cam = bpy.data.objects.new("PortraitCam", bpy.data.cameras.new("PortraitCam"))
scene.collection.objects.link(cam)
cam.data.type = 'ORTHO'
cam.data.ortho_scale = height * 1.08
cam.location = center + Vector((height * 0.9, -height * 2.2, height * 0.25))
cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_cavity = False
scene.display.shading.show_shadows = False
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'   # AgX renkleri soldurur
scene.view_settings.look = 'None'
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.resolution_x = 256
scene.render.resolution_y = 320
scene.render.filepath = png_out
bpy.ops.render.render(write_still=True)
print("[EXPORT] ok", fbx_out)
