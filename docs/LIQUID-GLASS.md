# Liquid Glass and native desktop blur

Liquid Glass combines a WPF foreground material with an experimental Windows Composition backing surface. Enable **Liquid Glass → Backdrop Blur (Windows)** in Settings. Changes apply immediately and persist. Glass Opacity changes the foreground tint, not the system blur radius; use roughly 35–60% to make the blurred desktop easier to see.

## Rendering

The existing transparent WPF window remains responsible for content, input and interaction. A separate non-activating, input-transparent native HWND uses a Windows Composition HostBackdropBrush. Its region follows the existing IslandSurface outline, including Notch shoulders, and its position follows the foreground in physical screen pixels. It sits directly below the foreground, hides when disabled or when the foreground is hidden, and is disposed when the island closes. Geometry updates reuse the existing UI; the music panel is not recreated.

The native HWND uses WS_EX_NOREDIRECTIONBITMAP, rather than the layered-window rendering path. A UI-thread DispatcherQueue supports desktop composition. The WPF material supplies tint, reflections, edge light and depth. It remains usable if native composition initialization or synchronization fails. No screenshots of the desktop are captured for this effect.

A previous experiment using DWMWA_SYSTEMBACKDROP_TYPE returned success but produced an unsuitable opaque rectangular backdrop on the test machine. The current HostBackdropBrush path visibly blurs the background and follows the clipped outline.

## Limits

Windows controls backdrop availability and blur strength. Transparency preferences, power settings, remote sessions and OS capabilities can change the result. Native blur is currently integrated with Liquid Glass. It does not implement optical refraction or a custom adjustable blur kernel. Very low tint opacity can reduce text readability on busy backgrounds.

## Validation

A real on-screen contrast scene with alternating black/white stripes and an orange region was inspected behind the actual IslandWindow, in capsule and Notch modes. Stripes behind the island were smoothed while foreground text and controls remained sharp. Outside the island, the original stripes remained sharp.

The existing executable regression suite covers settings, WPF loading and interactions, plus synthetic material renders at 100/125/150/200% on black, white, colored and text backgrounds. Those renders do not capture the native compositor. Mixed-DPI monitor transitions, remote desktop, lock/unlock and extended z-order behavior still require manual validation. This remains an experimental test build.

Checkpoint before the material work: 997c562.

## References

- [HostBackdropBrush](https://learn.microsoft.com/uwp/api/windows.ui.composition.compositor.createhostbackdropbrush)
- [DWM window attributes](https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)
- [Desktop composition interop](https://learn.microsoft.com/windows/win32/api/windows.ui.composition.interop/nn-windows-ui-composition-interop-icompositordesktopinterop)

## Animated opening regression

The native HWND and Composition visual now retain the foreground window's full client size during capsule transitions. The clipping region alone follows the animated outline, with capsule coordinates translated into host coordinates. This avoids exposing pixels beyond the compositor's previous allocation when the capsule expands.

`GlassTests.StableBackdropDuringTransition` checks that the native host stays visible and retains identical screen bounds throughout opening and closing, for capsule and Notch. The test fails against the preceding implementation (`Native backdrop resized or disappeared during opening`) and passes with the stable host. On-screen intermediate frames were also inspected against the stripe scene; automated HWND checks do not themselves measure blur pixels. Set `NOTCHFLOW_BACKDROP_PROBE=1` and `NOTCHFLOW_PROBE_ANIMATE=1` on the test executable for the repeating visual probe; add `NOTCHFLOW_PROBE_NOTCH=1` for Notch.
