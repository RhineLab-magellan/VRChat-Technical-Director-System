
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;
using Unity.Mathematics;


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerTrackingSystem : UdonSharpBehaviour
{
    //跟踪ID
    [UdonSynced] public int TrackingID;
    //跟踪目标
    public VRCPlayerApi TrackingTarget;
    //跟踪部位——0：头 1：左手上 2：右手上 3:模型原点
    private VRCPlayerApi.TrackingDataType TrackingDataType;

    private bool VrcTracking = false;

    [HideInInspector]
    public int TempType;

    [Tooltip("开启调试模式（减少日志输出以优化性能）")]
    public bool DebugMode;

    //跟踪数据
    private VRCPlayerApi.TrackingData TrackingData;

    //位置偏移
    [UdonSynced] public Vector3 PositionOffset;


    [HideInInspector]
    public Vector3 TempPositionOffset;

    [HideInInspector]
    public bool TempUseRelativePosition;

    //相对位置
    public Transform RelativePosition;

    [UdonSynced] private bool UseRelativePosition = false;

    [HideInInspector]
    [UdonSynced] public bool Appearance;

    //临时网络同步
    [HideInInspector]
    public UdonBehaviour TempSystem;

    //本地预览模式标志（临时写入时激活，防止预览值被意外序列化）
    private bool _isPreviewActive = false;

    //旋转方向
    [UdonSynced] public bool RotationOffset;

    [UdonSynced] private bool[] RotationLock = new bool[3];

    void Start()
    {
        TrackingTarget = Networking.GetOwner(this.gameObject);
        AppearanceChanger();
        OnDeserialization();
    }

    public void Update()
    {
        if (TrackingTarget == null)
        {
            TrackingTarget = Networking.GetOwner(this.gameObject);
        }
        if (!VrcTracking) return;

        TrackingData = TrackingTarget.GetTrackingData(TrackingDataType);

        if (UseRelativePosition)
        {
            ApplyRelativeTracking();
        }
        else
        {
            ApplyAbsoluteTracking();
        }
    }

    /// <summary>相对追踪模式：transform 的位置和旋转都跟随跟踪部位的 TrackingData</summary>
    private void ApplyRelativeTracking()
    {
        transform.position = TrackingData.position;

        Quaternion rot = TrackingData.rotation;
        if (RotationOffset)
        {
            rot = Quaternion.Inverse(rot);
        }

        Vector3 euler = rot.eulerAngles;
        if (RotationLock[0]) euler.x = 0f;
        if (RotationLock[1]) euler.y = 0f;
        if (RotationLock[2]) euler.z = 0f;

        transform.rotation = Quaternion.Euler(euler);
    }

    /// <summary>绝对追踪模式：transform 仅位置跟随（位置 + 偏移），不处理旋转</summary>
    private void ApplyAbsoluteTracking()
    {
        transform.position = TrackingData.position + PositionOffset;
        transform.rotation = Quaternion.identity;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        TrackingTarget = player;
        if (DebugMode) { Debug.Log("玩家" + player.displayName + "获得了跟踪权限"); }
    }

    public void AppearanceChanger()
    {
        RelativePosition.gameObject.SetActive(Appearance);
    }

    //网络区域

    //先将变量写入TempType，再调用CallTempChanger()同步出去。

    public void CallTempChanger()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.All, nameof(SetTrackingDataType), TempType);
    }

    //先将变量写入TempPositionOffset，再调用CallPositionOffset()同步出去。

    public void CallPositionOffset()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.All, nameof(SetPositionOffset), TempPositionOffset);
    }

    //设置相对追踪
    public void CallUseRelativePosition()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.All, nameof(SetUseRelativePosition), TempUseRelativePosition);
    }

    public void CallRotationLock()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.All, nameof(SetRotationLock), RotationLock);
    }

    public void CallRotationOffset()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.All, nameof(SetRotationOffset), RotationOffset);
    }

    public void CallAppearance()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.All, nameof(SetAppearance), Appearance);
    }



    //网络调用区域
    private bool _serializationPending = false;

    [NetworkCallable]
    public void SetTrackingDataType(int TempType)
    {
        TrackingID = TempType;
        TrackingDataType = (VRCPlayerApi.TrackingDataType)TempType;
        ScheduleSerialization();
    }

    [NetworkCallable]
    public void SetPositionOffset(Vector3 Position)
    {
        this.PositionOffset = Position;
        ScheduleSerialization();
    }

    //设置是否使用相对位置
    [NetworkCallable]
    public void SetUseRelativePosition(bool UseRelativePosition)
    {
        this.UseRelativePosition = UseRelativePosition;
        ScheduleSerialization();
    }
    //设置旋转锁
    [NetworkCallable]
    public void SetRotationLock(bool[] RotationLock)
    {
        this.RotationLock = RotationLock;
        ScheduleSerialization();
    }

    //设置旋转偏移
    [NetworkCallable]
    public void SetRotationOffset(bool RotationOffset)
    {
        this.RotationOffset = RotationOffset;
        ScheduleSerialization();
    }

    //设置显示模型
    [NetworkCallable]
    public void SetAppearance(bool Appearance)
    {
        this.Appearance = Appearance;
        AppearanceChanger();
        ScheduleSerialization();
    }

    private void ScheduleSerialization()
    {
        _isPreviewActive = false;
        if (!_serializationPending)
        {
            _serializationPending = true;
            SendCustomEventDelayedSeconds(nameof(NetworkingCall), 1f);
        }
    }

    public void NetworkingCall()
    {
        if (!Networking.IsOwner(this.gameObject))
        {
            return;
        }
        if (_serializationPending)
        {
            RequestSerialization();
            _serializationPending = false;
        }
    }

    public override void OnDeserialization()
    {
        //将TrackingID转换为TrackingDataType
        if (TrackingID <= 3)
        {
            TrackingDataType = (VRCPlayerApi.TrackingDataType)TrackingID;
            VrcTracking = true;
        }
        else
        {
            VrcTracking = false;
        }
        _serializationPending = false;
        //应用位置偏移到相对位置节点
        if (UseRelativePosition && RelativePosition != null)
        {
            RelativePosition.localPosition = PositionOffset;
        }
        //应用显示状态
        AppearanceChanger();
        if (TempSystem != null)
        {
            TempSystem.SendCustomEvent("SystemSelectChange");
        }
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsOwner(this.gameObject) && !_isPreviewActive)
        {
            RequestSerialization();
        }
    }

    public void TempWriteIn()
    {
        // 本地预览模式：将 SetProgramVariable 写入的同步字段值刷新到本地状态
        // 注意：预览值仅在本地有效，会在下次 OnDeserialization 或正式 WriteIn 时被覆盖
        _isPreviewActive = true;
        OnDeserialization();

    }



}
