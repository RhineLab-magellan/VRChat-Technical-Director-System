
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

public class VRPickUp : UdonSharpBehaviour
{
    public GameObject CentreTransform;

    private Material material;

    private bool IsBack = false;

    private VRCObjectSync VRCObjectSync;
    void Start()
    {
        VRCObjectSync = GetComponent<VRCObjectSync>();
        material = GetComponent<Renderer>().material;
    }

    public override void OnPickupUseDown()
    {
        IsBack = !IsBack;
        if (IsBack)
        {
            material.color = Color.green;
        }
        else
        {
            material.color = Color.white;
        }
    }
    public override void OnPickup()
    {
        IsBack = false;
    }
    public override void OnDrop()
    {
        if (IsBack)
        {
            VRCObjectSync.Respawn();
        }
        IsBack = false;
    }
}
