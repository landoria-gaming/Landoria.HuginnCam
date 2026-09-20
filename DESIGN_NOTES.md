# SagaCapture design notes

## Camera identity

- The camera is a drone.
- Its field of view is configured through `SagaCameraFOV`.
- The default field of view is 65 degrees and its accepted range is 40 to
  120 degrees.
- It should move and behave like a drone.
- It can hover in place.
- It is not a bird and should have no bird-related behavior or audio.

## Motion quality

- Smooth camera rotation as much as possible.
- Smooth acceleration and deceleration as much as possible.
- Avoid visible jolts during normal movement.
- Avoid visible jolts when changing camera states or behaviors.
- Normal acceleration and deceleration are limited to 1 meter per second
  squared.
- Acceleration itself changes gradually to avoid sudden changes in force.
- Flight-mode transitions blend over approximately 2 seconds.
- A transition preserves continuous position, velocity, acceleration, and
  rotation state instead of restarting movement interpolation.
- Flight-mode transitions should not be perceptible in the recorded image.

## Flight modes

The drone has two main flight modes:

- `TrailingFlight`: the drone follows the player while the player is running
  or sprinting.
- `OrbitFlight`: the drone flies around the player when the player's
  horizontal movement remains low.

### TrailingFlight

- The drone stays behind the player.
- Its maximum speed is 1.5 times the player's sprint speed.
- Its target speed increases progressively with its distance behind the
  player.
- As it approaches its ideal trailing position, its speed converges smoothly
  toward the player's current speed.
- Its ideal position is between 1 and 4 meters behind the player.
- It may move slightly to the left or right of the ideal trailing line.
- It always looks at the player.
- At low speed, it may descend to 1.5 meters above the terrain.
- Its terrain clearance rises smoothly to 2 meters as its speed approaches
  3 meters per second.
- It reevaluates the segment between itself and the player every 100 ms.
- Its obstacle look-ahead distance increases with its current speed.
- It uses 4 meters as the minimum obstacle look-ahead distance.
- The exact relationship between speed and look-ahead distance still needs
  to be defined.
- When an obstacle is detected, it performs several lightweight trajectory
  simulations with small offsets to the left and right.
- Those simulations include combined lateral and upward offsets, allowing the
  drone to climb while moving left or right around a three-dimensional obstacle.
- The player and other creatures count as obstacles for these simulations.
- Character avoidance uses the camera's 0.75-meter radius. If every lateral route is
  blocked, the drone retreats instead of continuing through the character.
- It chooses a lateral trajectory only when that trajectory avoids the first
  detected obstacle.
- If neither side avoids the obstacle, the drone keeps moving and may pass
  through it. It must not become blocked.
- Every simulated segment must also maintain at least 2 meters of terrain
  clearance at every checked point.
- The drone starts climbing when the projected segment would violate the
  minimum terrain clearance.
- The drone descends smoothly when it is higher than necessary.

## Environment zones

The drone recognizes two environment zones:

- `Forest`: at least two trees are present within 10 meters of the drone.
- `OpenArea`: fewer than two trees are present within 10 meters of the drone.

The zones define these flight limits:

| Zone | Maximum height | Orbit radius |
| --- | ---: | ---: |
| `Forest` | 4 m | 1–3 m |
| `OpenArea` | 8 m | 1–8 m |

### Framing and horizon

- The horizon always remains level.
- The camera never uses roll to follow or frame the player.
- The drone always keeps the player inside the camera field of view.
- When the drone is high, the player may appear near the bottom of the frame.
- If the player approaches the edge of the frame, the drone descends smoothly
  to restore safe framing.
- The drone adapts its height to keep the player visible.
- The camera may use a slight upward or downward pitch when required to keep
  the player inside the frame.
- Camera pitch is limited to 25 degrees above or below the horizon.
- The pitch limit takes priority over keeping the player visible.
- If the player leaves the field of view, the drone quickly returns to a
  nearby recovery position while preserving smooth acceleration and rotation.

### OrbitFlight

- The drone moves slowly around a player whose horizontal movement remains
  low.
