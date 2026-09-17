# HuginnCam

Snapshot builds use LandoriaModActions@v4.5 and the shared private references.
Requires MOD_REFERENCES_TOKEN. All five versions must match; only manifest and informational version use -snapshot.

HuginnCam records Valheim gameplay from an independent cinematic camera.

## Current features

- Press F8 to start recording and press F8 again to stop.
- Save full-resolution PNGs of the prepared cinematic frame and the player's complete screen beside each recording immediately before capture begins.
- Show `Recording...` in white below the minimap while recording is active.
- Capture a secondary camera at the active game resolution without changing the gameplay view.
- Follow five metres behind and two metres above the player's head while always looking at the player.
- Pull the camera closer when terrain, vegetation, or structures block its view.
- Warm up the cinematic camera before capture so its own automatic exposure is stable.
- Save recordings automatically to the Windows Desktop as H.265 MP4 files.
- Record at up to 60 frames per second using an available hardware encoder.
- Use a high-quality H.265 profile intended for editing and archival footage.
- Record Unity audio from the cinematic camera's position.
- Send Direct3D 11 textures directly to NVENC on supported NVIDIA systems.
- Run entirely on the client without a server installation.

FFmpeg must be installed separately. Set its full path in `Landoria.HuginnCam.cfg`, or leave the setting empty to resolve `ffmpeg.exe` from the Windows `PATH`.

Reusable Unity capture is provided by `UnityMediaRecorder.dll`. Generic FFmpeg transport and output creation are provided by `FFmpegMediaWriter.dll`. GPU video encoding is provided by `Direct3DVideoEncoder.dll`, which has no dependency on Valheim, Unity, BepInEx, or FFmpeg.

HuginnCam records directly to very-high-quality H.265 4:4:4 at CQ 8 through NVENC on NVIDIA systems. A three-surface GPU pool lets Unity render new frames while the previous frames are encoded. FFmpeg combines that stream with lossless 32-bit float PCM audio in a crash-resistant `*.mkv.tmp` container. After recording stops, FFmpeg copies the already-compressed video without re-encoding it and encodes the final MP4 audio once as 320-kbit/s AAC. The temporary MKV is deleted after successful MP4 creation unless `KeepIntermediateFile` is enabled in the BepInEx configuration. Other GPUs use a lossless intermediate followed by hardware H.265 compression.

`GeneratePreviewImage` controls whether the cinematic-camera PNG and gameplay-screen PNG are saved next to the video. It is enabled by default.

`AntiAliasingSamples` controls cinematic-camera MSAA and accepts `1`, `2`, `4` or `8`. It defaults to `4`; the multisampled image is resolved on the GPU before NVENC encoding.

Audio is captured directly from Valheim's Unity audio mix as 32-bit float samples before FFmpeg encodes it to AAC. Unity supports one active audio listener, so the player hears the cinematic camera's audio perspective while recording.

Example:

```ini
[Recording]
FfmpegPath = C:\ffmpeg\bin\ffmpeg.exe
```

## Planned highlights

- Switch smoothly between wide shots, tracking shots, close-ups, and overhead views.
- Show the recorded view in a small in-game preview.

## Controls

| Control | Action |
|---|---|
| `F8` | Start or stop recording |

## BepInEx configuration

| Setting | Default | Description |
|---|---|---|
| `FfmpegPath` | empty | Full path to `ffmpeg.exe`; an empty value uses the Windows `PATH`. |

## Valheim compatibility

Target: Valheim 1.0.x

## Contact

Report bugs through [GitHub Issues](https://github.com/landoria-gaming/Landoria.HuginnCam/issues).
