
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class DisPlayerM : UdonSharpBehaviour
{
    public Material CameraM;
    public Material CameraM2;
    public UdonBehaviour Main;

    public RenderTexture[] TVTextur;

    private GameObject[] CameraSystem;
    public Dropdown List;

    [UdonSynced] private int DisplayIndex;
    private RenderTexture[] DisPlayTEX;

    private int UsingCamera = -1;
    void Start()
    {
        SendCustomEventDelayedFrames("Start1", 1);

    }
    public void Start1()
    {
        DisPlayTEX = (RenderTexture[])Main.GetProgramVariable("CameraRender");
        CameraSystem = (GameObject[])Main.GetProgramVariable("System");
        OnDeserialization();
    }

    public void Changer()
    {
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        DisplayIndex = List.value;
        RequestSerialization();
        OnDeserialization();
    }

    public void ChangerOther()
    {
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        RequestSerialization();
        OnDeserialization();
    }

    public override void OnDeserialization()
    {
        if (UsingCamera > -1)
        {
            UdonBehaviour SystemUdon = CameraSystem[UsingCamera].GetComponent<UdonBehaviour>();
            SystemUdon.SetProgramVariable("Isusing", false);
            SystemUdon.SendCustomEvent("RefreashChanger");
            UsingCamera = -1;
        }

        if (-1 < DisplayIndex && DisplayIndex < DisPlayTEX.Length)
        {

            CameraM.SetTexture("_MainTex", DisPlayTEX[DisplayIndex]);
            //CameraM.SetTexture("_EmissionMap", DisPlayTEX[DisplayIndex]);
            CameraM2.SetTexture("_MainTex", DisPlayTEX[DisplayIndex]);
            List.value = DisplayIndex;
            UdonBehaviour SystemUdon = CameraSystem[DisplayIndex].GetComponent<UdonBehaviour>();
            UsingCamera = DisplayIndex;
            SystemUdon.SetProgramVariable("Isusing", true);
            SystemUdon.SendCustomEvent("RefreashChanger");
        }
        else if (-1 >= DisplayIndex && DisplayIndex < (-TVTextur.Length))
        {
            DisplayIndex = -DisplayIndex - 1;
            CameraM.SetTexture("_MainTex", TVTextur[DisplayIndex]);
            //CameraM.SetTexture("_EmissionMap", TVTextur[DisplayIndex]);
            CameraM2.SetTexture("_MainTex", TVTextur[DisplayIndex]);
            UsingCamera = -1;

        }






    }


}
