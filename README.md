# SagaCapture

SagaCapture records Valheim gameplay from a configurable secondary camera.

## Controls

- Press `F8` to enter or leave `CaptureMode`.
- Press `Shift+F8` to enter or leave `PreviewMode`.
- Press `Escape` to leave `PreviewMode` without opening Valheim's menu.
- In `CaptureMode`, `Escape` keeps its normal Valheim behavior and recording
  continues.
- Save MP4 recordings to the Windows `My Videos` directory.
- Reload the BepInEx configuration when it changes on disk.
- Configure recording quality and a frame-rate limit from 30 to 60 FPS.
- Configure `SagaCameraFOV` from 40 to 120 degrees; its default is 65.

The secondary camera is flown by the independent
[DronePilot](https://github.com/UnityRuntimeCameraRecorder/DronePilot) library.
SagaCapture supplies Valheim terrain, obstacle filtering, player motion, and
forest/open-area pilot profiles. DronePilot owns orbit and trailing flight but
never creates the camera or records video.

Flight settings live in the documented
`BepInEx/config/SagaCapture/drone-config.yaml`. The DLL creates this file with
defaults when it is missing and reloads valid changes while flying. BepInEx
settings control recording, pilot profiles, and separate Preview/Capture
telemetry switches. Enabled telemetry writes one JSON array per interval under
`BepInEx/config/SagaCapture/Sessions` by default.

Create a local directory link at `mods/DronePilot` pointing to the DronePilot
repository before building. On Windows, a directory junction works when
symbolic-link privileges are unavailable. Then run:

```text
dotnet build Landoria.SagaCapture.csproj -c Release
```

The build merges DronePilot, its YAML/JSON libraries, and the managed recorder
libraries into `Landoria.SagaCapture.dll`. Unity and Valheim assemblies remain
external; the native `Direct3DVideoEncoder.dll` stays beside the plugin.

The visible drone is loaded from `assets/sagacapture-drone`. Its Unity build
project lives in the sibling `../SagaCapture.ModelBuild` directory. Building
that project copies the verified bundle into SagaCapture's `assets` directory.

FFmpeg must be available through the `FFMPEG_PATH` environment variable, which
must point to a directory containing `ffmpeg.exe`.

## Contact

Report bugs through
[GitHub Issues](https://github.com/landoria-gaming/Landoria.SagaCapture/issues).