- Its flight speed is 0.5 meters per second.
- It always looks at the player.
- It uses the same 100 ms terrain and obstacle anticipation policy as
  `TrailingFlight`.
- Its obstacle probe follows a two-second tangent projection of the future
  orbit instead of only checking the very short immediate-target segment.
- Terrain altitude uses the maximum clearance required by eight future path
  samples, then smooths that reference over time.
- Terrain-driven climbs react faster than descents, while descents use a
  longer smoothing interval to avoid vertical oscillation.
- Vertical flight speed is explicitly limited to 0.4 meter per second.
- Vertical motion uses a separate non-overshooting damped controller, so
  downward momentum cannot carry the camera below its head-height target and
  trigger a repeated correction cycle.
- It switches to `TrailingFlight` when its horizontal distance from the
  player reaches 5 meters.
- The transition from `OrbitFlight` to `TrailingFlight` is gradual and must
  not cause a visible change in position, speed, acceleration, or rotation.
- `OrbitFlight` is active only while the drone can remain near the player at
  its orbit speed of 0.5 meters per second.
- Reaching 4 meters is not enough by itself to return to `OrbitFlight` when
  the player is still moving too quickly for orbit speed.
- The drone returns to `OrbitFlight` only after it is within 4 meters and can
  remain nearby at orbit speed.
- The transition between both flight modes remains gradual.
- Its horizontal orbit radius evolves gradually within the active environment
  zone limits.
- It prefers the player's head altitude in world space.
- That preferred altitude is clamped from the terrain directly below the
  drone, so slopes still respect local clearance and maximum-height rules.
- A new orbit starts from the drone's current radius, clamped to the active
  zone, and changes radius by at most 0.1 meter per second so radial movement
  does not hide the circular motion.
- It stays at least 0.5 meters above the terrain.
- Its 0.75-meter collision radius raises its effective center height to at
  least 0.75 meters above the terrain.
- Its terrain clearance increases smoothly from 0.5 to 2 meters as its speed
  rises, reaching the full clearance at 3 meters per second.
- When it is less than 3 meters horizontally from the player, it stays no
  higher than 2 meters above the terrain.
- It stays below the maximum height of the active environment zone.
- When an orbit session begins, the drone chooses one rotation direction.
- It keeps that direction for the entire orbit session.
- Each new orbit session uses the opposite direction from the previous orbit
  session.
- It prefers the area in front of the player without forbidding other orbit
  positions: it slows smoothly in front and moves faster behind the player.

## Controls

- `F8` enters or leaves `CaptureMode`, which records video.
- `Shift+F8` enters or leaves `PreviewMode`, which displays the drone camera
  without recording.
- PreviewMode warms its camera for at least 0.5 seconds and 8 rendered frames
  while the gameplay camera remains visible, preventing a flash on transition.
- `Escape` leaves `PreviewMode` without opening Valheim's menu.
- Leaving PreviewMode uses FreeFly's three-second eased return: it first looks
  toward the player, then aligns with the gameplay camera.
- The interface remains hidden until that return transition is complete.
- `Escape` does not stop `CaptureMode`; it retains its normal Valheim behavior.
- The shortcuts must remain configurable through BepInEx.

## Configuration

- Reload configuration changes while the game is running.
- Show an in-game message when the configuration is reloaded.
- Write configuration reload events to the BepInEx log.

## Recording

- Keep the secondary-camera creation infrastructure.
- Start the secondary camera from the gameplay camera's copied pose.
- Keep it synchronized with the gameplay camera throughout recording warmup.
- Copy the gameplay camera pose once more immediately before flight begins.
- Begin autonomous flight only after warmup completes and recording starts.
- Preserve the copied camera inclination when flight begins, then rotate
  smoothly toward normal drone framing.
- Keep video recording through UnityRuntimeCameraRecorder.
- Save videos in the user's Windows `My Videos` directory.
- Use medium recording quality by default.
- Use 30 FPS as the default maximum frame rate.
- Accept a configured maximum frame rate from 30 to 60 FPS.

## Current reset state

- Previous autonomous flight behaviors were removed.
- The secondary camera currently follows the gameplay camera directly.
- New drone movement and camera behavior will be designed from this foundation.
