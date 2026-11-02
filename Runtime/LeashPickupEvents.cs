// Copyright (C) 2026 Nommiiee
using UdonSharp;
using VRC.SDKBase;

namespace OpenSourceCollarAndLeash
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class LeashPickupEvents : UdonSharpBehaviour
    {
        public LeashController controller;
        public bool isCollarPickup = true;
        [UdonSynced, UnityEngine.HideInInspector] public int handleGeneration;

        public override void OnPickup()
        {
            if (controller == null || !Utilities.IsValid(Networking.LocalPlayer) || !isCollarPickup) return;
            Networking.SetOwner(Networking.LocalPlayer, controller.gameObject);
            controller.SendCustomEvent("ReleaseLeash");
        }

        public override void OnDrop()
        {
            if (controller == null || !isCollarPickup || !Utilities.IsValid(Networking.LocalPlayer)) return;
            Networking.SetOwner(Networking.LocalPlayer, controller.gameObject);
            controller.SendCustomEvent("AttachNearestPlayer");
        }
    }
}
