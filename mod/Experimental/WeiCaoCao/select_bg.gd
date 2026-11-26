extends Control
const Runtime = preload("res://Experimental/WeiCaoCao/spine_runtime.gd")
# Calibrated against the user's 1920x1080 game capture and original-art reference.
# The wide floating weapons make the full-body bounding-box center misleading.
const PORTRAIT_ZOOM = 1.45
# Recalibrated from the latest game capture: the previous baseline put the body too far left.
const PREVIOUS_FACE = Vector2(0.717, 0.094)
const REFERENCE_FACE = Vector2(0.616, 0.184)
var background: Node2D
var character: Node2D
var background_bounds: Rect2
var character_bounds: Rect2

func _ready():
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	clip_contents = true
	background = Runtime.create_sprite(self, "Background", "background")
	# The lobby source includes screen-sized masks; use its body-only derivative
	# over the supplied background so those masks cannot expose rectangular edges.
	character = Runtime.create_sprite(self, "Selection", "character")
	background_bounds = Runtime.bounds(self, "Background")
	character_bounds = Runtime.bounds(self, "Character")
	if background != null:
		add_child(background)
	if character != null:
		add_child(character)
	resized.connect(_layout)
	_layout()

func _unhandled_input(event):
	# GUI buttons handle their own clicks first; only the uncovered portrait reacts.
	if not is_visible_in_tree() or character == null:
		return
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		var local_point = character.get_global_transform_with_canvas().affine_inverse() * event.position
		if character_bounds.has_point(local_point):
			Runtime.play_click(character)
			Runtime.play_click(background)

func _layout():
	var viewport_size = size if size.x > 1 and size.y > 1 else get_viewport_rect().size
	if background != null:
		var fit = maxf(viewport_size.x / background_bounds.size.x, viewport_size.y / background_bounds.size.y) * 1.025
		background.scale = Vector2.ONE * fit
		background.position = viewport_size * 0.5 - background_bounds.get_center() * fit
	if character != null:
		var fit = minf(viewport_size.y * 0.9 / character_bounds.size.y, viewport_size.x * 0.6 / character_bounds.size.x)
		var previous_position = Vector2(viewport_size.x * 0.52 - character_bounds.get_center().x * fit, viewport_size.y * 0.9 - character_bounds.end.y * fit)
		character.scale = Vector2.ONE * fit * PORTRAIT_ZOOM
		# Enlarge around the face, then move that landmark to the reference framing.
		# The lower body intentionally extends below the canvas, as in the original art.
		character.position = previous_position * PORTRAIT_ZOOM + viewport_size * (REFERENCE_FACE - PREVIOUS_FACE * PORTRAIT_ZOOM)
