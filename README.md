# Open Source Collar And Leash

A configurable VRChat **world pickup** project by Nommiiee.

It creates a collar pickup, a leash handle pickup, and a world-space line anchored to a nearby player's avatar. It does not modify or attach anything to the avatar prefab.

The project includes the complete implementation, Unity Editor setup tool, asset builder, and documentation.

> **Pre-release notice:** This project is currently in pre-release. It has only been tested with a small number of players and has not yet undergone extensive multiplayer testing. Bugs, unexpected behavior, and compatibility issues may still exist.
>
> You can download the Unity package from the [GitHub Releases page](https://github.com/Nommiiee/Open-Source-Collar-And-Leash/releases) to try it out.
>
> If you encounter any issues, please report them through the [GitHub Issues tab](https://github.com/Nommiiee/Open-Source-Collar-And-Leash/issues). Feedback and testing reports are greatly appreciated.

## How It Works

- Drop the collar pickup near another player's neck. The controller selects the nearest remote player inside the configured radius and synchronizes that player's ID.
- The collar tracks the target's Neck bone, then Head, then avatar-root tracking data with a height offset fallback.
- A separate handle pickup appears near the collar. Its pickup/object-sync transform controls the loose end of the leash.
- On attachment, the handle is positioned and marked with a synchronized generation before leash correction is allowed. The target client waits for that marker, preventing a stale initial handle transform from teleporting the player during attachment.
- A world-space `LineRenderer` draws a quadratic curve. The line sags while it is shorter than the configured leash length and straightens when stretched.
- Picking up the collar releases the target. The collared player cannot pick up either the collar or leash handle; other players can pick up the collar to release them. An optional respawn event also releases them.
- Local movement correction is enabled by default. Each frame, the target's own client checks the leash span and teleports them back to the leash boundary if they exceed it. The correction preserves their VR playspace offset, uses a ground raycast where available, and reduces downward velocity while corrections are occurring.

The system is modular: `LeashController` owns target, rendering, and optional movement behavior; `LeashPickupEvents` forwards collar pickup/drop events.

The prefab builder creates a procedural collar mesh and default materials. Replace the generated meshes/materials freely.

## Dependencies and Compatibility

- Unity **2022.3.22f1** and a VRChat Worlds SDK project are the recommended baseline for the current VRChat SDK line.
- Requires VRChat Worlds SDK 3 and the UdonSharp package supplied by Creator Companion.
- This code uses `UdonSharpBehaviour`, `VRCPickup`, `VRCObjectSync`, player bone/tracking APIs, synced variables, and Udon player events.
- No custom shaders, PhysBones, VRC constraints, animation controllers, avatar menus, or expression parameters are required.
- This is a world prop and does not modify avatar assets.

For additional information:

- [VRChat Current Unity Version](https://creators.vrchat.com/sdk/upgrade/current-unity-version/)
- [VRChat SDK 3.10.6 Release](https://creators.vrchat.com/releases/release-3-10-6/)
- [VRChat Creator Getting Started](https://creators.vrchat.com/getting-started/)

## Download and Installation

### Option 1: Download the Unity Package

The easiest way to try the project is to download the pre-built `.unitypackage`.

1. Visit the [GitHub Releases page](https://github.com/Nommiiee/Open-Source-Collar-And-Leash/releases).
2. Open the latest pre-release.
3. Download `Open Source Collar And Leash.unitypackage` from the release assets.
4. Open your VRChat Worlds project in Unity.
5. Select **Assets > Import Package > Custom Package**.
6. Select the downloaded `.unitypackage` and import its contents.
7. Wait for scripts to compile and UdonSharp import to finish.
8. Open **Tools > Open Source Collar And Leash**.
9. Configure your preferences and click **Build and Add to Scene**.

If the tool creates missing UdonSharp program assets, wait for compilation and click **Build and Add to Scene** again.

### Option 2: Build from Source

1. Create a VRChat Worlds project with Creator Companion, Unity 2022.3, Worlds SDK, and UdonSharp installed.
2. Copy the `Open Source Collar And Leash` folder into the project's `Assets` folder.
3. To create the distributable, select `Assets/Open Source Collar And Leash` in the export project's Project window.
4. Choose **Assets > Export Package**, keep **Include dependencies** enabled, and save the `.unitypackage`.
5. In the destination Worlds project, choose **Assets > Import Package > Custom Package**, select the exported file, and import its contents.
6. Wait for scripts to compile and UdonSharp import to finish.
7. Open **Tools > Open Source Collar And Leash**.

## Build the Prefab

The setup window lets you configure:

- Optional custom collar mesh.
- Leash color.
- Leash length.
- Leash width.
- Player attachment radius.
- Number of instances to create in the active scene.

Leave the mesh empty or use **Use Defaults** to build with the generated collar ring and default leash settings. The instance count defaults to one and is clamped to at least one.

Click **Build and Add to Scene**.

This creates or replaces files inside the **Open Source Collar And Leash** project folder, including:

- `Prefabs/GeneratedCollarAndLeash.prefab`
- Generated collar mesh.
- Collar and leash materials.
- Required UdonSharp program assets.

The builder then adds the requested prefab instances to the active scene. Instances are spaced apart; copies start near the selected scene object, or at the origin when nothing is selected.

The editor builder creates missing paired UdonSharp program assets under `Runtime`, waits for UdonSharp compilation, then builds the prefab with UdonSharp-aware component creation.

It validates program sources and component references before adding the requested prefab count to the active scene.

**Important:** Keep the full project folder together. Moving only the prefab will break its script, mesh, and material references.

Build and test in VRChat with at least two clients before uploading your world.

## Configure Interactions

- **Attach Radius:** Set the radius in metres. The dropped collar attaches to the nearest remote player within this radius. Local self-attachment is intentionally skipped.
- **Leash Length:** Configure the maximum leash distance.
- **Line Segments:** Controls the smoothness of the rendered leash curve.
- **Sag Factor:** Controls the amount of leash sag.
- **Maximum Sag:** Sets the maximum allowed curve sag.
- **Enable Local Correction:** Enabled by default for a working leash. The target's local client receives `TeleportTo` corrections as soon as its per-frame check finds the collar-to-handle span exceeds the configured length. Disable this for a visual-only leash. The handler's client cannot move the remote player directly; the target client runs the correction.
- **Release On Respawn:** Enabled by default. Disable it to keep the collar attached and tracking the player after they respawn. Picking up the collar still releases the target.
- **Line Material and Width:** Configure these on the prefab's `LineRenderer`. Replace the generated collar material or mesh to match your world style.
- **Leash Handle:** The handle is a separate `VRCPickup` with `VRCObjectSync`. Add decorative child meshes under the handle, and ensure they do not add colliders that block pickup interaction.

On attachment, the handle spawns in front of the player at half the configured leash length.

There are no menus, animator parameters, or synced avatar expressions. The target player ID is synchronized by the controller.

## Limitations

- The leash is a rendered line with target-side teleport correction, not a physically simulated rope or constraint.
- The target's client performs the correction; the handler's client cannot directly move a remote player.
- Bone availability, avatar-root fallback alignment, player teleport behavior, pickup ownership, and object synchronization vary with runtime, avatar, and client conditions.
- Immediate teleport correction may feel abrupt and has not been extensively tested in a live instance.
- Ground raycasts use a fixed layer mask, so worlds with different ground layers may need that mask adjusted.
- The generated meshes are basic placeholders intended for customization, not production artwork.
- Generated prefab creation and VRChat runtime behavior require the matching Unity Editor and SDK packages.
- Multiplayer behavior has only been tested with a small number of players. Larger-instance behavior is not yet verified.

## Bug Reports and Feedback

This is an early pre-release, so bug reports, feedback, and testing are especially welcome.

If you encounter an issue, please report it through the [GitHub Issues tab](https://github.com/Nommiiee/Open-Source-Collar-And-Leash/issues).

When submitting a report, please include:

- A description of the issue.
- Steps to reproduce it.
- Any relevant Unity Console errors.
- The number of players involved.
- Any additional information that could help reproduce the issue.

Suggestions and contributions are also welcome through GitHub.

## License

This project is licensed under the **GNU General Public License v3.0 only (GPL-3.0-only)**.

See the [LICENSE](LICENSE) file for the full license text.

---

Thank you for checking out Open Source Collar And Leash.

Your feedback, testing, and contributions are appreciated!
