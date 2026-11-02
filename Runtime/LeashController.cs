// SPDX-License-Identifier: GPL-2.0-only
// Copyright (C) 2026 Nommiiee
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace OpenSourceCollarAndLeash
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class LeashController : UdonSharpBehaviour
    {
        [Header("Scene references")]
        public VRCPickup collarPickup;
        public VRCPickup handlePickup;
        public VRCObjectSync handleObjectSync;
        public LeashPickupEvents handleEvents;
        public LineRenderer leashRenderer;

        [Header("Attachment")]
        [Tooltip("Maximum world-space distance from a dropped collar to a remote player's neck, in metres.")]
        [Min(0.05f)] public float attachRadius = 0.4f;
        [Tooltip("Release the target when their local player respawns. Disable this to keep the collar attached through respawn.")]
        public bool releaseOnRespawn = true;

        [Header("Leash appearance")]
        [Min(0.25f)] public float leashLength = 2.5f;
        [Range(4, 32)] public int lineSegments = 16;
        [Tooltip("Sag depth grows with leash length while the line is slack.")]
        [Range(0f, 0.5f)] public float sagFactor = 0.12f;
        [Min(0f)] public float maximumSag = 0.75f;

        [Header("Movement correction")]
        [Tooltip("When enabled, the target's local client is immediately moved back inside the leash radius. Other clients cannot directly move them.")]
        public bool enableLocalCorrection = true;

        [UdonSynced] private int _targetPlayerId = -1;
        [UdonSynced] private int _handleGeneration;
        private VRCPlayerApi[] _players = new VRCPlayerApi[80];
        private Vector3[] _linePoints;
        private Rigidbody _collarBody;
        private Rigidbody _handleBody;
        private Vector3 _anchorPosition;
        private Quaternion _anchorRotation = Quaternion.identity;

        private void Start()
        {
            if (collarPickup != null) _collarBody = collarPickup.GetComponent<Rigidbody>();
            if (handlePickup != null) _handleBody = handlePickup.GetComponent<Rigidbody>();
            lineSegments = Mathf.Clamp(lineSegments, 4, 32);
            _linePoints = new Vector3[lineSegments + 1];
            if (leashRenderer != null)
            {
                leashRenderer.useWorldSpace = true;
                leashRenderer.positionCount = _linePoints.Length;
                leashRenderer.enabled = false;
            }
            RefreshPlayers();
            RefreshState();
        }

        public override void OnDeserialization(DeserializationResult result)
        {
            RefreshState();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            RefreshPlayers();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            RefreshPlayers();
            if (Utilities.IsValid(player) && player.playerId == _targetPlayerId && Networking.IsOwner(gameObject))
                ReleaseLeash();
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (!releaseOnRespawn || !Utilities.IsValid(player) || !player.isLocal || player.playerId != _targetPlayerId)
                return;
            Networking.SetOwner(player, gameObject);
            ReleaseLeash();
        }

        public override void PostLateUpdate()
        {
            if (_targetPlayerId < 0 || _collarBody == null || _handleBody == null) return;

            VRCPlayerApi target = VRCPlayerApi.GetPlayerById(_targetPlayerId);
            if (!Utilities.IsValid(target)) return;

            UpdateAnchor(target);
            _collarBody.position = _anchorPosition;
            _collarBody.rotation = _anchorRotation;

            if (enableLocalCorrection && target.isLocal && handleEvents != null &&
                handleEvents.handleGeneration == _handleGeneration)
                CorrectLocalTarget(target);

            DrawLeash(_anchorPosition, _handleBody.position);
        }

        public void AttachNearestPlayer()
        {
            if (!Utilities.IsValid(Networking.LocalPlayer)) return;
            RefreshPlayers();

            float radiusSquared = attachRadius * attachRadius;
            float nearestSquared = radiusSquared;
            VRCPlayerApi nearest = null;
            for (int i = 0; i < _players.Length; i++)
            {
                VRCPlayerApi candidate = _players[i];
                if (!Utilities.IsValid(candidate) || candidate.isLocal) continue;

                Vector3 candidateAnchor = GetNeckPosition(candidate);
                float distanceSquared = (candidateAnchor - collarPickup.transform.position).sqrMagnitude;
                if (distanceSquared <= nearestSquared)
                {
                    nearestSquared = distanceSquared;
                    nearest = candidate;
                }
            }
            if (!Utilities.IsValid(nearest)) return;

            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            PlaceHandle(nearest);
            _handleGeneration++;
            if (handleEvents != null) handleEvents.handleGeneration = _handleGeneration;
            _targetPlayerId = nearest.playerId;
            RequestSerialization();
            RefreshState();
        }

        public void ReleaseLeash()
        {
            if (Utilities.IsValid(Networking.LocalPlayer))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            _targetPlayerId = -1;
            RequestSerialization();
            RefreshState();
        }

        private void RefreshPlayers()
        {
            int playerCount = VRCPlayerApi.GetPlayerCount();
            if (playerCount > _players.Length) _players = new VRCPlayerApi[playerCount];
            VRCPlayerApi.GetPlayers(_players);
        }

        private void RefreshState()
        {
            bool attached = _targetPlayerId >= 0;
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            bool attachedToLocalPlayer = attached && Utilities.IsValid(localPlayer) &&
                                         localPlayer.playerId == _targetPlayerId;
            if (leashRenderer != null) leashRenderer.enabled = attached;
            if (handlePickup != null && handlePickup.gameObject.activeSelf != attached)
                handlePickup.gameObject.SetActive(attached);
            if (collarPickup != null) collarPickup.pickupable = !attachedToLocalPlayer;
            if (handlePickup != null) handlePickup.pickupable = !attachedToLocalPlayer;
        }

        private void PlaceHandle(VRCPlayerApi target)
        {
            if (_handleBody == null || handlePickup == null || !Utilities.IsValid(Networking.LocalPlayer)) return;
            // Enable the object before moving it so VRCObjectSync publishes this attachment's position,
            // rather than retaining the handle's previous inactive position.
            if (!handlePickup.gameObject.activeSelf) handlePickup.gameObject.SetActive(true);
            Networking.SetOwner(Networking.LocalPlayer, handlePickup.gameObject);
            Vector3 forward = target.GetRotation() * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            _handleBody.position = GetNeckPosition(target) + forward.normalized * (leashLength * 0.5f);
            if (handleObjectSync != null) handleObjectSync.FlagDiscontinuity();
        }

        private Vector3 GetNeckPosition(VRCPlayerApi player)
        {
            Vector3 neck = player.GetBonePosition(HumanBodyBones.Neck);
            if (neck != Vector3.zero) return neck;
            Vector3 head = player.GetBonePosition(HumanBodyBones.Head);
            if (head != Vector3.zero) return head;
            return player.GetTrackingData(VRCPlayerApi.TrackingDataType.AvatarRoot).position + Vector3.up * 1.35f;
        }

        private void UpdateAnchor(VRCPlayerApi player)
        {
            Vector3 neck = player.GetBonePosition(HumanBodyBones.Neck);
            if (neck != Vector3.zero)
            {
                _anchorPosition = neck;
                _anchorRotation = player.GetBoneRotation(HumanBodyBones.Neck);
                return;
            }
            Vector3 head = player.GetBonePosition(HumanBodyBones.Head);
            if (head != Vector3.zero)
            {
                _anchorPosition = head;
                _anchorRotation = player.GetBoneRotation(HumanBodyBones.Head);
                return;
            }
            var root = player.GetTrackingData(VRCPlayerApi.TrackingDataType.AvatarRoot);
            _anchorPosition = root.position + Vector3.up * 1.35f;
            _anchorRotation = root.rotation;
        }

        private void DrawLeash(Vector3 start, Vector3 end)
        {
            if (leashRenderer == null) return;
            float length = Vector3.Distance(start, end);
            float sag = length < leashLength ? Mathf.Min(maximumSag, (leashLength - length) * sagFactor) : 0f;
            Vector3 control = (start + end) * 0.5f + Vector3.down * sag;
            for (int i = 0; i < _linePoints.Length; i++)
            {
                float t = (float)i / (_linePoints.Length - 1);
                float inverse = 1f - t;
                _linePoints[i] = inverse * inverse * start + 2f * inverse * t * control + t * t * end;
            }
            leashRenderer.SetPositions(_linePoints);
        }

        private void CorrectLocalTarget(VRCPlayerApi target)
        {
            Vector3 playerRoot = target.GetTrackingData(VRCPlayerApi.TrackingDataType.AvatarRoot).position;
            Vector3 neckOffset = _anchorPosition - playerRoot;
            Vector3 handlePosition = _handleBody.position - neckOffset;
            Vector3 rootFromHandle = playerRoot - handlePosition;
            float rootDistance = rootFromHandle.magnitude;
            if (rootDistance <= leashLength || rootDistance < 0.001f) return;

            // Convert between avatar-root and room coordinates so the correction preserves the target's tracked playspace offset.
            var origin = target.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
            Vector3 playspaceOffset = origin.position - playerRoot;
            Vector3 naiveTarget = handlePosition + rootFromHandle / rootDistance * leashLength;

            // If there is ground between the target and the leash boundary, keep the correction on the ground side of it.
            Vector3 rayOrigin = naiveTarget;
            rayOrigin.y = handlePosition.y;
            Vector3 rayDirection = -neckOffset;
            RaycastHit[] hits = new RaycastHit[8];
            int hitCount = 0;
            if (rayDirection.sqrMagnitude > 0.0001f)
            {
                hitCount = Physics.RaycastNonAlloc(
                    new Ray(rayOrigin + neckOffset * 0.5f, rayDirection), hits, neckOffset.magnitude,
                    0b100000000001, QueryTriggerInteraction.Ignore);
            }

            Vector3 correctedRoot = naiveTarget;
            if (hitCount > 0)
            {
                Vector3 highestHit = hits[0].point;
                for (int i = 1; i < hitCount; i++)
                    if (hits[i].point.y > highestHit.y) highestHit = hits[i].point;

                float triangleHeight = highestHit.y - handlePosition.y;
                float heightSquared = leashLength * leashLength - triangleHeight * triangleHeight;
                if (heightSquared > 0f)
                {
                    float targetDistance = (highestHit - handlePosition).magnitude;
                    float allowedDistance = Mathf.Sqrt(heightSquared) - 0.002f;
                    if (allowedDistance > targetDistance + 0.375f)
                        allowedDistance = targetDistance - 0.375f;

                    Vector3 heightOrigin = handlePosition;
                    heightOrigin.y = highestHit.y;
                    Vector3 towardHit = highestHit - heightOrigin;
                    if (towardHit.sqrMagnitude > 0.0001f)
                        correctedRoot = heightOrigin + towardHit.normalized * allowedDistance;
                }
                else
                {
                    correctedRoot = highestHit;
                }
            }

            target.TeleportTo(correctedRoot + playspaceOffset, origin.rotation,
                VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);

            // Prevent downward velocity from accumulating during repeated boundary corrections.
            Vector3 velocity = target.GetVelocity();
            float gravityStrength = Physics.gravity.y * target.GetGravityStrength();
            if (velocity.y <= gravityStrength)
            {
                velocity.y = gravityStrength;
                target.SetVelocity(velocity);
            }
        }
    }
}









