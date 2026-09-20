# SagaCapture

SagaCapture records Valheim gameplay from a configurable secondary camera.

## Current foundation

- Press `F8` to enter or leave `CaptureMode`.
- Press `Shift+F8` to enter or leave `PreviewMode`.
- Press `Escape` to leave `PreviewMode` without opening Valheim's menu.
- In `CaptureMode`, `Escape` keeps its normal Valheim behavior and recording
  continues.
- Save MP4 recordings to the Windows `My Videos` directory.
- Reload the BepInEx configuration when it changes on disk.
- Configure recording quality and a frame-rate limit from 30 to 60 FPS.
- Configure `SagaCameraFOV` from 40 to 120 degrees; its default is 65.

The secondary camera currently follows the gameplay camera directly. New camera
behavior will be implemented on top of this minimal foundation.

FFmpeg must be available through the `FFMPEG_PATH` environment variable, which
must point to a directory containing `ffmpeg.exe`.

## Contact

Report bugs through
[GitHub Issues](https://github.com/landoria-gaming/Landoria.SagaCapture/issues).
