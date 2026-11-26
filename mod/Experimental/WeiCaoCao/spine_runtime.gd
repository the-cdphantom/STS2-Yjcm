extends RefCounted
const IDLE_ANIMATION = "play"
const CLICK_ANIMATION = "play2"

static func asset_root(host: Node) -> String:
	var node = host
	while node != null:
		if node.has_meta("weicaocao_asset_root"):
			return str(node.get_meta("weicaocao_asset_root"))
		node = node.get_parent()
	return ""

static func create_sprite(host: Node, folder: String, base: String) -> Node2D:
	if not ClassDB.class_exists("SpineSprite"):
		return null
	var root = asset_root(host)
	if root.is_empty():
		return null
	var skeleton = ClassDB.instantiate("SpineSkeletonFileResource")
	skeleton.call("load_from_file", root.path_join(folder).path_join(base + ".skel"))
	var atlas = ClassDB.instantiate("SpineAtlasResource")
	var atlas_folder = "Character" if folder in ["Combat", "Selection"] else folder
	atlas.call("load_from_atlas_file", root.path_join(atlas_folder).path_join(base + ".atlas"))
	var data = ClassDB.instantiate("SpineSkeletonDataResource")
	data.set("skeleton_file_res", skeleton)
	data.set("atlas_res", atlas)
	var sprite = ClassDB.instantiate("SpineSprite") as Node2D
	sprite.set("skeleton_data_res", data)
	var state = sprite.call("get_animation_state")
	play_idle(state)
	return sprite

static func play_idle(state: Object):
	state.call("set_animation", IDLE_ANIMATION, true, 0)

static func queue_click(state: Object):
	state.call("set_animation", CLICK_ANIMATION, false, 0)
	# Zero delay queues the next clip after the preceding non-looping clip ends.
	state.call("add_animation", IDLE_ANIMATION, 0.0, true, 0)

static func play_click(sprite: Node2D):
	if sprite == null or not is_instance_valid(sprite):
		return
	var state = sprite.call("get_animation_state")
	if state != null:
		queue_click(state)

static func bounds(host: Node, folder: String) -> Rect2:
	var root = asset_root(host)
	var file = FileAccess.open(root.path_join("character.json.txt"), FileAccess.READ)
	if file == null:
		file = FileAccess.open(root.path_join("character.json"), FileAccess.READ)
	if file == null:
		return Rect2(-500, -500, 1000, 1000)
	var data = JSON.parse_string(file.get_as_text())
	var b = data.assets[folder].focus
	# Spine Y up -> Godot canvas Y down.
	return Rect2(b.minX, -b.maxY, b.width, b.height)
