
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FastCameraChanger : UdonSharpBehaviour
{
    public UdonBehaviour OutputSystem;
    public MeshRenderer[] Display;

    public MeshRenderer[] TVDisplay;
    private RenderTexture[] DisPlayTEX;
    private RenderTexture[] TVTextur;

    [Tooltip("显示的第一个图片的索引")]
    public int ShowIndex = 0;

    private int DisplayIndex;

    private void Start()
    {
        if (Display.Length == 0) { return; }
        SendCustomEventDelayedFrames("Start1", 2);
    }

    public void Start1()
    {
        DisPlayTEX = (RenderTexture[])OutputSystem.GetProgramVariable("DisPlayTEX");
        TVTextur = (RenderTexture[])OutputSystem.GetProgramVariable("TVTextur");


        for (int i = 0; i < DisPlayTEX.Length; i++)
        {
            var Show = i + ShowIndex + 1;
            var DIndex = i + 1;
            if (DIndex > DisPlayTEX.Length) { return; }
            if (Show > Display.Length) { return; }
            Display[i].material.SetTexture("_MainTex", DisPlayTEX[Show - 1]);
        }
        for (int i = 0; i < TVTextur.Length; i++)
        {
            var DIndex = i + 1;
            if (DIndex > TVTextur.Length) { return; }
            if (DIndex > TVDisplay.Length) { return; }
            TVDisplay[i].material.SetTexture("_MainTex", TVTextur[i]);
        }
    }

    public void Retransmission()
    {
        if (DisPlayTEX.Length > DisplayIndex)
        {
            Debug.Log(DisplayIndex);
            OutputSystem.SetProgramVariable("DisplayIndex", DisplayIndex);
            OutputSystem.SendCustomEvent("ChangerOther");
        }
    }

    public void ChangeTVDisplay()
    {
        DisplayIndex = -1;
        Retransmission();
    }
}
