
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PanelPickUpControl : UdonSharpBehaviour
{
    private VRC_Pickup pickup;
    private VRCPlayerApi LocalPlayer;

    private Transform Local;

    private Vector3 DefectV;
    private Quaternion DefectQ;

    public MeshRenderer MeshRenderer;

    private Vector3 LocalPosition;

    private bool Temp = false;

    private bool PickUp = false;
    private string[] Interaction = new string[2] { "World Position", "Player Position" };

    private Color[] Colors = new Color[2] { Color.red, Color.green };

    public Vector3 Defect;

    private short Mode = 0;
    void Start()
    {
        LocalPlayer = Networking.LocalPlayer;
        pickup = this.GetComponent<VRC_Pickup>();
        //MeshRenderer = this.GetComponent<MeshRenderer>();
        Local = this.transform;
        Defect = Local.localScale;
        DefectQ = Local.rotation;
        DefectV = Local.position;
    }
    public void ReturnTo()
    {
        Local.SetPositionAndRotation(DefectV, DefectQ);
    }
    public override void OnPickupUseDown()
    {
        if (Mode == 0)
        {
            Mode = 1;
            Temp = true;
        }
        else if (Mode == 1)
        {
            Mode = 0;
            Temp = false;
        }
        MeshRenderer.material.color = Colors[Mode];
        pickup.UseText = Interaction[Mode];
    }
    public void SetPlaneDefect()
    {
        Local.localScale = Defect;
    }

    public void UseWorldPosition()
    {
        Mode = 0;
        Temp = false;
        LocalPosition = Local.position - LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin).position;
        MeshRenderer.material.color = Colors[Mode];
        pickup.UseText = Interaction[Mode];
    }

    public void UsePlayerPosition()
    {
        Mode = 1;
        Temp = true;
        LocalPosition = Local.position - LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin).position;
        MeshRenderer.material.color = Colors[Mode];
        pickup.UseText = Interaction[Mode];
    }

    public void Update()
    {
        if (PickUp)
        {
            return;
        }
        if (Temp)
        {
            Local.position = LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin).position + LocalPosition;
        }
    }

    public override void OnPickup()
    {
        PickUp = true;
    }
    public override void OnDrop()
    {
        LocalPosition = Local.position - LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin).position;
        PickUp = false;
    }
}
