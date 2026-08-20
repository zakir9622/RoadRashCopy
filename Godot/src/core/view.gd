class_name View
## Camera helpers that refuse Godot's look_at crash (zero-length look vector,
## or a node that is not in the tree yet).


static func look_at(node: Node3D, target: Vector3) -> void:
	if node == null or not node.is_inside_tree():
		return
	if node.global_position.distance_squared_to(target) < 0.0004:
		return
	node.look_at(target, Vector3.UP)
