"""Small, local locomotion rig for relaxed Urban/Autumn meshes, not a full auto-rigger."""
import bpy
from mathutils import Matrix, Vector


def smooth(low, high, value):
    t = max(0, min(1, (value - low) / (high - low)))
    return t * t * (3 - 2 * t)


def rig_relaxed_character(meshes, height):
    """Keep the authored upper-body pose; weight the head and legs for subtle motion.

    These two models have pocketed hands/coat accessories. Animating invented arm
    joints would pull those apart, so their upper bodies deliberately remain posed.
    Coordinates are metres after the converter's floor/height normalization.
    """
    armature = bpy.data.armatures.new('Generated locomotion')
    rig = bpy.data.objects.new('Generated locomotion', armature)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')

    def bone(name, head, tail, parent=None):
        joint = armature.edit_bones.new(name)
        joint.head = Vector(head) * height
        joint.tail = Vector(tail) * height
        if parent:
            joint.parent = armature.edit_bones[parent]
        return joint

    bone('Hips', (0, 0, .48), (0, 0, .58))
    bone('Spine', (0, 0, .58), (0, 0, .82), 'Hips')
    bone('Head', (0, 0, .82), (0, 0, .98), 'Spine')
    for side, sign in [('Left', 1), ('Right', -1)]:
        x = sign * .085
        bone(side + 'UpLeg', (x, 0, .49), (x, 0, .27), 'Hips')
        bone(side + 'Leg', (x, 0, .27), (x, 0, .06), side + 'UpLeg')
    bpy.ops.object.mode_set(mode='OBJECT')

    for mesh in meshes:
        # glTF skins need an armature parent and a clean mesh transform. Bake only
        # this prepared copy, keeping source FBX geometry and textures untouched.
        mesh.data.transform(mesh.matrix_world)
        mesh.parent = rig
        mesh.matrix_world = Matrix.Identity(4)
        groups = {joint.name: mesh.vertex_groups.new(name=joint.name) for joint in armature.bones}
        for vertex in mesh.data.vertices:
            point = mesh.matrix_world @ vertex.co / height
            leg = (1 - smooth(.45, .55, point.z)) * (1 - smooth(.23, .32, abs(point.x)))
            head = smooth(.79, .86, point.z)
            weights = {'Spine': (1 - leg) * (1 - head), 'Head': head}
            side = 'Left' if point.x >= 0 else 'Right'
            calf = 1 - smooth(.24, .31, point.z)
            weights[side + 'UpLeg'] = leg * (1 - calf)
            weights[side + 'Leg'] = leg * calf
            for name, weight in weights.items():
                if weight > .0001:
                    groups[name].add([vertex.index], weight, 'REPLACE')
        modifier = mesh.modifiers.new('Generated locomotion skin', 'ARMATURE')
        modifier.object = rig
    bpy.context.view_layer.update()
    return rig
