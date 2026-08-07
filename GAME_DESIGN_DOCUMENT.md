# Game Design Document (GDD)

## Core Gameplay Loop
1.  **Race:** Navigate procedurally generated highways (Coastal, Desert, City).
2.  **Combat:** Engage in melee and weapon-based combat with rival bikers at high speeds.
3.  **Survive:** Avoid civilian traffic and evade dynamic police pursuits.
4.  **Upgrade:** Earn currency to upgrade bike performance or purchase new weapons in the Garage.

## Physics & Handling
*   **Arcade-Sim Hybrid:** Custom sphere-cast raycast physics model. Snappy acceleration with rear-wheel drifting mechanics.
*   **Gravity Multiplier:** Custom gravity logic keeps the bike grounded during sharp elevation changes, simulating rider weight shifting.

## Combat Mechanics
*   **Animation Rigging (IK):** Hands dynamically lock to handlebars and detach seamlessly to swing weapons.
*   **Weapons:** Fists, chains, baseball bats. Weapons can be stolen from opponents during combat.
*   **Physics Reactions:** High-speed hits apply sideways impulse forces to rigid bodies.

## AI & "Heat" System
*   **Rival AI:** FSM-based (Race, Draft, Attack, Evade). Features an aggression multiplier that increases if the player attacks them.
*   **Police Heat:** Cops dynamically spawn based on destructive behavior. Getting stopped near a cop triggers a "Busted" sequence, costing the player currency.
