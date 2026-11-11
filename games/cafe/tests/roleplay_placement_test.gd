extends SceneTree

# These checks catch missing scene construction, duplicated restored rewards,
# unsafe spawn placement, and stale movement after a role reset.
var failures := 0

func _initialize() -> void:
	call_deferred("run")

func expect(condition: bool, message: String) -> void:
	if not condition:
		failures += 1
		push_error(message)

func run() -> void:
	if not ResourceLoader.exists("res://scripts/roleplay_world.gd"):
		expect(false, "Selectable role scenes must provide a runnable 3D world.")
		quit(1)
		return
	var world_script = load("res://scripts/roleplay_world.gd")
	for scene_id in ["shopping", "farm", "market", "restaurant", "amusement_park"]:
		var world = world_script.new()
		root.add_child(world)
		world.reduce_motion = true
		world.configure(scene_id, "Visitor", "Alex")
		expect(world.scene_id == scene_id, scene_id + ": selecting the scene must build its own environment.")
		expect(world.player is CharacterBody3D, scene_id + ": player must support walking.")
		expect(world.hero != null and world.camera is Camera3D, scene_id + ": player avatar and camera must be visible.")
		expect(world.find_child("ConversationPartner", true, false) != null, scene_id + ": a conversation partner must be present.")
		expect(world.find_child("PartnerCollision", true, false) is CollisionShape3D, scene_id + ": the learner must not walk through the partner.")
		expect(not world.near_npc() and not world.in_conversation, scene_id + ": learners must arrive at the entrance and choose whom to approach.")
		expect(world.has_method("nearest_npc_id") and world.has_method("npc_data"), scene_id + ": world must expose named NPC proximity.")
		expect(world.find_children("PartnerCollision*", "CollisionShape3D", true, false).size() == 3, scene_id + ": all three NPCs must have physical collision.")
		expect(world.find_children("*", "CollisionShape3D", true, false).size() >= 6, scene_id + ": floor, boundaries, and furniture need collision.")
		var scene_label = world.find_child("SceneSign", true, false)
		expect(scene_label is Label3D and scene_label.text.to_lower().contains(scene_id.replace("_", " ")), scene_id + ": world sign must identify the selected scene.")
		var role_label = world.find_child("RoleSign", true, false)
		expect(role_label is Label3D and role_label.text.contains("Visitor"), scene_id + ": role sign must identify the learner's role.")
		# Concrete action roots are stable when saved actions are restored twice.
		for action in ["water", "harvest", "basket", "pay", "meal", "table", "outfit", "price", "bags", "ride", "tickets", "snack", "meet"]:
			world.apply_action(action)
			var action_root = world.find_child("Action_" + action, true, false)
			expect(action_root is Node3D and action_root.get_child_count() >= 2, scene_id + ": " + action + " must produce a concrete prop and readable result.")
			var count_before: int = world.find_children("*", "MeshInstance3D", true, false).size()
			world.apply_action(action)
			expect(world.find_children("*", "MeshInstance3D", true, false).size() == count_before, scene_id + ": restoring " + action + " must not duplicate props.")
		world.player.position = Vector3(0, 0, 3.7)
		world.player.velocity = Vector3(3, 1, 2)
		world.move_player(0.016, true)
		expect(world.player.velocity == Vector3.ZERO, scene_id + ": pausing must stop movement.")
		world.walk_to_npc()
		for frame in range(450):
			world.move_player(0.016, false)
			await physics_frame
		expect(world.near_npc(), scene_id + ": walk-to-partner must reach the conversation area.")
		world.reset_actions()
		expect(world.find_child("Action_outfit", true, false) == null, scene_id + ": reset must remove prior action props immediately.")
		expect(world.player.velocity == Vector3.ZERO, scene_id + ": reset must clear velocity.")
		# Different approved turns can share one action without losing feedback.
		world.apply_action("basket", "apples", "I'd like some apples, please.")
		world.apply_action("basket", "bag", "May I have a bag?")
		expect(world.find_child("Action_basket_apples", true, false) != null and world.find_child("Action_basket_bag", true, false) != null, scene_id + ": separate approved turns must each produce feedback.")
		var turn_count: int = world.find_children("*", "MeshInstance3D", true, false).size()
		world.apply_action("basket", "bag", "May I have a bag?")
		expect(world.find_children("*", "MeshInstance3D", true, false).size() == turn_count, scene_id + ": restoring an approved turn must remain idempotent.")
		expect(world.find_child("PaperBag", true, false) is MeshInstance3D, scene_id + ": an approved bag request must show a bag.")
		world.apply_action("basket", "eggs", "I would like six eggs, please.")
		expect(world.find_children("Egg*", "MeshInstance3D", true, false).size() == 6, scene_id + ": an approved six-egg request must show six eggs.")
		world.apply_action("basket", "two_apples", "Here are your two apples.")
		var apple_turn = world.find_child("Action_basket_two_apples", true, false)
		# A fruit consists of the round body, stem and leaf; count only bodies.
		expect(apple_turn.find_children("Apple*", "Node3D", true, false).size() == 2, scene_id + ": two apples must look like two apples.")
		world.apply_action("meal", "soup", "I would like the vegetable soup, please.")
		expect(world.find_child("SoupBowl", true, false) is MeshInstance3D, scene_id + ": vegetable soup must be served in a bowl.")
		world.apply_action("pay", "bill", "Your bill is ten dollars.")
		var bill = world.find_child("BillReceipt", true, false)
		expect(bill is Label3D and bill.text.contains("$10"), scene_id + ": presenting a bill must show the amount without marking it paid.")
		if scene_id == "shopping":
			check_shopping(world)
		if scene_id == "amusement_park":
			check_park(world)
		if scene_id == "farm":
			world.reset_actions()
			var dry_colour: Color = world.farm_soils[0].material_override.albedo_color
			world.apply_action("water", "carrots", "I will water the carrots.")
			expect(world.farm_soils[0].material_override.albedo_color != dry_colour, "Watering must visibly wet the real farm soil.")
			expect(world.find_child("WateredPlants", true, false) is Node3D, "Watering must reach the real crops.")
			world.reset_actions()
			expect(world.farm_soils[0].material_override.albedo_color == dry_colour, "Reset must restore dry crop soil.")
		# Move away again so a stale automatic-walk flag would visibly pull back.
		world.player.position = Vector3(0, 0, 3.7)
		await physics_frame
		var spawn: Vector3 = world.player.position
		world.move_player(0.016, false)
		expect(Vector2(world.player.position.x, world.player.position.z).distance_to(Vector2(spawn.x, spawn.z)) < 0.03, scene_id + ": reset must cancel automatic walking.")
		# Reconfiguration kills transient effects and drops ambient-node references.
		world.reduce_motion = false
		world.apply_action("snack", "disposal", "Popcorn, please.")
		world.configure("shopping", "Helping a friend", "Mia, your friend")
		world._process(0.1)
		expect(world.find_child("FerrisWheelRotor", true, false) == null and world.find_child("Action_snack_disposal", true, false) == null, "Changing roles/scenes must safely dispose prior rides and action props.")
		world.free()
	print("Role scene placement/actions: ", "passed" if failures == 0 else "%d failed" % failures)
	quit(0 if failures == 0 else 1)

