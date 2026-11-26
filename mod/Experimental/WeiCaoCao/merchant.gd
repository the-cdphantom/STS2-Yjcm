extends Node2D
const Runtime = preload("res://Experimental/WeiCaoCao/spine_runtime.gd")
const BODY_HEIGHT = 495.0
const BODY_WIDTH = 530.0
const FACING_X = -1.0

func _ready():
	var character = Runtime.create_sprite(self, "Combat", "character")
	if character == null:
		return
	var body = Runtime.bounds(self, "Combat")
	var fit = minf(BODY_HEIGHT / body.size.y, BODY_WIDTH / body.size.x)
	character.scale = Vector2(FACING_X * fit, fit)
	character.position = Vector2(-body.get_center().x * FACING_X * fit, -body.end.y * fit)
	add_child(character)
