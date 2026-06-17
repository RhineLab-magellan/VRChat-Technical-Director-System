
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace RhineLab.VRCD.TechnicalDirector.CaneraSpace
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class CameraSpace : UdonSharpBehaviour
    {
        [UdonSynced] public Vector3 Position = Vector3.zero;

        private bool IsRunning = false;

        private Transform Camera;
        [UdonSynced] public Quaternion Rotation = Quaternion.identity;

        public float CameraSpeed = 0.5f;



        void Start()
        {
            Camera = this.transform.GetChild(0).transform;
            OnDeserialization();
        }

        public void CallNetworkSerialization()
        {
            if (!IsRunning)
            {
                Networking.SetOwner(Networking.LocalPlayer, Camera.gameObject);
                IsRunning = true;
            }
            OnDeserialization();
        }

        public void CheckOwner()
        {
            if (Networking.IsOwner(Camera.gameObject))
            {
                IsRunning = true;
            }
            else
            {
                IsRunning = false;
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player == Networking.LocalPlayer)
            {
                IsRunning = true;
            }
            else
            {
                IsRunning = false;
            }
        }
        public void UpdateValue()
        {
            OnDeserialization();
        }

        public override void OnDeserialization()
        {
            Camera.localPosition = Position;
            Camera.localRotation = Rotation;
        }

        public void GetTransform()
        {

        }
    }
}
