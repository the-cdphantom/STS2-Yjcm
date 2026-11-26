extends SceneTree

func _initialize():
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--pack="):
			assert(ProjectSettings.load_resource_pack(argument.trim_prefix("--pack="), true))
	var root = "res://Yjcm/Images/CardFrames/WesternDesertB/"
	for kind in ["attack", "skill", "power"]:
		var frame = load(root + "card_frame_" + kind + "_weidongzhuo_s.png") as Texture2D
		assert(frame != null and frame.get_size() == Vector2(598,844), "Missing DongZhuo B frame: " + kind)
		var picture = frame.get_image()
		if picture.is_compressed():
			picture.decompress()
		assert(picture.get_pixel(299,200).a == 0, "Portrait window must stay transparent: " + kind)
		assert(picture.get_pixel(299,700).a > 0, "Frame body must remain visible: " + kind)
	var badge = load(root + "qun_badge.png") as Texture2D
	assert(badge != null and badge.get_size() == Vector2(120,120))
	print("WEIDONGZHUO_CARD_FRAME_OK approved=B textures=3 badge=qun portraitWindow=transparent")
	quit()
