# Open Source Collar And Leash

A configurable VRChat **world pickup** project by Nommiiee. It creates a collar pickup, a leash handle pickup, and a world-space line anchored to a nearby player's avatar. It does not modify or attach anything to the avatar prefab. The release contains the implementation, Unity editor setup tool, asset builder, and documentation.

## How it works

- Drop the collar pickup near another player's neck. The controller selects the nearest remote player inside the configured radius and synchronizes that player's ID.
- The collar tracks the target's Neck bone, then Head, then avatar-root tracking data with a height offset fallback.
- A separate handle pickup appears near the collar. Its pickup/object-sync transform controls the loose end of the leash.
- On attachment, the handle is positioned and marked with a synchronized generation before leash correction is allowed. The target client waits for that marker, preventing a stale initial handle transform from teleporting the player during attachment.
- A world-space `LineRenderer` draws a quadratic curve. The line sags while it is shorter than the configured leash length and straightens when stretched.
- Picking up the collar releases the target. The collared player cannot pick up either the collar or leash handle; other players can pick up the collar to release them. An optional respawn event also releases them.
- Local movement correction is enabled by default. Each frame, the target's own client checks the leash span and teleports them back to the leash boundary if they exceed it. The correction preserves their VR playspace offset, uses a ground raycast where available, and reduces downward velocity while corrections are occurring.

The system is modular: `LeashController` owns target, rendering, and optional movement behavior; `LeashPickupEvents` forwards collar pickup/drop events. The prefab builder creates a procedural collar mesh and default materials. Replace the generated meshes/materials freely.

## Dependencies and compatibility

- Unity **2022.3.22f1** and a VRChat Worlds SDK project are the recommended baseline for the current VRChat SDK line as checked on 2026-11-02. VRChat's current documentation says the SDK uses Unity 2022.3; do not upgrade a VRChat project to Unity 6. See [Current Unity Version](https://creators.vrchat.com/sdk/upgrade/current-unity-version/) and the [SDK 3.10.6 release](https://creators.vrchat.com/releases/release-3-10-6/).
- Requires VRChat Worlds SDK 3 and the UdonSharp package supplied by Creator Companion. This code uses `UdonSharpBehaviour`, `VRCPickup`, `VRCObjectSync`, player bone/tracking APIs, synced variables, and Udon player events.
- No custom shaders, PhysBones, VRC constraints, animation controllers, avatar menus, or expression parameters are required. This is a world prop and does not modify avatar assets.
- VRChat distinguishes world content and avatar content; use this package in a Worlds SDK project. See [VRChat Creator Getting Started](https://creators.vrchat.com/getting-started/).

## Import and build the prefab

1. Create a VRChat Worlds project with Creator Companion, Unity 2022.3, Worlds SDK, and UdonSharp installed.
2. Copy the `Open Source Collar And Leash` folder into the project's `Assets` folder.
3. To create the distributable, select `Assets/Open Source Collar And Leash` in the export project's Project window, choose **Assets > Export Package**, keep **Include dependencies** enabled, and save the `.unitypackage`.
4. In the destination Worlds project, choose **Assets > Import Package > Custom Package**, select the exported file, and import its contents. Wait for scripts to compile and UdonSharp import to finish.
5. Open **Tools > Open Source Collar And Leash**. The setup window lets you select an optional collar mesh, leash color, length, width, attach radius, and the number of instances to create in the active scene.
6. Leave the mesh empty or use **Use Defaults** to build with the generated collar ring and default leash settings. The instance count defaults to one and is clamped to at least one.
7. Click **Build and Add to Scene**. This creates or replaces files inside the **Open Source Collar And Leash** project folder, including `Prefabs/GeneratedCollarAndLeash.prefab` and collar/leash materials, then adds the requested prefab instances to the active scene. Instances are spaced apart; copies start near the selected scene object, or at the origin when nothing is selected.
8. If the tool creates missing UdonSharp program assets, wait for compilation and click **Build and Add to Scene** again.
9. Build and test in VRChat with two clients before uploading.

The editor builder creates missing paired UdonSharp program assets under `Runtime`, waits for UdonSharp compilation, then builds the prefab with UdonSharp-aware component creation. When no collar mesh is selected it uses the generated procedural ring. It validates program sources and component references and adds the requested prefab count to the active scene. Keep the full project folder together; moving only the prefab breaks its script, mesh, and material references.

## Configure interactions

- Set **Attach Radius** in metres. The dropped collar attaches to the nearest remote player within this radius; local self-attachment is intentionally skipped.
- Set **Leash Length**, **Line Segments**, **Sag Factor**, and **Maximum Sag** on `LeashController`. On attachment, the handle spawns in front of the player at half the configured leash length.
- Keep **Enable Local Correction** enabled for a working leash. The target's local client receives `TeleportTo` corrections as soon as its per-frame check finds the collar-to-handle span exceeds the configured length. Set it to disabled for a visual-only leash. The handler's client cannot move the remote player directly; the target client runs the correction.
- **Release On Respawn** is enabled by default. Disable it to keep the collar attached and tracking the player after they respawn. Picking up the collar still releases the target.
- Set the line material and width on the prefab's `LineRenderer`; replace the generated collar material or mesh for your world style.
- The handle is a separate `VRCPickup` with `VRCObjectSync`. Add decorative child meshes under the handle, and ensure they do not add colliders that block pickup interaction.

There are no menus, animator parameters, or synced avatar expressions. The target player ID is synchronized by the controller.

## Limitations

- The leash is a rendered line with target-side teleport correction, not a physically simulated rope or constraint. The target's client performs the correction; the handler's client cannot directly move a remote player.
- Bone availability, avatar-root fallback alignment, player teleport behavior, pickup ownership, and object synchronization vary with runtime/avatar/client conditions.
- Immediate teleport correction may feel abrupt and has not been tested in a live instance. Ground raycasts use a fixed layer mask, so worlds with different ground layers may need that mask adjusted.
- The generated meshes are basic placeholders intended for customization, not production artwork.
- Generated prefab creation and VRChat runtime behavior require the matching Unity editor and SDK packages.

This project is licensed under the GNU General Public License, version 3 only. See [LICENSE](LICENSE) for the license text.
