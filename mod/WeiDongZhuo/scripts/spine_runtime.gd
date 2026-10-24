extends RefCounted

static func create_sprite(host: Node, folder: String, file_name: String, animation: String = "play") -> Node2D:
	if not ClassDB.class_exists("SpineSprite"):
		push_error("WeiDongZhuo: Spine runtime is unavailable")
		return null
	var asset_root = str(skin_meta(host, "weidongzhuo_asset_root", ""))
	if asset_root.is_empty():
		push_error("WeiDongZhuo: asset root was not configured")
		return null
	var base = asset_root.path_join(folder).path_join(file_name)
	if not FileAccess.file_exists(base + ".skel") or not FileAccess.file_exists(base + ".atlas"):
		push_error("WeiDongZhuo: missing animation " + base)
		return null
	var skeleton_file = ClassDB.instantiate("SpineSkeletonFileResource")
	skeleton_file.call("load_from_file", base + ".skel")
	var atlas = ClassDB.instantiate("SpineAtlasResource")
	atlas.call("load_from_atlas_file", base + ".atlas")
	var data = ClassDB.instantiate("SpineSkeletonDataResource")
	data.set("skeleton_file_res", skeleton_file)
	data.set("atlas_res", atlas)
	var sprite = ClassDB.instantiate("SpineSprite") as Node2D
	sprite.set("skeleton_data_res", data)
	var state = sprite.call("get_animation_state")
	if state == null:
		sprite.free()
		push_error("WeiDongZhuo: animation state failed")
		return null
	state.call("set_animation", animation, true, 0)
	return sprite

static func skin_meta(host: Node, key: String, fallback: Variant) -> Variant:
	var current = host
	while current != null:
		if current.has_meta(key):
			return current.get_meta(key)
		current = current.get_parent()
	return fallback
