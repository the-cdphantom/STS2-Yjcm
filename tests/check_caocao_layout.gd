extends SceneTree

class AnimationProbe extends RefCounted:
	var commands = []
	func set_animation(animation, loop, track):
		commands.append(["set", animation, loop, track])
	func add_animation(animation, delay, loop, track):
		commands.append(["add", animation, delay, loop, track])

func _initialize():
	var pack_path = ""
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--pack="):
			pack_path = argument.trim_prefix("--pack=").replace("\\", "/")
			assert(ProjectSettings.load_resource_pack(pack_path,true))
	var runtime = load("res://Experimental/WeiCaoCao/spine_runtime.gd")
	# Exercise the actual packaged/deployed loose metadata, including ancestor lookup.
	var metadata_root = pack_path.get_base_dir().path_join("Spine/WeiCaoCao")
	var metadata_path = metadata_root.path_join("character.json.txt")
	assert(FileAccess.file_exists(metadata_path), "Packaged character metadata missing: " + metadata_path)
	var metadata = JSON.parse_string(FileAccess.get_file_as_string(metadata_path))
	var host = Node.new()
	host.set_meta("weicaocao_asset_root", metadata_root)
	var child = Node.new()
	host.add_child(child)
	var actual_bounds = {}
	for folder in ["Background", "Character", "Combat"]:
		var b = metadata.assets[folder].focus
		var expected = Rect2(b.minX, -b.maxY, b.width, b.height)
		var actual = runtime.bounds(child, folder)
		if actual == Rect2(-500, -500, 1000, 1000) or not actual.is_equal_approx(expected):
			push_error("Runtime bounds did not read packaged metadata: " + folder)
			host.free()
			quit(1)
			return
		actual_bounds[folder] = actual
	# Old local asset trees still use character.json. Test fallback separately.
	var legacy_root = OS.get_user_data_dir().path_join("yjcm-bounds-" + str(Time.get_ticks_usec()))
	assert(DirAccess.make_dir_recursive_absolute(legacy_root) == OK)
	var legacy_path = legacy_root.path_join("character.json")
	assert(DirAccess.copy_absolute(metadata_path, legacy_path) == OK)
	host.set_meta("weicaocao_asset_root", legacy_root)
	assert(runtime.bounds(child, "Combat").is_equal_approx(actual_bounds.Combat), "Legacy character.json fallback failed")
	assert(DirAccess.remove_absolute(legacy_path) == OK)
	assert(runtime.bounds(child, "Combat") == Rect2(-500, -500, 1000, 1000), "Missing metadata fallback changed")
	assert(DirAccess.remove_absolute(legacy_root) == OK)
	host.free()
	print("CAOCAO_RUNTIME_BOUNDS_OK folders=3 source=" + metadata_path + " legacy_fallback=true missing_fallback=true")
	var probe = AnimationProbe.new()
	runtime.play_idle(probe)
	assert(probe.commands == [["set", "play", true, 0]])
	probe.commands.clear()
	runtime.queue_click(probe)
	assert(probe.commands == [["set", "play2", false, 0], ["add", "play", 0.0, true, 0]])
	var scene = load("res://Experimental/WeiCaoCao/select_bg.tscn") as PackedScene
	assert(scene != null)
	var node = scene.instantiate() as Control
	node.set_anchors_preset(Control.PRESET_TOP_LEFT)
	# Test responsive transforms without requiring the game's Spine extension.
	var bg = Node2D.new()
	var character = Node2D.new()
	node.set("background", bg)
	node.set("character", character)
	node.add_child(bg)
	node.add_child(character)
	node.set("background_bounds", actual_bounds.Background)
	node.set("character_bounds", actual_bounds.Character)
	for viewport_size in [Vector2(1920,1080),Vector2(2560,1080),Vector2(1280,720),Vector2(1080,1920)]:
		node.size = viewport_size
		node.call("_layout")
		assert(bg.scale.x > 0 and is_equal_approx(bg.scale.x, bg.scale.y))
		assert(2948.372 * bg.scale.x >= viewport_size.x and 1605.08 * bg.scale.y >= viewport_size.y)
		assert(character.scale.x > 0 and is_equal_approx(character.scale.x, character.scale.y))
		# Regression fixture: face landmark measured in the reported in-game screenshot.
		# The original reference places it at X61.6%, Y18.4%, with body cropped below.
		var old_fit = minf(viewport_size.y * 0.9 / 1291.356, viewport_size.x * 0.6 / 1885.083)
		var old_position = Vector2(viewport_size.x * 0.52 + 624.8065 * old_fit, viewport_size.y * 0.9 - 833.731 * old_fit)
		var local_face = (viewport_size * Vector2(0.717, 0.094) - old_position) / old_fit
		var relocated = character.position + local_face * character.scale
		assert(absf(relocated.x / viewport_size.x - 0.616) < 0.001)
		assert(absf(relocated.y / viewport_size.y - 0.184) < 0.001)
	var material = load("res://materials/cards/frames/card_frame_weicaocao_mat.tres") as ShaderMaterial
	assert(material != null and material.shader != null)
	assert(material.get_shader_parameter("body_color").b > material.get_shader_parameter("body_color").r)
	var frame_root = "res://Experimental/WeiCaoCao/card_frames/"
	for kind in ["attack", "skill", "power"]:
		var frame = load(frame_root + "card_frame_" + kind + "_weicaocao_s.png") as Texture2D
		assert(frame != null and frame.get_size() == Vector2(598,844), "Missing M2 frame " + kind)
		var frame_image = frame.get_image()
		if frame_image.is_compressed():
			frame_image.decompress()
		assert(frame_image.get_pixel(299,200).a == 0 and frame_image.get_pixel(299,700).a > 0)
	var wei_badge = load(frame_root + "wei_badge.png") as Texture2D
	assert(wei_badge != null and wei_badge.get_size() == Vector2(120,120))
	var combat_scene = load("res://Experimental/WeiCaoCao/creature.tscn") as PackedScene
	assert(combat_scene != null)
	var combat_node = combat_scene.instantiate()
	assert(combat_node.get_node("Bounds").size == Vector2(530,495))
	combat_node.free()
	var icon_scene = load("res://Experimental/WeiCaoCao/icon.tscn") as PackedScene
	assert(icon_scene != null)
	var icon = icon_scene.instantiate() as TextureRect
	assert(icon.size == Vector2(54,54) and icon.texture != null)
	assert(icon.texture.get_size() == Vector2(256,256))
	icon.free()
	var relic = load("res://Experimental/WeiCaoCao/images/jun_he.png") as Texture2D
	assert(relic != null and relic.get_size() == Vector2(256,256))
	var relic_image = relic.get_image()
	if relic_image.is_compressed():
		relic_image.decompress()
	assert(relic_image.get_pixel(0,0).a == 0)
	for image_name in ["hu_jia", "duo_yue", "jun_he_buff", "select_icon", "fu_le", "fu_enchantment", "wen_lan_buff"]:
		assert(load("res://Experimental/WeiCaoCao/images/" + image_name + ".png") is Texture2D)
	for image_name in ["ming_fa_buff", "po_rui_buff", "gui_xin_buff"]:
		var buff_icon = load("res://Experimental/WeiCaoCao/images/" + image_name + ".png") as Texture2D
		assert(buff_icon != null and buff_icon.get_size() == Vector2(256, 256), "Missing/corrupt buff icon: " + image_name)
	for image_name in ["dian_lun", "chong_yi", "su_jun", "di_feng", "zhi_dui", "ying_shi", "shi_zha", "liang_yan", "shi_mou_yang", "shi_mou_yin", "quan_mou_yang", "quan_mou_yin", "xian_mou_yang", "xian_mou_yin", "san_shi", "chen_lue", "dun_xi", "she_fu", "qiao_dui", "tuo_yu", "che_xie", "kang_li", "wei_ti", "bo_xuan", "chou_tao", "ming_fa", "wu_mei", "zhan_yi", "nu_chen", "gong_tu"]:
		var portrait = load("res://Experimental/WeiCaoCao/images/" + image_name + ".png") as Texture2D
		assert(portrait != null and portrait.get_size() == Vector2(800,600), "Missing/corrupt portrait: " + image_name)
	node.free()
	for form in ["Yang", "Yin"]:
		for index in range(1,5):
			assert(load("res://Yjcm/Audio/Monsters/MouSimaYi/Sfx/" + form + "/quan_mou_" + str(index) + ".wav") is AudioStream)
	print("CAOCAO_LAYOUT_OK resolutions=4 m2_frames=3 wei_badge=true idle=play click=play2_once_then_idle")
	quit()
