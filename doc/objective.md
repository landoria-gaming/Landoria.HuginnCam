# objective.mp4 specifications

Measured with FFprobe 9.0.1.

## File

| Property | Value |
|---|---|
| Container | MP4 / QuickTime MOV |
| Major brand | `mp42` |
| Compatible brands | `isom`, `mp42` |
| Streams | 1 video, 1 audio |
| Duration | 13.610767 seconds |
| Size | 114,642,002 bytes (109.33 MiB) |
| Total bitrate | 67.383 Mbit/s |
| Start time | 0 seconds |
| Creation time | 2026-09-15 19:36:42 UTC |
| SHA-256 | `182FB87C798C9AB412EBAE7E5D62E2CF6E1DDCA1C6DCB0DFA7C57E943FB01ADE` |

## Video

| Property | Value |
|---|---|
| Codec | H.264 / AVC |
| Codec tag | `avc1` |
| MIME codec | `avc1.640033` |
| Profile | High |
| Level | 5.1 |
| Resolution | 3840 × 2160 |
| Display aspect ratio | 16:9 |
| Pixel aspect ratio | 1:1 |
| Scan | Progressive |
| Pixel format | `yuv420p` |
| Chroma subsampling | 4:2:0 |
| Chroma location | Left |
| Bit depth | 8-bit |
| Color range | Limited / TV |
| Color primaries | BT.709 |
| Transfer characteristics | BT.709 |
| Color matrix | BT.709 |
| Nominal frame rate | 60 FPS |
| Average frame rate | 59.9525 FPS |
| Time base | 1/90000 second |
| Duration | 13.610767 seconds |
| Frame count | 816 |
| Video bitrate | 67.185 Mbit/s |
| B-frames | None |
| I-frames | 28 |
| P-frames | 788 |
| Extradata | 41 bytes |

## Frame timing

| Property | Value |
|---|---|
| Average interval | 16.6799 ms |
| Minimum interval | 16.4000 ms |
| Maximum interval | 26.2110 ms |
| Typical interval | 16.6667 ms |
| Keyframe interval | Up to 30 frames |

The stream is effectively 60 FPS. Only one measured interval is significantly longer than the normal 16.67 ms interval.

## Audio

| Property | Value |
|---|---|
| Codec | AAC Low Complexity |
| Codec tag | `mp4a` |
| MIME codec | `mp4a.40.2` |
| Sample format | Planar float (`fltp`) |
| Sample rate | 48,000 Hz |
| Channels | 2 |
| Channel layout | Stereo |
| Audio bitrate | 191.997 kbit/s |
| Time base | 1/48000 second |
| Duration | 13.610771 seconds |
| AAC frame count | 631 |
| Initial padding | 0 |
| Track name | System sounds |

## Synchronization

Both streams start at 0 seconds. Audio is only 0.000004 seconds longer than video, so their measured end-time difference is approximately 4 microseconds.

## Target characteristics

The important characteristics to reproduce are:

- 3840 × 2160 output;
- stable 60 FPS timing;
- explicit BT.709 color metadata;
- 48 kHz stereo audio;
- audio and video starting together and ending at almost exactly the same time;
- a bitrate high enough to preserve detail during motion.
