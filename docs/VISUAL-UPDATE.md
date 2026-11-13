# Audio-reactive rework

Audio analysis is off by default. Enable **Media Player → Enable system audio analysis** to use WASAPI endpoint loopback. This captures the default multimedia output mix, including other applications and notifications. It is not Process Loopback and is not isolated to the selected GSMTC media session. GSMTC identifies applications with an AppUserModelId, without a reliable process ID mapping for arbitrary applications and browser tabs. Process Loopback requires explicit PID/process-tree selection and a separate activation path.

Audio buffers remain in memory. Nothing is recorded or transmitted. The service uses NAudio 2.2.1 already bundled with the app, an explicit 48 kHz float stereo capture format, and a 2048-sample Hann-windowed FFT. Sixteen logarithmic bands cover 40 Hz–16 kHz. RMS, peak, bass/mid/treble energy and positive energy attacks drive visuals. A noise floor, bounded adaptive normalization, attack/release smoothing, sensitivity control, and stale-buffer silence handling prevent continuous activity in silence. UI updates are limited to approximately 30 Hz. Default device changes are checked every two seconds. Capture stops when disabled or the window closes.

Album Glow follows bass/onset energy with softer mid-level intensity. Breathing Island changes only its decorative inset and contour. Equalizer uses frequency bins instead of timed oscillations. Classic retains its original decorative indicators. Reduce Motion suppresses reactive movement.

Acrylic Glass and Liquid Glass are no longer offered. Existing saved glass selections render as Solid. No native desktop blur is claimed. The current per-pixel-alpha WPF HWND covers a larger rectangle than the island, while DWM backdrop applies to window bounds. A production native glass implementation needs a clipped native backing HWND coordinated with the foreground window for z-order, DPI, shape transitions and input. This update avoids an unverified replacement of that window architecture.

All optional XAML and dynamically assigned tooltips were removed. Intentional source menus, confirmations and settings dropdowns remain. Volume dragging now captures the slider itself from pointer-down through pointer-up, including clicks on the track. Volume notifications never replace an already expanded player. Collapse attempts record their initiating method and whether an interaction blocked them in LocalAppData/NotchFlow/interactions.log.

Validation includes controlled DSP tones, attacks, silence, native capture startup, input capture/release regressions, settings persistence, WPF rendering and application startup. Rendered screenshots use synthetic artwork and controlled analysis values. They are not evidence of physical pointer trajectories, mixed-DPI monitor transitions, or long-duration GPU performance.

References
- https://github.com/naudio/NAudio/blob/v2.2.1/Docs/WasapiLoopbackCapture.md
- https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/
- https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type
