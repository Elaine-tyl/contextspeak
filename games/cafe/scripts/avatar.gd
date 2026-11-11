extends Node3D

const CampusStyle = preload("campus_style.gd")

const CLIPS := {
	"idle": "Armature|Idle",
	"walk": "Armature|Walking",
	"sit": "Armature|SitIdle"
}
var animation: AnimationPlayer
var current := ""

func setup(model: Node3D, clothing := "woman-clothed") -> void:
	add_child(model)
	animation = model.find_child("AnimationPlayer", true, false) as AnimationPlayer
	play("idle")
	animation.advance(0)
	# FBX mesh bounds do not include skin deformation. Measure the standing rig.
	var skeleton: Skeleton3D = model.find_child("Skeleton3D", true, false)
	var rig_to_avatar := global_transform.affine_inverse() * skeleton.global_transform
	var top := rig_to_avatar * skeleton.get_bone_global_pose(skeleton.find_bone("HeadTop_End")).origin
	var foot := rig_to_avatar * skeleton.get_bone_global_pose(skeleton.find_bone("LeftToeBase")).origin
	var hips := rig_to_avatar * skeleton.get_bone_global_pose(skeleton.find_bone("Hips")).origin
	var factor := 1.65 / (top.y - foot.y)
	model.scale *= factor
	model.position = (model.position - Vector3(hips.x, foot.y, hips.z)) * factor
	for mesh in model.find_children("*", "MeshInstance3D", true, false):
		mesh.hide()
	CampusStyle.new().build(skeleton, clothing, self)

func play(action: String) -> void:
	if not animation or action == current:
		return
	var name: String = CLIPS.get(action, "")
	if not animation.has_animation(name):
		return
	current = action
	animation.get_animation(name).loop_mode = Animation.LOOP_LINEAR
	animation.play(name, 0.18)
