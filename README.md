# SagaCapture

SagaCapture records your Valheim adventures as an automatically directed MP4.
Unlike a traditional screen recorder, it does not keep a single view for the
whole video. It switches between the gameplay camera and a moving cinematic
camera that follows your character, frames the action, and avoids terrain and
obstacles.

## Requirements

- Windows x64.
- An NVIDIA GPU with NVENC support and a recent NVIDIA driver.

  To check your GPU, run `nvidia-smi` in Command Prompt and find its model in the
  [NVIDIA Video Encode and Decode GPU Support Matrix](https://developer.nvidia.com/video-encode-and-decode-gpu-support-matrix-new).
  Check that `H.264 (AVC) YUV 4:2:0` is marked `YES`.

- [FFmpeg](https://ffmpeg.org/download.html), which is used to create the final
  MP4 file. It is not bundled and must be installed separately.

Set environment variable `FFMPEG_PATH` to the absolute path of the FFmpeg `bin`
folder containing `ffmpeg.exe` before starting Valheim. For example, in
PowerShell:

```powershell
$env:FFMPEG_PATH = "C:\tools\ffmpeg\bin"
```

## Record a video

Press `F8` to start or stop video recording.

Videos are saved in the **SagaCapture** folder inside your Windows **Videos**
folder. The folder is created automatically.

## Settings

Open this BepInEx file for recording settings:

```text
BepInEx/config/Landoria.SagaCapture.cfg
```

| Section | Setting | Default | Description |
| --- | --- | --- | --- |
| `Controls` | `CaptureModeShortcut` | `F8` | Starts or stops Capture Mode. |
| `CinematicCameraRendering` | `GraphicsPreset` | `SameAsGame` | Sets the cinematic-camera graphics preset: `SameAsGame`, `VeryLow`, `Low`, `Medium`, or `High`. |
| `CinematicCameraRendering` | `Resolution` | `SameAsGame` | Sets the cinematic-camera resolution: `SameAsGame`, `HD720`, `FullHD1080`, `QHD1440`, or `UHD2160`. |
| `CinematicCameraRendering` | `MaximumFrameRate` | `60` | Limits capture to `30` or `60` FPS. |
| `GameplayCamera` | `IncludeUI` | `true` | Includes the Valheim interface in gameplay shots. |

## Contact

Report bugs through
[GitHub Issues](https://github.com/landoria-gaming/Landoria.SagaCapture/issues).
