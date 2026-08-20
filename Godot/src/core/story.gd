class_name Story
## Road Rash career narrative: illegal street races, named rivals, gang turf,
## cop busts, the shop, and the championship ride-off. Text is data — UI renders it.

const CLUB := "DER PANZER KLUB"
const SHOP := "OLLEY'S SKOOT-A-RAMA"

const PROLOGUE := "The races aren't on any calendar. No licenses, no ambulances on standby. You buy a tired Panda 250 with a thousand bucks and a rumor: start last, finish fourth or better. Five California tracks, five divisions, fourteen other rashers who will punch the chain out of your hands. Sign the postcard at Der Panzer Klub. Pay Olley if the bike still rolls. The gangs already know your name. The cops will too."

const BUST_LINES := [
	"You're going downtown, rasher. Pay the fine.",
	"Badge, cuffs, and a bill. Stay on the bike next time.",
	"Motor officer says you're done. Walk home.",
]

const BROKE_LINE := "Hospital, impound, and the shop all want money you don't have. That's game over — the road doesn't do credit."

const ENDING := "Fifth division. Night City. First across the line. Natasha pulls alongside, visor up, and for once nobody swings. Two bikes, one highway, and every cop you ever outran in the mirrors. That's the championship. That's Road Rash."


static func division_story(division: int, gang: String) -> String:
	match division:
		0:
			return "Rookie night on the coast. The %s are testing whether you belong on this road or in a ditch." % gang
		1:
			return "Amateur heat. Desert mirage and the %s don't brake for anyone — including you." % gang
		2:
			return "Pro circuit, concrete canyon. The %s own downtown after dark. Cops have your plate." % gang
		3:
			return "Expert altitude. Hairpins, thin air, and the %s waiting in the blind apex." % gang
		_:
			return "Champion weekend. Blackout streets. Beat the %s here and the whole circuit kneels." % gang


static func rival_profile(rider_id: String) -> Dictionary:
	for row in Campaign.ROSTER:
		if String(row["id"]) == rider_id:
			return row
	return {}


static func is_hostile(save: Dictionary, rider_id: String) -> bool:
	var grudges: Dictionary = save.get("grudges", {})
	return int(grudges.get(rider_id, 0)) > 0


static func vignette(save: Dictionary, summary: Dictionary) -> Dictionary:
	var name := String(save.get("player_name", "Rasher"))
	var chapter := int(save.get("chapter", 0))
	var event := Campaign.event_at(mini(chapter, Campaign.total_events() - 1))
	var gang := String(event.get("gang", "Desades"))

	if bool(summary.get("game_over", false)):
		return {"title": "BROKE", "body": BROKE_LINE, "speaker": SHOP}

	if bool(summary.get("champion", false)):
		return {"title": "CHAMPIONSHIP", "body": ENDING, "speaker": "NATASHA"}

	if bool(summary.get("busted", false)):
		return {
			"title": "BUSTED",
			"body": BUST_LINES[chapter % BUST_LINES.size()],
			"speaker": "MOTOR OFFICER",
		}

	var natasha_hostile := is_hostile(save, "natasha")
	if bool(summary.get("won", false)):
		if not natasha_hostile:
			return {
				"title": "AFTER THE LINE",
				"body": "%s, that was clean. Fourth or better keeps you in the game — first pays the Diablo. Watch Slater, he swerves on purpose." % name,
				"speaker": "NATASHA",
			}
		return {
			"title": "THE PACK",
			"body": "You crossed in the money. The %s noticed. They'll come looking with chains next event." % gang,
			"speaker": gang.to_upper(),
		}

	if not natasha_hostile:
		return {
			"title": "BACK MARKER",
			"body": "You're still breathing, %s. Top four next time or this was a hobby. Don't dive-bomb me again and I'll keep the tips coming." % name,
			"speaker": "NATASHA",
		}
	return {
		"title": "ROAD RASH",
		"body": "Outside the money. The %s are laughing. Shop's open if you still have a bike to put money into." % gang,
		"speaker": gang.to_upper(),
	}


static func pre_race_copy(save: Dictionary) -> Dictionary:
	var chapter := int(save.get("chapter", 0))
	var event := Campaign.event_at(chapter)
	var gang := String(event.get("gang", "Desades"))
	var division := int(event.get("division", 0))
	var hostile := is_hostile(save, "natasha")
	var tip := "Natasha: stay off my shoulder and I'll call the cop's distance."
	if hostile:
		tip = "Natasha: you hit me. No more favors. Eat chain."
	return {
		"title": String(event["title"]),
		"body": division_story(division, gang) + " " + String(event["intro"]),
		"tip": tip,
		"gang": gang,
	}
