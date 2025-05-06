
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class 发送控制信号 : UdonSharpBehaviour
{
    [Tooltip("请将但单相机系统最上层放置在此（脚本名为：ControlCenter）")]
    public GameObject[] System;
    [Tooltip("缩略图刷新速度（单位：帧/秒）")]
    private int SystemIndex = 0;

    private RenderTexture[] CameraRender;

    public Dropdown VoidName;
    public Dropdown CameraName;
    public Text CameraTracking;
    public InputField TrackingName;
    public Text TrackingFor;
    public Image SlarpBooton;
    public Image UseButton;
    public Image UseUdonBooton;





    private int VoidNameID;
    private int CameraTrackingTarget;
    private UdonBehaviour MainControl;
    private string DisPlayName;
    private string[] PlayName;
    private string TrackingOwner;
    private bool Slarp = true;
    private bool UseUdon = true;


    public Material CameraM;

    //
    void Start()
    {
        PlayName = new string[System.Length];
        var Master = Networking.GetOwner(this.gameObject).displayName;
        for (int i = 0; i < PlayName.Length; i++)
        {
            PlayName[i] = Master;
        }
        if (Networking.GetOwner(this.gameObject) == Networking.LocalPlayer)
        {
            TrackingOwner = Networking.LocalPlayer.displayName;
            TrackingFor.text = PlayName[SystemIndex];
        }
        else
        {
            TrackingFor.text = PlayName[SystemIndex];
        }


        MainControl = System[SystemIndex].GetComponent<UdonBehaviour>();

        CameraRender = new RenderTexture[System.Length];
        for (int i = 0; i < CameraRender.Length; i++)
        {
            CameraRender[i] = (RenderTexture)System[i].GetComponent<UdonBehaviour>().GetProgramVariable("RenderTEX");
        }
        SendCustomEventDelayedFrames("Start1", 1);
        CameraChangerBottom();
    }

    public void Start1()
    {
        TrackingOwner = (string)MainControl.GetProgramVariable("TrackingOwner");
        TrackingFor.text = PlayName[SystemIndex];
    }

    public override void Interact()
    {
        InteractStart();
    }

    public void InteractStart()
    {
        UseButton.color = Color.white;
        if (SystemIndex >= System.Length)
        {
            return;
        }
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        DisPlayName = TrackingName.text;
        MainControl = System[SystemIndex].GetComponent<UdonBehaviour>();
        Networking.SetOwner(Networking.LocalPlayer, MainControl.gameObject);
        MainControl.SetProgramVariable("VoidNameID", VoidNameID);
        MainControl.SetProgramVariable("CameraTrackingTarget", CameraTrackingTarget);
        MainControl.SetProgramVariable("DisPlayName", DisPlayName);
        MainControl.SetProgramVariable("Slarp", Slarp);
        MainControl.SetProgramVariable("UseUdon", UseUdon);
        MainControl.SendCustomEvent("StartChanger");

        TrackingOwner = (string)MainControl.GetProgramVariable("TrackingOwner");
        PlayName[SystemIndex] = TrackingOwner;
        TrackingFor.text = PlayName[SystemIndex];
        SendCustomEventDelayedSeconds("ResetColor", 1f);
    }

    public void ResetColor()
    {
        UseButton.color = Color.black;
    }

    public void ModeChanger()
    {
        VoidNameID = VoidName.value;
    }
    public void CameraChanger()
    {
        SystemIndex = CameraName.value;
        CameraChangerBottom();
    }

    public void CameraChangerBottom()
    {
        MainControl = System[SystemIndex].GetComponent<UdonBehaviour>();
        CameraM.SetTexture("_MainTex", CameraRender[SystemIndex]);
        TrackingOwner = (string)MainControl.GetProgramVariable("TrackingOwner");
        TrackingFor.text = PlayName[SystemIndex];
    }

    public void CameraNumberUp()
    {
        SystemIndex = SystemIndex++;
        if (System.Length > SystemIndex)
        {
            CameraName.value = SystemIndex;
            MainControl = System[SystemIndex].GetComponent<UdonBehaviour>();
            CameraM.SetTexture("_MainTex", CameraRender[SystemIndex]);
            TrackingOwner = (string)MainControl.GetProgramVariable("TrackingOwner");
            TrackingFor.text = PlayName[SystemIndex];
        }
        else
        {
            SystemIndex = System.Length - 1;
            MainControl = System[SystemIndex].GetComponent<UdonBehaviour>();
            CameraM.SetTexture("_MainTex", CameraRender[SystemIndex]);
            TrackingOwner = (string)MainControl.GetProgramVariable("TrackingOwner");
            TrackingFor.text = PlayName[SystemIndex];
        }
    }

    public void CameraNumberDown()
    {
        SystemIndex = --SystemIndex;
        if (SystemIndex < 0)
        {
            SystemIndex = 0;
            CameraName.value = SystemIndex;
            MainControl = System[SystemIndex].GetComponent<UdonBehaviour>();
            CameraM.SetTexture("_MainTex", CameraRender[SystemIndex]);
            TrackingOwner = (string)MainControl.GetProgramVariable("TrackingOwner");
            TrackingFor.text = PlayName[SystemIndex];
        }
        else
        {

            CameraName.value = SystemIndex;
            MainControl = System[SystemIndex].GetComponent<UdonBehaviour>();
            CameraM.SetTexture("_MainTex", CameraRender[SystemIndex]);
            TrackingOwner = (string)MainControl.GetProgramVariable("TrackingOwner");
            TrackingFor.text = PlayName[SystemIndex];
        }
    }


    public void Up()
    {
        CameraTrackingTarget = CameraTrackingTarget++;

        CameraTracking.text = CameraTrackingTarget.ToString();
    }
    public void Down()
    {
        if (CameraTrackingTarget < 1)
        {
            CameraTrackingTarget = 0;
        }
        else
        {
            CameraTrackingTarget = CameraTrackingTarget--;
        }

        CameraTracking.text = CameraTrackingTarget.ToString();
    }

    public void Up10()
    {
        CameraTrackingTarget = CameraTrackingTarget + 10;

        CameraTracking.text = CameraTrackingTarget.ToString();
    }
    public void Down10()
    {
        if (CameraTrackingTarget < 10)
        {
            CameraTrackingTarget = 0;
        }
        else
        {
            CameraTrackingTarget = CameraTrackingTarget - 10;
        }

        CameraTracking.text = CameraTrackingTarget.ToString();
    }

    public void ChangerDisplay()
    {
        DisPlayName = TrackingName.text;
    }


    public override void OnDeserialization()
    {
        TrackingFor.text = PlayName[SystemIndex];
    }
    public void ChangerSlarpMode()
    {
        Slarp = !Slarp;
        if (Slarp)
        {
            SlarpBooton.color = Color.yellow;
        }
        else
        {
            SlarpBooton.color = Color.red;
        }
    }

    public void ChangerUdonUse()
    {
        UseUdon = !UseUdon;
        if (UseUdon)
        {
            UseUdonBooton.color = Color.yellow;
        }
        else
        {
            UseUdonBooton.color = Color.red;
        }
    }

    //快速保存读取面板数据回写
    public void QuickSave()
    {
        if (CameraName != null)
        {
            CameraName.value = SystemIndex;
        }
        if (CameraTracking != null)
        {
            CameraTracking.text = CameraTrackingTarget.ToString();
        }
        if (VoidName != null)
        {
            VoidName.value = VoidNameID;
        }
        if (TrackingName != null)
        {
            TrackingName.text = DisPlayName;
        }
        if (Slarp)
        { SlarpBooton.color = Color.white; }
        else
        { SlarpBooton.color = Color.black; }
        if (UseUdon)
        { UseUdonBooton.color = Color.white; }
        else
        { UseUdonBooton.color = Color.black; }
        InteractStart();
    }

    //快速玩家名称触发
    public void QuickPlayerName()
    {
        MainControl.SetProgramVariable("DisPlayName", DisPlayName);
        MainControl.SendCustomEvent("StartChanger");
        PlayName[SystemIndex] = DisPlayName;
        TrackingFor.text = PlayName[SystemIndex];
        Debug.Log("重置玩家名称已经开启");
    }

    public void ResetDisplay()
    {
        for (int i = 0; i < PlayName.Length; i++)
        {
            var Control = System[i].GetComponent<UdonBehaviour>();
            Control.SendCustomEvent("ResetDisplay");
        }
    }

}
