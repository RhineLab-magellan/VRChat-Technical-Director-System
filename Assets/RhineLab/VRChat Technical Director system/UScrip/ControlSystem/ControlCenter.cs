
using System;
using RhineLab.VRCD.TechnicalDirector.ControlSystem;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ControlCenter : UdonSharpBehaviour
{

    //切换跟踪模式所在位置
    private string[] VoidNames;

    //跟踪指示器
    //public Transform TrackingIndicator;

    //启动的
    [UdonSynced] private int VoidNameID = 0;

    [UdonSynced] private int CameraTrackingTarget = 0;

    private string DisPlayName;

    [UdonSynced] private bool Slarp;

    [UdonSynced] private bool UseUdon;
    [UdonSynced] private bool VoidObjectActive;



    //开始设置初始值
    [Tooltip("系统使用的RenderTexture")]
    public RenderTexture RenderTEX;
    [Tooltip("需要使用到RenderTexture的地方放这里")]
    public MeshRenderer[] material;
    [Tooltip("子系统设置在这里")]
    public GameObject[] ModName;
    [Tooltip("启用丢包检测（开启后您与其他人的同步会有延迟《大概1-5秒》）")]
    public bool SafeMod;

    [Tooltip("开启调试模式（减少日志输出以优化性能）")]
    public bool DebugMode;



    private VRCPlayerApi[] Players;
    private string[] Displayers;

    //跟踪模块
    private Transform TrackingCamera;
    private UdonBehaviour TrackingCameraUdon;
    //private Transform TrackingCamera2;
    private Camera CAM;
    [HideInInspector]
    public Transform CAMTransform;
    private string TrackingOwner;

    //相机脚本
    private UdonBehaviour CAMUdon;
    private UdonBehaviour CAMUdon2;

    //网络重传机制
    private bool NetworkingOn;
    private bool NetworkingStart;
    private VRCPlayerApi LocalPlayer;

    //缩略图相关

    //缩略图刷新
    [Tooltip("缩略图刷新帧率（单位：帧率）")]
    public float Refresh = 1f;

    public bool IsThumbnailOn = true;
    private float RefreshTime;

    private bool NeedRuning;

    private bool Isflowing;

    //用于指示是否处于使用状态--切换省性能模式和普通模式
    private bool Isusing = false;

    //用于指示是否处于运行状态--用于终止缩略图循环
    private bool Isrun = false;

    //指示是否启用了缩略图
    private bool IsThumbnail = false;

    //FOV控制器
    private CameraDataSYNC FOVController;


    void Start()
    {
        TrackingCameraUdon = transform.Find("TrackingTarget").GetComponent<UdonBehaviour>();
        TrackingCamera = TrackingCameraUdon.transform.GetChild(0);
        //TrackingCamera2 = transform.Find("OWNER Chest");
        CAMTransform = transform.Find("CameraTranform");
        CAM = CAMTransform.GetChild(0).GetComponent<Camera>();
        FOVController = CAMTransform.GetChild(0).GetChild(0).GetComponent<CameraDataSYNC>();

        LocalPlayer = Networking.LocalPlayer;
        UdonBehaviour TempUdon;
        for (int i = 0; i < ModName.Length; i++)
        {
            TempUdon = ModName[i].GetComponent<UdonBehaviour>();
            TempUdon.SetProgramVariable("TrackingIndicator", TrackingCamera);
            TempUdon.SetProgramVariable("controlCenter", this);
            TempUdon.enabled = false;

        }


        //以下部分为初始化更换系统下的所有RenderTEX
        CAMUdon = TrackingCamera.gameObject.GetComponent<UdonBehaviour>();
        //CAMUdon2 = TrackingCamera2.gameObject.GetComponent<UdonBehaviour>();

        TrackingOwner = Networking.GetOwner(TrackingCamera.gameObject).displayName;
        //CAM.GetComponent<VRCObjectSync>().FlagDiscontinuity();

        CAM.targetTexture = RenderTEX;
        CAM.enabled = true;

        //if (material.Length == 0) { return; }
        SendCustomEventDelayedFrames(nameof(Start1), 1);
        SendCustomEventDelayedFrames(nameof(RefreashTime), 1);

    }

    public void RefreashTime()
    {

        if (Refresh > 0f) // 避免除以零的错误
        {
            RefreshTime = 1 / Refresh;
        }
        else
        {
            Debug.LogError("Refresh 值不能为零或负数，无法计算帧时间。");
            RefreshTime = 1f;
        }
        //首次循环延时（防止同时渲染）
        var RandomTime = UnityEngine.Random.Range(0f, 2 * RefreshTime);

        SendCustomEventDelayedSeconds(nameof(ThumbnailUpdate), RandomTime);
    }
    public void Start1()
    {
        //Debug.Log(material.Length);
        for (int i = 0; i < material.Length; i++)
        {
            material[i].material.SetTexture("_MainTex", RenderTEX);
        }
        if (CAM.enabled)
        {
            CAM.enabled = false;
            CallCamera();
            return;
        }
        else
        {
            CAM.enabled = true;
            SendCustomEventDelayedFrames("Start1", 1);
        }

    }


    public void ThumbnailUpdate()
    {
        if (Isrun == false || NeedRuning || !IsThumbnailOn) { if (DebugMode) { Debug.Log("直播模式"); } return; }
        if (!Isusing)
        {
            if (CAM.enabled == false)
            {
                CAM.enabled = true;
                SendCustomEventDelayedFrames(nameof(CameraRefreash), 1);

            }
            else
            {
                SendCustomEvent(nameof(CameraRefreash));
            }
        }

    }

    public void RefreashChanger()
    {
        SendCustomEvent(nameof(ThumbnailUpdate));

    }


    public void CameraRefreash()
    {
        CAM.enabled = false;
        SendCustomEventDelayedSeconds(nameof(ThumbnailUpdate), RefreshTime);
    }



    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        CallCamera();
        ResetDisplay();

    }

    public void StartChanger()
    {
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        Networking.SetOwner(Networking.LocalPlayer, CAM.gameObject);
        Networking.SetOwner(Networking.LocalPlayer, CAMTransform.gameObject);
        TrackingCameraUdon.SendCustomEvent("UpdateValue");
        if (SafeMod)
        {
            RequestSerializationSafe();
            DisplayChanger();
        }
        else
        {
            RequestSerialization();
            OnDeserialization();
            DisplayChanger();
        }

    }

    public override void OnDeserialization()
    {
        CallCamera();

        NetworkingStart = false;
    }



    //统一关闭子系统以解锁Camera的控制
    public void CallCamera()
    {
        if (ModName.Length < VoidNameID)
        {
            Debug.LogError("您的系统设置错误，请检查VoidNameID是否超出范围");
            return;
        }

        for (int i = 0; i < ModName.Length; i++)
        {
            ModName[i].GetComponent<UdonBehaviour>().enabled = false;
        }

        ModSet();
    }

    private void ModSet()
    {
        if (VoidNameID == 0) { CAM.enabled = false; Isrun = false; return; }
        else
        {
            Isrun = true;
            UdonBehaviour ModUdon = ModName[VoidNameID - 1].GetComponent<UdonBehaviour>();
            //Debug.Log(VoidNameID - 1);
            CAM.enabled = true;
            ModUdon.enabled = true;
            ModUdon.SetProgramVariable("CameraTrackingTarget", CameraTrackingTarget);
            ModUdon.SetProgramVariable("Slarp", Slarp);
            ModUdon.SetProgramVariable("VoidObjectActive", VoidObjectActive);
            ModUdon.SetProgramVariable("UseUdon", UseUdon);
            ModUdon.SendCustomEvent("ChangerTarget");
            NeedRuning = (Boolean)ModUdon.GetProgramVariable("NeedRuning");
            if (IsThumbnail)
            {
                SendCustomEventDelayedSeconds(nameof(ThumbnailUpdate), RefreshTime);
            }



        }
    }

    public void DisplayChanger()
    {
        if (DisPlayName == Networking.GetOwner(TrackingCamera.gameObject).displayName /*&&
    DisPlayName == Networking.GetOwner(TrackingCamera2.gameObject).displayName*/)
        {
            TrackingOwner = DisPlayName;
            return;
        }
        int Index = Array.IndexOf(Displayers, DisPlayName);
        if (Index < 0)
        { return; }
        Networking.SetOwner(Players[Index], TrackingCamera.gameObject);
        //Networking.SetOwner(Players[Index], TrackingCamera2.gameObject);
        TrackingOwner = Networking.GetOwner(TrackingCamera.gameObject).displayName;
        TrackingCameraUdon.SendCustomEventDelayedFrames("UpdateValue", 5);
    }

    public void ResetDisplay()
    {
        Players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(Players);
        Displayers = new string[Players.Length];
        for (int i = 0; i < Players.Length; i++)
        {
            var Name = Players[i].displayName;
            Displayers[i] = Name;
        }
    }

    //网络重传机制
    public void RequestSerializationSafe()
    {
        NetworkingOn = true;
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "NetworkStart1");
        OnDeserialization();
    }

    public void NetworkS()
    {
        if (NetworkingOn == false) { return; }
        if (LocalPlayer == Networking.GetOwner(this.gameObject)) { RequestSerialization(); }
        NetworkingOn = false;

    }

    public void NetworkStart1()
    {
        if (LocalPlayer == Networking.GetOwner(this.gameObject)) { return; }
        NetworkingStart = true;
        SendCustomEventDelayedSeconds("NetwokEnd", 10);
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetworkS");

    }

    public void NetwokEnd()
    {
        if (NetworkingStart)
        {
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetworkError");
        }
    }

    public void NetworkError()
    {
        RequestSerialization();
    }

    public void FOVControllerSet(float FOV)
    {
        FOVController.fov = FOV;
        FOVController.SendCustomEventDelayedFrames(nameof(FOVController.ReSync), 1);
    }

    public string GetPointString(int Mode, int Index)
    {
        if (Mode == 0)
        {
            return "未开启系统";
        }
        相机分配系统 ModUdon = ModName[Mode - 1].GetComponent<相机分配系统>();
        if (Index >= ModUdon.PointString.Length)
        {
            return "不存在机位";
        }


        return ModUdon.PointString[Index];
    }
}
