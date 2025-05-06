
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]

public class BottonIndex : UdonSharpBehaviour
{
    [Tooltip("该按钮对应的系统的索引值")]
    public int DisplayIndex;
    public UdonBehaviour Retransmission;

    public void Use()
    {
        Retransmission.SetProgramVariable("DisplayIndex", DisplayIndex);
        Retransmission.SendCustomEvent("Retransmission");
    }
}
