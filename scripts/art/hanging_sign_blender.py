"""Blender authoring/export: run with Blender --background --python this_file."""
from pathlib import Path
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parents[2]
sources = root.parent / 'NatureCalls-Models'
source = sources / 'Signs-ConnectionHanging.blend'
destination = root / 'Assets/Art/Models/Signs/Signs-ConnectionHanging.fbx'
if source.exists() or destination.exists():
    raise RuntimeError('Preserve existing sign source/export; edit it explicitly rather than overwriting.')
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1

def material(name, color):
    mat = bpy.data.materials.new(name); mat.diffuse_color = (*color, 1)
    return mat

edge = material('Sign Board', (.24, .34, .27))
rope = material('Sign Rope', (.36, .25, .14))
face = material('Sign Face', (.85, .81, .68)); face.use_nodes = True
for kind in ('LAN', 'Relay'):
    image = bpy.data.images.load(str(root / ('Assets/Art/Textures/Signs/Signs-Connection-' + kind + '.png')))
    image.pack(); image.use_fake_user = True; image.filepath = '//../NatureCalls/Assets/Art/Textures/Signs/Signs-Connection-' + kind + '.png'
    if kind == 'LAN':
        texture = face.node_tree.nodes.new('ShaderNodeTexImage'); texture.image = image
        face.node_tree.links.new(texture.outputs['Color'], face.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
board = bpy.context.object; board.name = 'Hanging Board'; board.dimensions = (.7, .035, .28)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
board.data.materials.append(edge)
bevel = board.modifiers.new('Rounded wooden edges', 'BEVEL'); bevel.width = .012; bevel.segments = 3
bpy.context.view_layer.objects.active = board; bpy.ops.object.modifier_apply(modifier=bevel.name)
mesh = bpy.data.meshes.new('Lettering UV'); mesh.from_pydata([(-.335, -.019, -.125), (.335, -.019, -.125), (.335, -.019, .125), (-.335, -.019, .125)], [], [(0, 1, 2, 3)])
mesh.uv_layers.new()
for loop, uv in zip(mesh.uv_layers.active.data, [(0, 0), (1, 0), (1, 1), (0, 1)]): loop.uv = uv
panel = bpy.data.objects.new('Sign Lettering', mesh); scene.collection.objects.link(panel); panel.data.materials.append(face)
for side in (-1, 1):
    curve = bpy.data.curves.new('Hanging cord', 'CURVE'); curve.dimensions = '3D'; curve.bevel_depth = .006; curve.bevel_resolution = 3
    spline = curve.splines.new('BEZIER'); spline.bezier_points.add(2)
    for point, coordinate in zip(spline.bezier_points, [(side * .23, 0, .135), (side * .13, -.005, .27), (0, 0, .38)]):
        point.co = coordinate; point.handle_left_type = point.handle_right_type = 'AUTO'
    cord = bpy.data.objects.new('Left Cord' if side < 0 else 'Right Cord', curve); scene.collection.objects.link(cord); cord.data.materials.append(rope)
    bpy.context.view_layer.objects.active = cord; cord.select_set(True); board.select_set(False); panel.select_set(False)
    bpy.ops.object.convert(target='MESH'); cord.select_set(False)
for obj in scene.objects: obj.select_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(source))
bpy.ops.export_scene.fbx(filepath=str(destination), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_anim=False, path_mode='STRIP', use_mesh_modifiers=True)
print('HANGING_SIGN_EXPORTED', source.name, destination.name)
