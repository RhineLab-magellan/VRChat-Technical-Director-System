
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class RelativeCenter : UdonSharpBehaviour
{
    public Transform CenterPoint;
    void Start()
    {

    }
    void Update()
    {
        transform.position = CenterPoint.position;
        transform.rotation = CenterPoint.rotation;
    }
}
