# DronePilot extraction notes

This file records the requirements and decisions for the next SagaCapture changes.

## Requirements

- Name the new assembly `DronePilot.dll`.
- Put the new DLL's source in a separate `DronePilot` repository under the `UnityRuntimeCameraRecorder` GitHub organization. Commit and push the completed implementation when requested work is ready.
- This is a structural refactor of SagaCapture, not a flight redesign. Preserve the current behavior in Valheim.
- The drone DLL receives a Unity `Camera` to control and a target `GameObject` to follow.
- One optional world-axis aim offset defines the target point for both camera framing and orbit-height reference. By default, use the target center; SagaCapture supplies `(0, 1.25, 0)` above the Valheim player's world position.
- Its purpose is to fly the camera and keep the target in its field of view.
- Extract drone flight and camera movement into a separate DLL.
- The drone DLL may depend on Unity, but must not reference Valheim assemblies or types.
- Generalize the existing open-area and forest flight rules without changing their behavior; the drone must not know Valheim's `TreeBase` type.
- Support multiple pilot profiles in the drone DLL. The host provides the condition that selects each profile.
- Keep all recording concepts and dependencies out of the drone DLL.
- The drone DLL must not create or destroy the supplied camera.
- Obstacle detection may use an optional host-provided implementation that decides whether a detected Unity `Collider` should be ignored. The drone DLL must not hard-code vegetation names or Valheim types.

## Proposed boundary

- Drone DLL: flight modes, motion, framing, route planning, and obstacle queries through Unity types and injected world data.
- SagaCapture plugin: Valheim player data, terrain height, obstacle classification, UI, camera effects, and recording.
- Pass target motion and optional world-query data through Unity types or game-independent interfaces; do not pass `Player`, `ZoneSystem`, `Heightmap`, or `Character` types.
- Keep the drone DLL independent of BepInEx and the recorder. SagaCapture owns video capture, recording controls, and output files.
- SagaCapture owns camera creation, effects, activation, and lifetime; the drone DLL controls the supplied camera's flight and orientation.
- Replace `DroneEnvironment`'s `TreeBase` scan and `IsForest` flag with named pilot profiles supplied by the host. Each profile includes permitted flight height and orbit radius, and the host provides its selection condition.
- Let the Valheim adapter decide how nearby trees map to that environment data; preserve the current transition behavior.
- Keep pilot profiles separate from flight behaviors such as orbit and trailing: a profile supplies limits and tuning, while a behavior determines the movement pattern.
- Preserve the current flight-mode selection and timing while moving Valheim-specific profile conditions into the host.

## Decisions

- Implementation started after the user's approval. Keep this file as the
  feature and validation checklist until both repositories are verified.

## Part 2: Drone debug telemetry

- Keep the current camera-position and orbit-motion diagnostics, sampled every 0.5 seconds when debug logging is enabled.
- Buffer debug samples and events in memory; their internal representation does not have to use typed objects.
- Create one debug directory for each camera-control session.
- The caller supplies one telemetry configuration per session, including enabled state, root directory, sample interval, flush interval, and orbit-direction threshold. When BepInEx `RootDirectory` is empty, SagaCapture creates and supplies its profile's `BepInEx/config/SagaCapture` directory. `DronePilot.dll` creates `Sessions/<session>` inside the supplied root.
- A session begins when the drone DLL takes control of the camera and ends when it releases control, in either PreviewMode or CaptureMode.
- Every minute, write the buffered samples to a new JSON file containing an array of objects, then clear the successfully written samples from memory.
- Flush the remaining samples when the session ends so the final partial minute is not lost.
- Keep this telemetry independent of video recording; it describes drone flight, not recorder output.
- Replace the single SagaCapture `DebugLogs` setting with two BepInEx enabled flags: PreviewMode enabled by default and CaptureMode disabled by default. Keep telemetry root, sampling interval, flush interval, and orbit-direction threshold shared.
- SagaCapture selects one preset and passes it to the drone DLL when each session starts; the DLL must not depend on BepInEx or know the mode names.
- Preserve the current position fields: relative horizontal and vertical coordinates, world heights, horizontal distance, pitch, target viewport coordinates, and in-frame status.
- Preserve the current orbit-motion fields: direction, reason, tangential and radial speed, tangential acceleration, and target distance.
- Also record a UTC timestamp and elapsed session time, active flight mode and pilot profile, camera and target world positions, camera velocity and acceleration, target velocity, desired waypoint, and terrain clearance.
- Record obstacle-avoidance decisions and mode/profile changes as timestamped events, so short events between 0.5-second samples are not lost.
- Keep the JSON schema stable and use explicit field names and units.
- Minimize runtime overhead: collect only when debug telemetry is enabled, at the existing 0.5-second cadence, and reuse values already computed by flight control instead of adding physics queries.
- Avoid per-frame JSON serialization, disk I/O, and unnecessary string formatting. At each minute boundary, swap the active buffer and serialize/write the completed buffer off the Unity main thread.
- Copy all Unity-derived values into plain data before background work; never call Unity APIs from the writer thread.
- If writing falls behind, pause new debug collection rather than grow memory without limit; only discard an existing buffer after its JSON file has been written successfully.

## Part 3: Drone configuration

