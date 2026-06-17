
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class FlyCameraVRTracking : UdonSharpBehaviour
{
    private Vector3 VRDronePosition;
    private Transform DroneTransform;
    void Start()
    {
        DroneTransform = transform;
        VRDronePosition = DroneTransform.position;
    }

    public override void OnPickupUseDown()
    {
        VRDronePosition = DroneTransform.position;
    }
    public override void OnDrop()
    {
        DroneTransform.position = VRDronePosition;
    }


}