func check_shopping(world) -> void:
	expect(world.find_child("ClothingRack*", true, false) is Node3D, "Shopping must show hanging clothes on a rack.")
	expect(world.find_child("ShopMirror", true, false) is MeshInstance3D, "Shopping must include a mirror.")
	expect(world.find_child("FittingRoom", true, false) is Node3D, "Shopping must include a fitting room.")
	expect(world.find_child("SaleArea", true, false) is Node3D, "Shopping must include a sale area.")
	world.apply_action("bags", "keep_browsing", "Don't try it on; let's check another shop.")
	var browsing = world.find_child("Action_bags_keep_browsing", true, false)
	expect(browsing.find_child("FittingRoomCue", true, false) == null, "Choosing another shop must not direct the learner to try clothes on.")
	world.reset_actions()
	for choice in [
		["blue_hoodie", "I like the blue hoodie.", "HoodieBody", Color("#6b9dcc")],
		["red_jacket", "The red jacket looks good.", "JacketBody", Color("#ca786e")],
		["green_sweatshirt", "I prefer the green sweatshirt.", "SweatshirtBody", Color("#87a67c")]
	]:
		world.apply_action("outfit", choice[0], choice[1])
		var garment = world.find_child("Action_outfit_" + choice[0], true, false)
		var body = garment.find_child(choice[2], true, false) if garment else null
		expect(body is MeshInstance3D and body.material_override.albedo_color.is_equal_approx(choice[3]), "Outfit feedback must show the learner's actual garment and colour: " + choice[0])
		var count: int = world.find_children("*", "MeshInstance3D", true, false).size()
		world.apply_action("outfit", choice[0], choice[1])
		expect(world.find_children("*", "MeshInstance3D", true, false).size() == count, "Restoring the same chosen outfit must be idempotent.")
	world.apply_action("outfit", "blue_hoodie_reply", "I want the green sweatshirt.")
	var real_choice = world.find_child("Action_outfit_blue_hoodie_reply", true, false)
	var real_body = real_choice.find_child("SweatshirtBody", true, false) if real_choice else null
	expect(real_body is MeshInstance3D and real_body.material_override.albedo_color.is_equal_approx(Color("#87a67c")), "The accepted outfit reply must take priority over words in the step identifier.")
	world.apply_action("outfit", "short_reply", "Yes!")
	expect(world.find_child("Action_outfit_short_reply", true, false).find_child("ClothingChoice", true, false) is Label3D, "A short outfit reply must show a neutral clothing cue without inventing a hoodie.")
	for choice in [["dress", "I love the green dress.", "GREEN DRESS", Color("#87a67c")], ["jeans", "The blue jeans look good.", "BLUE JEANS", Color("#6b9dcc")], ["shorts", "Let's get red shorts.", "RED SHORTS", Color("#ca786e")], ["shoes", "I prefer the white shoes.", "WHITE SHOES", Color("#f1e9d6")]]:
		world.apply_action("outfit", choice[0], choice[1])
		var selected = world.find_child("Action_outfit_" + choice[0], true, false)
		var item = selected.find_child("ClothingChoice", true, false) if selected else null
		var card = selected.find_child("ClothingCard", true, false) if selected else null
		expect(item is Label3D and item.text == choice[2] and card is MeshInstance3D and card.material_override.albedo_color.is_equal_approx(choice[3]), "Unsupported garment geometry must show the accepted item and colour: " + choice[0])
		expect(selected != null and selected.find_child("HoodieBody", true, false) == null, "A different accepted clothing item must never turn into a hoodie.")
	world.apply_action("price", "cheaper", "Can we find a cheaper one in the sale?")
	var price = world.find_child("Action_price_cheaper", true, false)
	var price_tag = price.find_child("PriceTag", true, false) if price else null
	expect(price_tag is Label3D and price_tag.text.contains("SALE") and not price_tag.text.contains("PAID"), "Asking for a cheaper outfit must show a sale price cue.")
	world.apply_action("bags", "try_on", "Let's try it on in the fitting room.")
	expect(world.find_child("FittingRoomCue", true, false) is Label3D, "Trying an outfit must point to the fitting room.")
	world.apply_action("bags", "carry", "I can carry our bags.")
	expect(world.find_child("ShoppingBag", true, false) is MeshInstance3D, "Finishing shopping must show a shopping bag.")