- Define the configuration consumed by the new drone DLL without making the DLL depend on BepInEx.
- Define a configuration object matching `drone-config.yaml`; serialize it to YAML and deserialize it from YAML. Validate the full object before the drone uses it.
- If the YAML file is missing, `DronePilot.dll` creates it with every default configuration value before starting flight. The generated file must preserve the template's useful documentation for each setting: purpose, unit, default, valid range when applicable, and the effect of higher or lower values. Embed the documented default YAML as an assembly resource so defaults and comments have one source of truth without code constants. Do not overwrite an existing file.
- Install the YAML at `BepInEx/config/SagaCapture/drone-config.yaml`; SagaCapture passes this path to `DronePilot.dll` for startup loading and hot reload.
- Check the YAML file for changes once per second while the drone is active. When changed, deserialize and validate a complete new configuration, then replace the active configuration atomically on Unity's main thread.
- Handle files being saved in multiple steps with a short retry, as the existing `ConfigWatcher` does. Do not expose a partially loaded configuration to flight code.
- A missing file at startup is generated from defaults. An existing invalid file at startup throws a descriptive error. An invalid hot reload reports an error and keeps the last valid configuration active; never silently repair the invalid file.
- SagaCapture remains responsible for its BepInEx settings and passes drone configuration to the DLL.
- The new drone DLL must declare no `const` settings and have no hard-coded flight or telemetry tuning values. Flight tuning comes from `drone-config.yaml`; pilot profiles, telemetry settings, and other runtime inputs come from the host.
- Keep current flight values as YAML defaults, including algorithm thresholds, sampling counts, timing, and route-offset choices. Keep current telemetry values as host-preset defaults.
- Keep forest detection radius, scan interval, and Valheim object classification in SagaCapture. The DLL receives the selected pilot profile and its limits.
- Group flight settings by flight-mode switching, trailing, orbit, motion, aiming, framing, and trajectory planning. Include internal numeric tolerances and route-offset arrays so no flight tuning value is hidden in the DLL.
- Validate every YAML value and all related constraints before flight starts (for example, minimum distance below maximum distance, blend start below blend end, orbit return distance below trailing entry distance, and emergency limits above normal limits). Any invalid configuration throws a descriptive error naming the offending YAML path; never clamp, repair, or silently substitute a value.
- Correct descriptions before publishing the configuration: trailing catch-up bonus is speed in m/s, the altitude sine rate is radians/second rather than Hz, orbit speed is tangential m/s rather than angular speed, character retreat distance is a fallback move rather than minimum separation, and the framing pitch is a recovery-distance calculation rather than a camera tilt setting.
- `drone-config.yaml` now contains the proposed DLL defaults; it is a specification only until the new DLL loads and validates it.
- The host supplies named pilot profiles at runtime; `drone-config.yaml` contains no Valheim-specific profile names or limits. It includes the current 1.5-times-maxSpeed trailing cap.
- `Configuration/Landoria.SagaCapture.cfg.example` is the proposed BepInEx template. It keeps the existing controls/camera/recording defaults, puts both telemetry enable flags and shared settings in `[Telemetry]`, and defines the two named Valheim pilot profiles. Forest's tree-density condition lives in `[PilotProfile.Forest]`.
- The host's shared telemetry settings retain 0.5-second sampling, 60-second JSON flushes, and the current 0.05 m/s orbit-direction threshold; `drone-config.yaml` contains no telemetry section.
- Optional flight features have `enabled: true` switches by default. Both flight modes, catch-up, terrain clearance, look smoothing, and lost-target recovery remain mandatory.
- Orbit flight has a separate reactive-obstacle-avoidance switch, disabled by default to preserve current behavior; planned orbit and terrain clearance remain active.
- The YAML now includes the currently hidden flight route offsets, speed/altitude blend thresholds, lateral variation, emergency distance, and numerical tolerances. Re-audit during extraction to catch any remaining tuning literals; telemetry-specific timing comes from the host's per-session options.

## Refactoring checks

- SagaCapture must keep using its current `(0, 1.25, 0)` focus point, terrain clearance, obstacle rules, and orbit/trailing transitions.
- The host retains camera creation, rendering, effects, audio, recording, and lifetime.
- Do not introduce new flight policies while extracting the DLL.
- No asset or prefab is loaded by name in flight code. Valheim-specific obstacle filtering uses `Heightmap`, `ItemDrop`, `Pickable`, `Character`, `TreeBase`, and hard-coded vegetation name fragments (`grass`, `bush`, etc.). The Valheim adapter must implement the optional collider-ignore rule so the current behavior is preserved.
- SagaCapture's collider-ignore implementation covers the current light-vegetation name fragments (`grass`, `bush`, `shrub`, etc.), `ItemDrop`, and `Pickable`. Terrain height (`Heightmap`) and character avoidance (`Character`) remain separate rules.
- Camera effect mirroring uses hard-coded component type names; keep it in SagaCapture, outside the drone DLL.
- Replace `Player.GetVelocity()` and `Player.m_runSpeed` with host-supplied `targetVelocity` (`Vector3`) and `maxSpeed` (`float`). Replace `Player.m_eye` in orbit planning with the same optional aim point used for framing; do not add a separate eye-height parameter.
- Replace `ZoneSystem.GetGroundHeight()` with a host-supplied terrain-height query; preserve current terrain sampling and clearance.
- Replace `Character` checks with a host-supplied obstacle classification so the existing retreat rule remains intact.
- Route flight diagnostics through an optional logging callback instead of `SagaCapturePlugin.Log`.
