# Downhill Test Track

Open `Assets/Scenes/DownhillTestTrack.unity` to test the soapbox cart on an extended mountain descent with two banked hairpins.

The scene contains a continuous banked road, guardrails, three checkpoint gantries, a slalom section, start and finish gates, procedural mountain terrain, roadside rocks and pine trees, lighting, distance fog, a Main Camera and a spawned cart. Crossing the start gate begins the timer. The finish gate shows the completed run and persistent fastest time; use the results button or **R** to spawn a fresh cart and race again.

Terrain generation shapes the mountain around the generated road and blends the roadside shoulders into the surrounding landscape.

The generated scene includes five colour-coded AI racers with different skill levels. Select the **AI Racers** object to toggle **Enable AI Racers** or change **Active Racer Count** in the Inspector. AI opponents strongly prefer the central portion of the road but retain small individual lane offsets and subtle wandering on straights. These offsets tighten toward the centre through sharp corners. Short-range barrier probes apply a small, damped steering correction and preventative braking only when a guardrail is close, avoiding high-speed ping-pong reactions. A separate forward traffic probe detects slower AI racers, chooses the clearer passing side, smoothly moves out and brakes when closing distance is unsafe. They also pump at low speed and recover themselves if stuck. They do not activate the player's timer, checkpoints or camera.

Each checkpoint must be crossed in order and stores a safe downhill-facing recovery pose. A fatal crash recovers automatically after two seconds. Being stopped below 2 km/h for five seconds or remaining upside down for two seconds shows a **Hold R to recover** prompt. Leaving the marked race surface for four continuous seconds triggers recovery; elevation changes and brief airborne moments do not. Recovery spawns a healthy cart at the latest valid checkpoint and adds five seconds to the active run. Missing a checkpoint invalidates the finish and does not update the fastest time.

## Modular track kit

Reusable pieces are stored in `Assets/DownhillTrack/Prefabs`:

- Start Gate with timing trigger
- Finish Gate with timing trigger
- Time Trial Manager and runtime UI
- Checkpoint Gate
- Red and white 6-metre guardrails
- 10- and 20-metre straight road pieces
- Left- and right-banked 20-metre road pieces
- Slalom Marker

These prefabs can be placed in future scenes independently. A scene needs one Time Trial Manager, a configured cart prefab and spawn point, plus the Start and Finish Gate prefabs.

The track is generated automatically only when `Assets/Scenes/DownhillTestTrack.unity` does not exist. Script updates never regenerate an existing scene. Use **Tools > Downhill Carts > Create Downhill Test Track** when you explicitly want to rebuild it. Rebuilding replaces the generated track scene and terrain data, so duplicate the scene first if you want to preserve manual edits.

Use **Tools > Downhill Carts > Create Modular Track Kit** to rebuild only the reusable prefab collection.
