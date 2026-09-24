# SagaCapture

SagaCapture records your Valheim adventures as an automatically edited MP4 with
game audio.

## Before playing

- Install BepInEx and SagaCapture in your Valheim profile.
- Install FFmpeg.
- Set `FFMPEG_PATH` to the folder that contains `ffmpeg.exe`.

## Record a video

1. Start or join a Valheim world.
2. Press `F8` to start Capture Mode.
3. Keep playing normally while SagaCapture records.
4. Press `F8` again to stop and save the video.

The current recording frame rate appears in the top-left corner while recording.
Pressing `Escape` does not stop the recording.

## Your video

- Videos are saved in your Windows **Videos** folder.
- SagaCapture alternates between your normal gameplay view and cinematic views.
- Cinematic views follow your character and avoid terrain and obstacles.
- If a safe cinematic position cannot be found, the gameplay view stays active.
- Interrupted recordings are recovered when possible.

## Settings

Open this BepInEx file for recording settings:

```text
BepInEx/config/Landoria.SagaCapture.cfg
```

You can change:

- the `F8` shortcut;
- recording resolution and graphics quality;
- the 30 or 60 FPS limit;
- whether gameplay shots include the Valheim interface.

The interface is excluded by default.

Camera movement settings are stored here:

```text
BepInEx/config/SagaCapture/config.yaml
```

Valid YAML changes are applied while recording. If a change is invalid,
SagaCapture keeps the last valid settings.

## Problems

- Make sure `FFMPEG_PATH` points to the folder containing `ffmpeg.exe`.
- Check the BepInEx log if recording does not start or a setting is rejected.
- Report bugs on [GitHub Issues](https://github.com/landoria-gaming/Landoria.SagaCapture/issues).