func check_park(world) -> void:
	var rotor = world.find_child("FerrisWheelRotor", true, false)
	var carousel = world.find_child("CarouselRotor", true, false)
	expect(rotor is Node3D and carousel is Node3D, "The park must have a Ferris wheel and carousel.")
	expect(world.find_child("CoasterTrack", true, false) is Node3D, "The park must have a roller coaster cue.")
	expect(world.find_child("TicketBooth", true, false) is Node3D, "The park must have a ticket booth.")
	expect(world.find_child("SnackStand", true, false) is Node3D, "The park must have somewhere to buy snacks.")
	world.reset_actions()
	for ride in [["wheel", "Let's ride the Ferris wheel.", "FERRIS WHEEL"], ["carousel", "I choose the carousel.", "CAROUSEL"], ["coaster", "The roller coaster looks fun.", "ROLLER COASTER"]]:
		world.apply_action("ride", ride[0], ride[1])
		var prop = world.find_child("Action_ride_" + ride[0], true, false)
		var marker = prop.find_child("SelectedRideMarker", true, false) if prop else null
		var label = marker.find_child("RideChoice", true, false) if marker else null
		expect(label is Label3D and label.text == ride[2], "The selected ride must have its own visible marker: " + ride[0])
		var count: int = world.find_children("*", "MeshInstance3D", true, false).size()
		world.apply_action("ride", ride[0], ride[1])
		expect(world.find_children("*", "MeshInstance3D", true, false).size() == count, "Restoring a ride choice must not duplicate the marker.")
	world.apply_action("ride", "coaster_reply", "I'd prefer the carousel.")
	var real_ride = world.find_child("Action_ride_coaster_reply", true, false)
	var real_ride_label = real_ride.find_child("RideChoice", true, false) if real_ride else null
	expect(real_ride_label is Label3D and real_ride_label.text == "CAROUSEL", "The accepted ride reply must take priority over words in the step identifier.")
	world.apply_action("ride", "change_mind", "Not the coaster; let's try the carousel instead.")
	var changed = world.find_child("Action_ride_change_mind", true, false)
	var changed_label = changed.find_child("RideChoice", true, false) if changed else null
	expect(changed_label is Label3D and changed_label.text == "CAROUSEL", "A positive alternative must override an explicitly rejected ride.")
	world.apply_action("ride", "instead_of", "Let's try the carousel instead of the coaster.")
	var alternative = world.find_child("Action_ride_instead_of", true, false)
	var alternative_label = alternative.find_child("RideChoice", true, false) if alternative else null
	expect(alternative_label is Label3D and alternative_label.text == "CAROUSEL", "An instead-of phrase must not select the rejected final ride.")
	for plan in [["reassure", "No worries, we can skip the big rides."], ["later", "Let's come back later."], ["decline", "I don't want the coaster."]]:
		world.apply_action("ride", plan[0], plan[1])
		var plan_prop = world.find_child("Action_ride_" + plan[0], true, false)
		expect(plan_prop != null and plan_prop.find_child("SharedPlanCue", true, false) is Label3D and plan_prop.find_child("SelectedRideMarker", true, false) == null, "A shared plan without a positive ride choice must not invent a Ferris wheel ride: " + plan[0])
	world.apply_action("ride", "curly_decline", "I don’t want the coaster.")
	var curly_decline = world.find_child("Action_ride_curly_decline", true, false)
	expect(curly_decline.find_child("SharedPlanCue", true, false) != null and curly_decline.find_child("RideChoice", true, false) == null, "Curly apostrophes must not reverse a spoken negative preference.")
	world.apply_action("meet", "ice_cream_stand", "Meet by the ice cream stand.")
	var snack_meet = world.find_child("Action_meet_ice_cream_stand", true, false)
	var snack_landmark = snack_meet.find_child("MeetingLandmark", true, false) if snack_meet else null
	expect(snack_landmark is Label3D and snack_landmark.text.contains("SNACK STAND"), "An ice cream stand meeting must point to the snack stand.")
	world.apply_action("meet", "statue", "Let's meet by the statue.")
	var unknown_meet = world.find_child("Action_meet_statue", true, false)
	expect(unknown_meet != null and unknown_meet.find_child("SharedMeetingCue", true, false) is Label3D and unknown_meet.find_child("MeetingLandmark", true, false) == null, "An unsupported meeting landmark must keep a generic plan cue instead of inventing an entrance.")
	world.apply_action("tickets", "pair", "Let's get two tickets.")
	var tickets = world.find_child("Action_tickets_pair", true, false)
	expect(tickets != null and tickets.find_children("ParkTicket*", "MeshInstance3D", true, false).size() == 2, "Two friends need two visible park tickets.")
	world.apply_action("tickets", "four", "Let's get four tickets.")
	var four_tickets = world.find_child("Action_tickets_four", true, false)
	expect(four_tickets.find_children("ParkTicket*", "MeshInstance3D", true, false).size() == 4, "An accepted ticket quantity must match its visual.")
	world.apply_action("snack", "popcorn", "I would like popcorn.")
	world.apply_action("snack", "icecream", "Let's have ice cream.")
	var popcorn = world.find_child("Action_snack_popcorn", true, false)
	var icecream = world.find_child("Action_snack_icecream", true, false)
	expect(popcorn != null and popcorn.find_child("PopcornCarton", true, false) is MeshInstance3D, "Popcorn must appear in a carton.")
	expect(icecream != null and icecream.find_child("IceCreamCone", true, false) is MeshInstance3D, "Ice cream must appear in a cone.")
	world.apply_action("snack", "change_snack", "Let's have popcorn instead of ice cream.")
	var changed_snack = world.find_child("Action_snack_change_snack", true, false)
	expect(changed_snack.find_child("PopcornCarton", true, false) != null and changed_snack.find_child("IceCreamCone", true, false) == null, "Snack alternatives must show the chosen snack, not the declined snack.")
	world.apply_action("snack", "share", "Yes!")
	var shared = world.find_child("Action_snack_share", true, false)
	expect(shared != null and shared.find_child("SharedSnack", true, false) is MeshInstance3D, "A short snack reply must show a generic food cue.")
	world.apply_action("meet", "wheel", "Let's meet by the Ferris wheel.")
	var wheel_meet = world.find_child("Action_meet_wheel", true, false)
	var meet = wheel_meet.find_child("MeetingLandmark", true, false) if wheel_meet else null
	expect(meet is Label3D and meet.text.contains("FERRIS WHEEL"), "The meeting marker must reflect the chosen landmark.")
	if rotor and carousel:
		world.reduce_motion = false
		world._process(0.5)
		expect(absf(rotor.rotation.z) > 0.01 and absf(carousel.rotation.y) > 0.01, "The park rides must move gently when motion is enabled.")
		world.reduce_motion = true
		var still_wheel: Vector3 = rotor.rotation
		var still_carousel: Vector3 = carousel.rotation
		world._process(0.5)
		expect(rotor.rotation == still_wheel and carousel.rotation == still_carousel, "Reduce motion must stop every ambient park ride.")
		world.reset_actions()
		expect(rotor.rotation.is_zero_approx() and carousel.rotation.is_zero_approx(), "Reset must restore ambient rides to their starting pose.")
		world.reduce_motion = false
		world._process(0.5)
		expect(absf(rotor.rotation.z) > 0.01, "Ambient motion must work again after reset.")
		world.reduce_motion = true
