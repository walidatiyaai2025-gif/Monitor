# Motion Specification

Allowed motion in M0:

- Live status pulse.
- Slow scan-track movement in the central live dashboard area.
- Soft light sweep on the live estate panel.
- Normal hover/transition feedback.
- Client-side clock/countdown changes.

Rules:

- No network request is triggered by animation.
- No SQL collection is triggered by animation.
- Avoid flashing, bouncing, large continuous movement and heavy animation libraries.
- Motion communicates system activity, not decoration.
