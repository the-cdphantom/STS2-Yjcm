extends Node2D

const Runtime = preload("res://WeiDongZhuo/scripts/spine_runtime.gd")

func _ready():
	apply_skin()

func apply_skin():
	var character = Runtime.create_sprite(self, "Combat", "character")
	if character == null:
		return
	var previous = get_node_or_null("WeiDongZhuoSpine")
	if previous != null:
		remove_child(previous)
		previous.queue_free()
	character.name = "WeiDongZhuoSpine"
	# Measured play bounds: X -730.189..284.460, Godot Y -412.531..413.644.
	# Keep the wide weapon clear of the left edge while placing the body nearer its health bar.
	character.scale = Vector2.ONE * 0.42
	character.position = Vector2(95, -174)
	if bool(Runtime.skin_meta(self, "weidongzhuo_taishan_skin", false)):
		# TaiShan body-only bounds: Godot Y -584.784..252.167; feet at the original baseline.
		character.position = Vector2(95, -106)
	add_child(character)
