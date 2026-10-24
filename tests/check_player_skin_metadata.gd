extends SceneTree

func _initialize():
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--pack="):
			if not ProjectSettings.load_resource_pack(argument.trim_prefix("--pack="), true):
				quit(1)
				return
	var runtime = load("res://WeiDongZhuo/scripts/spine_runtime.gd")
	# The two identical characters are created before either runs _ready.
	var world = Node.new()
	world.set_meta("weidongzhuo_asset_root", "observer/wrong")
	world.set_meta("weidongzhuo_taishan_skin", true)
	var a = Node.new()
	var b = Node.new()
	var a_body = Node.new()
	var b_body = Node.new()
	world.add_child(a)
	world.add_child(b)
	a.add_child(a_body)
	b.add_child(b_body)
	a.set_meta("weidongzhuo_asset_root", "owner-a/classic")
	a.set_meta("weidongzhuo_taishan_skin", false)
	b.set_meta("weidongzhuo_asset_root", "owner-b/taishan")
	b.set_meta("weidongzhuo_taishan_skin", true)
	if runtime.skin_meta(a_body, "weidongzhuo_asset_root", "") != "owner-a/classic" or runtime.skin_meta(a_body, "weidongzhuo_taishan_skin", true) != false:
		push_error("Classic player's node was overwritten by another player's skin")
		quit(1)
		return
	if runtime.skin_meta(b_body, "weidongzhuo_asset_root", "") != "owner-b/taishan" or runtime.skin_meta(b_body, "weidongzhuo_taishan_skin", false) != true:
		push_error("Remote TaiShan metadata missing")
		quit(1)
		return
	world.set_meta("weidongzhuo_taishan_skin", false)
	if runtime.skin_meta(b_body, "weidongzhuo_taishan_skin", false) != true:
		push_error("Scene root must not override player-local metadata")
		quit(1)
		return
	# Merchant uses the same runtime reader; parse its refresh script as well.
	if load("res://WeiDongZhuo/scripts/combat_body.gd") == null:
		quit(1)
		return
	world.free()
	print("PLAYER_SKIN_METADATA_OK isolated_players=2 classic_false_preserved=true root_independent=true")
	quit()
