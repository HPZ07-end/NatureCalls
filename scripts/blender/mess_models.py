"""Generates the cartoon mess models and sprites (GLOSSARY: Mess, StoolKind).

Run headless (never touches an open Blender GUI):
    /Applications/Blender.app/Contents/MacOS/Blender -b -P scripts/blender/mess_models.py -- <out_dir> [<preview.png>]

Every mesh is < 2000 triangles with its origin at the bottom centre. Exports one FBX per model and two PNG sprites.
Material slot names are stable; Editor/MessArtSetup maps them to URP materials.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(args[0] if args else "Assets/Art/Mess")
PREVIEW = args[1] if len(args) > 1 else None
os.makedirs(OUT, exist_ok=True)
random.seed(20261126)

COLORS = {
    "Stool": (0.48, 0.24, 0.07, 1), "StoolDry": (0.29, 0.15, 0.05, 1), "Loose": (0.66, 0.44, 0.11, 1),
    "Bubble": (0.82, 0.62, 0.24, 1), "Vomit": (0.73, 0.76, 0.23, 1), "VomitChunk": (0.88, 0.63, 0.25, 1),
    "Urine": (0.95, 0.84, 0.16, 0.6), "Smear": (0.42, 0.23, 0.06, 1),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = COLORS[name]
    return mat


def finish(bm, name, materials, smooth=True):
    mesh = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    for mat in materials:
        mesh.materials.append(material(mat))
    for poly in mesh.polygons:
        poly.use_smooth = smooth
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    # Origin at the bottom centre.
    xs = [v.co.x for v in mesh.vertices]; ys = [v.co.y for v in mesh.vertices]; zs = [v.co.z for v in mesh.vertices]
    offset = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, min(zs)))
    for v in mesh.vertices:
        v.co -= offset
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    assert tris < 2000, f"{name}: {tris} triangles"
    print(f"MESS_MODEL|{name}|tris={tris}|size=({max(xs)-min(xs):.3f},{max(ys)-min(ys):.3f},{max(zs)-min(zs):.3f})")
    return obj


def tube(bm, points, radii, segments, mat_index=0, cap_end=True):
    """Sweeps a circle along points; returns the last ring."""
    rings = []
    for i, p in enumerate(points):
        a = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
        t = a.normalized()
        ref = Vector((0, 0, 1)) if abs(t.z) < .9 else Vector((1, 0, 0))
        n = t.cross(ref).normalized(); b = t.cross(n).normalized()
        ring = [bm.verts.new(p + (n * math.cos(2 * math.pi * k / segments) + b * math.sin(2 * math.pi * k / segments)) * radii[i])
                for k in range(segments)]
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(segments):
            f = bm.faces.new((r0[k], r0[(k + 1) % segments], r1[(k + 1) % segments], r1[k])); f.material_index = mat_index
    for ring, end in ((rings[0], points[0]), (rings[-1], points[-1])):
        if end is points[-1] and not cap_end:
            continue
        tip = bm.verts.new(end + (end - (points[1] if end is points[0] else points[-2])).normalized() * radii[0 if end is points[0] else -1] * .6)
        for k in range(segments):
            f = bm.faces.new((ring[k], ring[(k + 1) % segments], tip) if end is points[-1] else (ring[(k + 1) % segments], ring[k], tip))
            f.material_index = mat_index
    return rings


def blob(bm, center, radius, squash=(1, 1, 1), subdiv=2, jitter=.12, mat_index=0):
    geom = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    for v in geom["verts"]:
        v.co = Vector((v.co.x * squash[0], v.co.y * squash[1], v.co.z * squash[2])) * (1 + random.uniform(-jitter, jitter)) + center
    for f in {f for v in geom["verts"] for f in v.link_faces}:
        f.material_index = mat_index


def disc(bm, center, radius, thickness, segments=32, lobes=5, wobble=.18, mat_index=0, dome=0.0):
    """An irregular flat splat: lobed outline, slightly domed top."""
    phase = random.uniform(0, math.tau)
    rr = [radius * (1 + wobble * math.sin(lobes * a + phase) + wobble * .5 * math.sin((lobes + 3) * a + 2 * phase) + random.uniform(-.05, .05))
          for a in (2 * math.pi * k / segments for k in range(segments))]
    top = [bm.verts.new(center + Vector((math.cos(2 * math.pi * k / segments) * rr[k], math.sin(2 * math.pi * k / segments) * rr[k], thickness)))
           for k in range(segments)]
    bottom = [bm.verts.new(center + Vector((math.cos(2 * math.pi * k / segments) * rr[k] * 1.04, math.sin(2 * math.pi * k / segments) * rr[k] * 1.04, 0)))
              for k in range(segments)]
    inner = [bm.verts.new(center + Vector((math.cos(2 * math.pi * k / segments) * rr[k] * .55, math.sin(2 * math.pi * k / segments) * rr[k] * .55, thickness + dome * .6)))
             for k in range(segments)]
    mid = bm.verts.new(center + Vector((0, 0, thickness + dome)))
    for k in range(segments):
        n = (k + 1) % segments
        for verts in ((bottom[k], bottom[n], top[n], top[k]), (top[k], top[n], inner[n], inner[k]), (inner[k], inner[n], mid)):
            bm.faces.new(verts).material_index = mat_index
    bm.faces.new(list(reversed(bottom))).material_index = mat_index


# ---------------------------------------------------------------- models
def soft_swirl(name="StoolSoftSwirl", squash=1.0, spread=1.0):
    bm = bmesh.new()
    turns, samples = 2.7, 64
    pts, radii = [], []
    for i in range(samples):
        t = i / (samples - 1)
        a = t * turns * math.tau
        r = (.085 * (1 - t) + .004) * spread
        pts.append(Vector((math.cos(a) * r, math.sin(a) * r, (.03 + .15 * t) * squash)))
        radii.append((.040 * (1 - t) + .011) * (1 + .08 * math.sin(i * 1.7)) * (1 if squash == 1 else 1.15))
    tube(bm, pts, radii, 12)
    # Base cushion hides the start of the coil.
    blob(bm, Vector((0, 0, .028 * squash)), .095 * spread, squash=(1, 1, .36 * squash + .05), subdiv=2, jitter=.06)
    obj = finish(bm, name, ["Stool"])
    return obj


def soft_squashed():
    return soft_swirl("StoolSoftSquashed", squash=.42, spread=1.35)


def hard_log():
    bm = bmesh.new()
    rings, segments, length = 18, 12, .21
    pts, radii = [], []
    for i in range(rings):
        t = i / (rings - 1)
        pts.append(Vector(((t - .5) * length, .018 * math.sin(t * math.pi * 1.3), .028 + .006 * math.sin(t * 5))))
        crack = .72 if i in (4, 5, 10, 14) else 1.0  # dry cracks: pinched grooves
        radii.append(.027 * crack * (1 - .25 * abs(t - .5) * 2) + .004)
    tube(bm, pts, radii, segments)
    # Lumpy cracked chunks along the top.
    for x in (-.06, .015, .07):
        blob(bm, Vector((x, .004, .047)), .012, squash=(1.4, 1, .6), subdiv=1, jitter=.2)
    return finish(bm, "StoolHardLog", ["StoolDry"])


def hard_pellets():
    bm = bmesh.new()
    x = -.07
    for i in range(6):
        r = random.uniform(.016, .024)
        blob(bm, Vector((x, random.uniform(-.012, .012), r * .9)), r, squash=(1.05, 1, .92), subdiv=2, jitter=.09)
        x += r * 1.7 + .002
    return finish(bm, "StoolHardPellets", ["StoolDry"])


def loose_splat():
    bm = bmesh.new()
    disc(bm, Vector((0, 0, 0)), .30, .008, segments=40, lobes=5, wobble=.09, dome=.006)
    for _ in range(9):  # splash dots
        a = random.uniform(0, math.tau); d = random.uniform(.34, .46)
        disc(bm, Vector((math.cos(a) * d, math.sin(a) * d, 0)), random.uniform(.018, .04), .005, segments=10, lobes=3, wobble=.1)
    for _ in range(7):  # bubbles
        a = random.uniform(0, math.tau); d = random.uniform(0, .2)
        blob(bm, Vector((math.cos(a) * d, math.sin(a) * d, .01)), random.uniform(.01, .022), squash=(1, 1, .8), subdiv=1, jitter=.02, mat_index=1)
    return finish(bm, "StoolLooseSplat", ["Loose", "Bubble"])


def vomit():
    bm = bmesh.new()
    disc(bm, Vector((0, 0, 0)), .26, .01, segments=36, lobes=4, wobble=.12, dome=.012)
    for _ in range(5):
        a = random.uniform(0, math.tau); d = random.uniform(.3, .42)
        disc(bm, Vector((math.cos(a) * d, math.sin(a) * d, 0)), random.uniform(.02, .045), .006, segments=10, lobes=3)
    for _ in range(14):  # chunks
        a = random.uniform(0, math.tau); d = random.uniform(0, .2)
        blob(bm, Vector((math.cos(a) * d, math.sin(a) * d, .016)), random.uniform(.01, .02), squash=(1.2, 1, .7), subdiv=1, jitter=.25, mat_index=1)
    return finish(bm, "Vomit", ["Vomit", "VomitChunk"])


def urine_puddle():
    bm = bmesh.new()
    disc(bm, Vector((0, 0, 0)), .32, .004, segments=40, lobes=3, wobble=.11, dome=.002)
    for _ in range(5):
        a = random.uniform(0, math.tau); d = random.uniform(.36, .45)
        disc(bm, Vector((math.cos(a) * d, math.sin(a) * d, 0)), random.uniform(.015, .03), .003, segments=8, lobes=2)
    return finish(bm, "UrinePuddle", ["Urine"])


def toilet_smear():
    """Skid streaks on a paraboloid that approximates the inner bowl; origin at the bowl bottom."""
    bm = bmesh.new()
    def bowl(x, y):
        return 0.14 * ((x / .16) ** 2 + (y / .2) ** 2)
    for s in range(5):
        a0 = math.radians(-40 + s * 20 + random.uniform(-6, 6))
        width = random.uniform(.012, .022)
        rows = 12
        left, right = [], []
        for i in range(rows):
            t = .25 + .7 * i / (rows - 1)
            ax = math.sin(a0) * .16 * t; ay = -math.cos(a0) * .2 * t
            nx, ny = math.cos(a0), math.sin(a0)
            w = width * (1 - .7 * abs(t - .6))
            for side, store in ((-1, left), (1, right)):
                x = ax + nx * w * side; y = ay + ny * w * side
                store.append(bm.verts.new((x, y, bowl(x, y) + .004)))
        for i in range(rows - 1):
            bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
    # A blob stuck near the waterline.
    blob(bm, Vector((.02, -.05, bowl(.02, -.05) + .008)), .02, squash=(1.3, 1, .5), subdiv=1)
    return finish(bm, "ToiletSmear", ["Smear"])


# ---------------------------------------------------------------- sprites
def save_png(name, size, shade):
    img = bpy.data.images.new(name, size, size, alpha=True)
    px = [0.0] * (size * size * 4)
    for y in range(size):
        for x in range(size):
            r, g, b, a = shade(x / (size - 1), y / (size - 1))
            i = (y * size + x) * 4
            px[i:i + 4] = (r, g, b, a)
    img.pixels = px
    img.filepath_raw = os.path.join(OUT, name + ".png")
    img.file_format = "PNG"
    img.save()
    print(f"MESS_SPRITE|{name}|{size}")


def stink(u, v):
    a = 0.0
    for k, x0 in enumerate((.3, .5, .7)):
        cx = x0 + .07 * math.sin(v * 13 + k * 1.9)
        d = abs(u - cx)
        if .08 < v < .92:
            a = max(a, max(0, 1 - d / .035) * min(1, (v - .08) * 8, (.92 - v) * 8))
    col = (.35, .85, .15)
    # Flies: dark bodies with pale wings.
    for fx, fy in ((.22, .78), (.78, .62), (.6, .86)):
        body = ((u - fx) / .028) ** 2 + ((v - fy) / .018) ** 2
        wing = ((u - fx) / .02) ** 2 + ((v - fy - .025) / .03) ** 2
        if body < 1:
            return (.05, .05, .05, 1)
        if wing < 1:
            return (.85, .95, 1, .7)
    return (*col, a)


def sparkle(u, v):
    dx, dy = u - .5, v - .5
    r = math.hypot(dx, dy)
    star = max(0, 1 - abs(dx) / .03 - abs(dy) / .45) + max(0, 1 - abs(dy) / .03 - abs(dx) / .45)
    diag = max(0, 1 - abs(dx - dy) / .04 - r / .3) + max(0, 1 - abs(dx + dy) / .04 - r / .3)
    glow = max(0, 1 - r / .22) ** 2
    a = min(1, star + .6 * diag + glow)
    return (1, .86 + .14 * glow, .35 + .5 * glow, a)


def drop(u, v):
    r = math.hypot(u - .5, v - .5) / .5
    return (1, 1, 1, max(0, 1 - r) ** 1.5)


def preview(objects, path):
    scene = bpy.context.scene
    x = 0
    for obj in objects:
        width = obj.dimensions.x
        obj.location = (x + width / 2, 0, 0)
        x += width + .08
    world = bpy.data.worlds.new("W"); world.color = (.92, .92, .88); scene.world = world
    cam_data = bpy.data.cameras.new("Cam"); cam_data.type = "ORTHO"; cam_data.ortho_scale = x * 1.05
    cam = bpy.data.objects.new("Cam", cam_data); scene.collection.objects.link(cam)
    cam.location = (x / 2, -1.6, 1.0); cam.rotation_euler = (math.radians(62), 0, 0)
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"; scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_specular_highlight = True
    scene.render.resolution_x, scene.render.resolution_y = 1800, 520
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print(f"MESS_PREVIEW|{path}")


def export(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    loc = obj.location.copy(); obj.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, obj.name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False)
    obj.location = loc


reset()
models = [soft_swirl(), soft_squashed(), hard_log(), hard_pellets(), loose_splat(), vomit(), urine_puddle(), toilet_smear()]
for model in models:
    export(model)
save_png("StinkSprite", 128, stink)
save_png("ComfortSparkle", 128, sparkle)
save_png("DropSprite", 64, drop)
if PREVIEW:
    preview(models, PREVIEW)
print("MESS_MODELS_DONE")
