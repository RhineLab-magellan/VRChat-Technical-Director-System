
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CameraDataSYNC : UdonSharpBehaviour
{
    private Camera CAM;
    [UdonSynced]private float fov;
    void Start()
    {
        CAM = this.gameObject.GetComponent<Camera>();
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "SYNC");
    }

    public void SYNC()
    {
        fov = CAM.fieldOfView;
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        CAM.fieldOfView = fov;
    }
}
