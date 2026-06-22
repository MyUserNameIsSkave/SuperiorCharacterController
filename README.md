# Superior Character Controller

A physics-based, sweep-and-slide character controller for Unity, with support for arbitrary gravity (walk on walls, ceilings, etc). It does **not** use Unity's built-in `CharacterController` or a `Rigidbody` for movement — it's a fully custom kinematic sweep-and-slide solution.


## Features

- **Simple, explicit input** — drive the character with a single `AskedLocalVelocity` (body-local) or `AskedWorldVelocity` (world-space) vector. No state machine or event wiring required to get moving.
- **Arbitrary gravity** — the controller reasons about movement relative to its own up axis, not a hardcoded world up, with a smooth reorientation when that axis changes. Gravity strength itself is just the `currentGravity` field (see [Driving the character](#driving-the-character)).
- **Step-up and ground snapping** — clears small ledges and keeps contact with stairs/slopes without bouncing (There are some issues on moving stairs).
- **Slope handling** — slide, climb, and preserve inertia when leaving a slope, with crest detection to launch over ridges instead of gluing to the descending face.
- **Dynamic surfaces** — ride moving and rotating platforms or walls (`SCC_DynamicSurface`), with full momentum hand-off on leave (position, rotation, launch detection). It doesn't matter what's actually moving the platform, animation, a script, physics orelse. `SCC_DynamicSurface` only reads the transform delta, so any of them works.
- **Rigidbody interaction** — optionally push non-kinematic rigidbodies and be slowed by them in return (`SCC_Pushable`, opt-in per object). This support is intentionally rudimentary: an approximate push/resistance model, not a full physical simulation — don't expect believable heavy-object physics.
- **Self-contained inspector** — a small set of custom attributes (`Tab`, `EnableIf`/`EndIf`, `ShowInInspector`, `MinMaxRangeSlider`) organize the layout without depending on a third-party inspector plugin.

## Installation

Download the `.unitypackage` from the [latest release](https://github.com/MyUserNameIsSkave/SuperiorCharacterController/releases/latest) and import it (**Assets > Import Package > Custom Package...**, or just double-click it).

> Installing via "Add package from git URL" is **not supported** — without Assembly Definitions, Unity resolves the Editor and Runtime code as separate, unlinked assemblies once it's treated as an actual package, which breaks compilation. The `.unitypackage` keeps everything under `Assets/` instead, where that split doesn't happen.


## Requirements

- Unity 2021.3 or newer
- [DOTween](http://dotween.demigiant.com/) (free version) — used for the up-axis reorientation tween. Import it separately; it isn't bundled, since it isn't distributed as a UPM package itself.


## Usage

### Setup

- Add `SCC_Controller` to your player's root GameObject. It will auto-create a `CapsuleCollider` if one isn't already present.
- Recommended hierarchy: keep the **root GameObject at ground/feet level** (this is what `SCC_Controller` and its `CapsuleCollider` live on), and parent your visual model/camera as a **child object, offset upward** to eye/head height. This keeps the root's transform a clean, predictable ground-contact point.
- Add `SCC_DynamicSurface` to any platform or wall you want the controller to ride correctly (position/rotation tracking, momentum hand-off).
- Add `SCC_Pushable` to a `Rigidbody` you want the controller to be able to push, and to push back against the controller.

### Driving the character

Two fields cover the whole input surface — no internal state machine to learn:

```csharp
scc.AskedLocalVelocity = new Vector3(strafe, jumpOrFall, forward); // body-local (x = right, z = forward, y = up)
scc.AskedWorldVelocity = someWorldSpaceVelocity;                   // world-space convenience setter/getter

scc.currentGravity = 9.81f; // gravity strength along the controller's own up axis, exposed in the Gravity tab (defaults to 45)
```

Set whichever velocity field is more convenient for the call site (e.g. `AskedWorldVelocity` for wall normals or camera-relative directions) — both read/write through the same underlying value. `currentGravity` is the second variable you're expected to drive: it has a sane inspector default (45) so the character falls out of the box, but the controller never re-derives it on its own — override it at runtime for anything dynamic (low-gravity zones, gravity guns, etc).


## Custom inspector attributes

Located under `Runtime/Attributes` (namespace `SuperiorCharacterController.Attributes`):

| Attribute | Purpose |
|---|---|
| `[Tab("Name", icon)]` | Starts a new inspector tab. `icon` is an optional built-in Editor icon name, shown instead of the tab text. |
| `[EnableIf("boolFieldName")]` / `[EndIf]` | Greys out this field, and every field after it, until the next `[EndIf]` or a new `[EnableIf]`. |
| `[ShowInInspector]` | Displays a private, non-serialized field as a read-only debug row. |
| `[MinMaxRangeSlider(min, max)]` | Draws a `Vector2` as a double-handle min/max slider. |


## AI disclosure

Parts of this package (notably the custom inspector attribute system, and editing/refactoring passes on the controller) were written with the assistance of an LLM (Claude, by Anthropic). The core movement/collision logic and design decisions are human-authored; the LLM was used as a coding assistant, not an autonomous author.

(And yes, this very README — including this sentence — was drafted by the same LLM. Turtles all the way down.)

## License

MIT — see [LICENSE](LICENSE).
