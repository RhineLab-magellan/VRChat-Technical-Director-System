
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CameraDataSYNC : UdonSharpBehaviour
{
    private Camera CAM;
    [UdonSynced, HideInInspector] public float fov;

    [HideInInspector] public CameraViewControl cameraViewController;
    [HideInInspector] public int Index;

    private GameObject[] FOVObjects;
    void Start()
    {
        CAM = this.transform.parent.GetComponent<Camera>();
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "SYNC");
    }

    public void SYNC()
    {
        fov = CAM.fieldOfView;
        if (!Networking.IsOwner(this.gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        }
        RequestSerialization();
    }

    public void ReSync()
    {
        CAM.fieldOfView = fov;
        if (Networking.IsOwner(this.gameObject))
        {
            RequestSerialization();
        }
        if (cameraViewController != null)
        {
            if (Index == cameraViewController.SystemIndex)
            {
                cameraViewController.SystemOFF();
                cameraViewController.FOV = fov;
            }
        }

    }

    public override void OnDeserialization()
    {
        CAM.fieldOfView = fov;
    }
}
