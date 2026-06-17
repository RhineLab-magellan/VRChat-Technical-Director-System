
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlayerTrackingControl : UdonSharpBehaviour
{
    //系统选择
    public UdonBehaviour SystemSelect;

    private UdonBehaviour[] System;

    private int SystemID = 0;

    public Text SystemSelectText;


    //跟踪位置切换
    public Toggle[] TrackingToggles;
    private int TrackingTargetID = 0;

    public Text TrackingTargetText;

    public string[] TrackingTargetNames;
    //追踪位置偏移
    private Vector3 TrackingOffset;

    public Slider[] TrackingOffsetSliders;

    public Text[] TrackingOffsetSliderText;

    public Text TrackingOffsetText;
    //追踪位置偏移最大范围
    private float MaxOffset;
    private float MinOffset;

    public Slider[] TrackingOffsetMaxSliders;

    public Text[] TrackingOffsetMaxText;

    //相对追踪
    private bool RelativeTracking;

    public Toggle RelativeTrackingToggle;

    //显示模型
    private bool DisplayModel;
    public Toggle DisplayModelToggle;

    //临时写入机制
    private bool TempWrite;
    public Toggle TempWriteToggle;

    //归零偏移
    public Toggle[] ResetOffsetToggle;

    //旋转锁
    public Toggle[] RotationLockToggle;
    private bool[] RotationLock = new bool[3];

    //旋转偏移
    public Toggle RotationOffsetToggle;
    private bool RotationOffset;

    // 防重入哨兵：阻断 SystemSelectChange → TemporaryWriteIn → TempWriteIn → OnDeserialization → SystemSelectChange 无限递归
    private bool _isSystemSelectChanging = false;




    void Start()
    {
        //同步 Toggle 初始状态
        TempWrite = TempWriteToggle.isOn;
        for (int i = 0; i < TrackingToggles.Length; i++)
        {
            TrackingToggles[i].SetIsOnWithoutNotify(false);
        }
        //初始化系统选择
        TrackingOffsetMaxChange();
        SendCustomEventDelayedFrames("Start1", 1);
    }
    public void Start1()
    {
        var TempSystem = (GameObject[])SystemSelect.GetProgramVariable("System");
        if (TempSystem == null || TempSystem.Length == 0)
        {
            Debug.LogError("[PlayerTrackingControl] SystemSelect 未配置 System 数组或为空");
            return;
        }
        System = new UdonBehaviour[TempSystem.Length];
        for (int i = 0; i < TempSystem.Length; i++)
        {
            var go = TempSystem[i];
            if (go == null)
            {
                Debug.LogError("[PlayerTrackingControl] System[" + i + "] 为 null，跳过");
                continue;
            }
            var udon = go.GetComponent<UdonBehaviour>();
            if (udon == null)
            {
                Debug.LogError("[PlayerTrackingControl] System[" + i + "] 上无 UdonBehaviour，跳过");
                continue;
            }
            var trackingCamera = udon.GetProgramVariable("TrackingCamera");
            if (trackingCamera == null)
            {
                Debug.LogError("[PlayerTrackingControl] System[" + i + "] 的 TrackingCamera 为 null，跳过");
                continue;
            }
            var parent = ((Transform)trackingCamera).parent;
            if (parent == null)
            {
                Debug.LogError("[PlayerTrackingControl] System[" + i + "] 的 TrackingCamera 无父节点，跳过");
                continue;
            }
            var parentUdon = parent.GetComponent<UdonBehaviour>();
            if (parentUdon == null)
            {
                Debug.LogError("[PlayerTrackingControl] System[" + i + "] 的 TrackingCamera 父节点上无 UdonBehaviour，跳过");
                continue;
            }
            System[i] = parentUdon;
            Debug.Log("[PlayerTrackingControl] 系统 " + i + ": " + System[i].name);
        }
        SystemSelectChange();
    }
    //系统选择切换
    public void IndexUp()
    {
        SystemID++;
        if (SystemID >= System.Length)
        {
            SystemID = 0;
        }
        SystemSelectChange();
    }

    public void IndexDown()
    {
        SystemID--;
        if (SystemID < 0)
        {
            SystemID = System.Length - 1;
        }
        SystemSelectChange();
    }

    //系统切换初始化
    public void SystemSelectChange()
    {
        // 防重入：如果是从 TempWriteIn → OnDeserialization 回调触发的，跳过以免无限递归
        if (_isSystemSelectChanging) return;
        _isSystemSelectChanging = true;

        //系统选择文本
        SystemSelectText.text = SystemID.ToString();
        UdonBehaviour TempSystem = System[SystemID];
        //跟踪位置
        TrackingTargetID = (int)TempSystem.GetProgramVariable("TrackingID");
        //偏移位置
        TrackingOffset = (Vector3)TempSystem.GetProgramVariable("PositionOffset");
        //相对追踪
        RelativeTracking = (bool)TempSystem.GetProgramVariable("UseRelativePosition");
        //显示模型
        DisplayModel = (bool)TempSystem.GetProgramVariable("Appearance");
        //初始化数据

        //跟踪位置文本
        TrackingTargetText.text = TrackingTargetNames[TrackingTargetID];
        //偏移位置文本
        TrackingOffsetText.text = TrackingOffset.ToString("F2");
        //相对追踪
        RelativeTrackingToggle.SetIsOnWithoutNotify(RelativeTracking);
        //显示模型
        DisplayModelToggle.SetIsOnWithoutNotify(DisplayModel);


        //初始化XYZ + 最大最小值
        float MaxOffset = this.MaxOffset;
        float MinOffset = this.MinOffset;
        for (int i = 0; i < TrackingOffsetSliders.Length; i++)
        {
            if (TrackingOffset[i] > MaxOffset)
            {
                TrackingOffsetMaxSliders[0].value = TrackingOffset[i];
            }
            if (TrackingOffset[i] < MinOffset)
            {
                TrackingOffsetMaxSliders[1].value = TrackingOffset[i];
            }

            TrackingOffsetSliders[i].SetValueWithoutNotify(TrackingOffset[i]);
            TrackingOffsetSliderText[i].text = TrackingOffset[i].ToString("F2");
        }
        TrackingOffsetSliderChange();
        //旋转锁
        RotationLock = (bool[])TempSystem.GetProgramVariable("RotationLock");
        for (int i = 0; i < RotationLockToggle.Length; i++)
        {
            RotationLockToggle[i].SetIsOnWithoutNotify(RotationLock[i]);
        }
        //旋转偏移
        RotationOffset = (bool)TempSystem.GetProgramVariable("RotationOffset");
        RotationOffsetToggle.SetIsOnWithoutNotify(RotationOffset);

        _isSystemSelectChanging = false;
    }

    //临时写入机制切换
    public void TempWriteChange()
    {
        TempWrite = TempWriteToggle.isOn;
        TemporaryWriteIn();
    }

    //显示模型切换
    public void DisplayModelChange()
    {
        DisplayModel = DisplayModelToggle.isOn;
        TemporaryWriteIn();
    }

    //相对追踪切换
    public void RelativeTrackingChange()
    {
        RelativeTracking = RelativeTrackingToggle.isOn;
        TemporaryWriteIn();
    }

    //追踪位置偏移范围
    public void TrackingOffsetMaxChange()
    {
        MaxOffset = TrackingOffsetMaxSliders[0].value;
        MinOffset = TrackingOffsetMaxSliders[1].value;
        for (int i = 0; i < TrackingOffsetSliders.Length; i++)
        {
            if (TrackingOffsetSliders[i].value > MaxOffset)
            {
                TrackingOffsetSliders[i].value = MaxOffset;
            }
            if (TrackingOffsetSliders[i].value < MinOffset)
            {
                TrackingOffsetSliders[i].value = MinOffset;
            }
            TrackingOffsetSliders[i].maxValue = MaxOffset;
            TrackingOffsetSliders[i].minValue = MinOffset;
        }
        TrackingOffsetMaxSliders[0].minValue = MinOffset;
        TrackingOffsetMaxSliders[1].maxValue = MaxOffset;

        TrackingOffsetMaxText[0].text = MaxOffset.ToString("F2");
        TrackingOffsetMaxText[1].text = MinOffset.ToString("F2");
        TemporaryWriteIn();
    }

    //追踪位置偏移
    public void TrackingOffsetSliderChange()
    {
        TrackingOffset.x = TrackingOffsetSliders[0].value;
        TrackingOffset.y = TrackingOffsetSliders[1].value;
        TrackingOffset.z = TrackingOffsetSliders[2].value;
        TrackingOffsetText.text = TrackingOffset.ToString("F2");
        for (int i = 0; i < TrackingOffsetSliders.Length; i++)
        {
            TrackingOffsetSliderText[i].text = TrackingOffsetSliders[i].value.ToString("F2");
        }
        TemporaryWriteIn();
    }

    //追踪位置切换
    public void TrackingToggleChange()
    {
        for (int i = 0; i < TrackingToggles.Length; i++)
        {
            if (TrackingToggles[i].isOn)
            {
                TrackingTargetID = i;
                TrackingToggles[i].SetIsOnWithoutNotify(false);
            }
        }
        TrackingTargetText.text = TrackingTargetNames[TrackingTargetID];
        TemporaryWriteIn();
    }

    //旋转锁更新
    public void RotationLockChange()
    {
        for (int i = 0; i < RotationLockToggle.Length; i++)
        {
            RotationLock[i] = RotationLockToggle[i].isOn;
        }
        TemporaryWriteIn();
    }

    //旋转反转更新
    public void RotationOffsetChange()
    {
        RotationOffset = RotationOffsetToggle.isOn;
        TemporaryWriteIn();
    }

    public void TemporaryWriteIn()
    {
        if (TempWrite)
        {
            Debug.Log("临时写入");
            UdonBehaviour TempSystem = System[SystemID];
            TempSystem.SetProgramVariable("PositionOffset", TrackingOffset);
            TempSystem.SetProgramVariable("TrackingID", TrackingTargetID);
            TempSystem.SetProgramVariable("UseRelativePosition", RelativeTracking);
            TempSystem.SetProgramVariable("Appearance", DisplayModel);
            TempSystem.SetProgramVariable("RotationOffset", RotationOffset);
            TempSystem.SetProgramVariable("RotationLock", RotationLock);
            TempSystem.SendCustomEvent("TempWriteIn");
        }
    }

    //正式发送
    public void WriteIn()
    {
        UdonBehaviour TempSystem = System[SystemID];
        //位置更新
        TempSystem.SetProgramVariable("TempPositionOffset", TrackingOffset);
        TempSystem.SendCustomEventDelayedFrames("CallPositionOffset", 1);
        //跟踪位置更新
        TempSystem.SetProgramVariable("TempType", TrackingTargetID);
        TempSystem.SendCustomEventDelayedFrames("CallTempChanger", 1);
        //相对位置更新
        TempSystem.SetProgramVariable("TempUseRelativePosition", RelativeTracking);
        TempSystem.SendCustomEventDelayedFrames("CallUseRelativePosition", 1);
        //显示模型更新
        TempSystem.SetProgramVariable("Appearance", DisplayModel);
        TempSystem.SendCustomEventDelayedFrames("CallAppearance", 1);
        //旋转偏移更新
        TempSystem.SetProgramVariable("RotationOffset", RotationOffset);
        TempSystem.SendCustomEventDelayedFrames("CallRotationOffset", 1);
        //旋转锁更新
        TempSystem.SetProgramVariable("RotationLock", RotationLock);
        TempSystem.SendCustomEventDelayedFrames("CallRotationLock", 1);

    }

    //重新网络同步
    public void NetworkingCall()
    {
        UdonBehaviour TempSystem = System[SystemID];
        TempSystem.SetProgramVariable("TempSystem", GetComponent<UdonBehaviour>());
        if (Networking.IsOwner(TempSystem.gameObject))
        {
            TempSystem.RequestSerialization();
        }
    }

    //归零
    public void ResetOffset()
    {
        int i = 0;
        for (i = 0; i < ResetOffsetToggle.Length; i++)
        {
            if (ResetOffsetToggle[i].isOn)
            {
                TrackingOffsetSliders[i].value = 0;
                ResetOffsetToggle[i].SetIsOnWithoutNotify(false);
            }
        }
    }
}
