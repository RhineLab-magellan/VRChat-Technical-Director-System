

using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class CameraViewControl : UdonSharpBehaviour
{
    //核心Udon
    public UdonBehaviour MainUdon;
    [Tooltip("同步补偿值")]
    public float Compensation;
    //[Tooltip("缓动速度值")]
    //public float Speed;

    //系统信息获取
    private GameObject[] System;
    private Camera[] Camera;

    private RenderTexture[] CameraRender;

    [UdonSynced, HideInInspector] public int SystemIndex;
    public Text SystemIndexText;

    //相机系统
    public MeshRenderer CameraDisplay;

    //fov
    [UdonSynced, HideInInspector] public float FOV;
    public Text FOVText;

    //Slider控制倍率
    public Slider FOVControl;
    private float FOVControlValue;
    public Text FOVControlText;

    //缓冲值
    public Slider FOVBufferSlider;
    [UdonSynced] private float FOVBufferValue;
    public Text FOVBufferText;

    //倍率
    public Slider FOVRate;
    private float FOVRateValue;
    public Text FOVRateText;

    //直接调用
    public Slider FOVSlider;
    public Text FOVSliderText;
    private float Fovslidervalue;

    //FOV限制器
    [UdonSynced] private float FOVMaxValue = 179f;
    [UdonSynced] private float FOVMinValue = 0.1f;
    public Slider FOVMaxSlider;
    public Text FOVMaxText;
    public Slider FOVMinSlider;
    public Text FOVMinText;

    //系统启用状态

    //自动模式
    private bool SystemOpenState;
    public Image SystemOpenImage;
    public GameObject SystemOpenObject;

    //手动模式
    private bool SystemOpenState1;
    public Image systemOpenImage1;
    public GameObject SystemOpenObject1;

    //FOV限制器
    private bool FOVLimitState;
    public Image FOVLimitImage;
    public GameObject FOVLimitObject;

    //手柄控制
    private bool IsVR;
    public Toggle HandControlToggle;

    private bool HandControlState;

    private float HandControlValue;





    //初始化

    void Start()
    {
        System = (GameObject[])MainUdon.GetProgramVariable("System");
        Camera = new Camera[System.Length];
        for (int i = 0; i < System.Length; i++)
        {
            Camera[i] = System[i].transform.Find("CameraTranform").GetChild(0).GetComponent<Camera>();
        }
        FOVBufferSliderChange();
        FOV = Camera[SystemIndex].GetGateFittedFieldOfView();
        SendCustomEventDelayedFrames("Start1", 1);
        FOVSliderChange();
        MaxValueChange();
        MinValueChange();
        SystemOpenObject.SetActive(false);
        SystemOpenObject1.SetActive(false);
        FOVLimitObject.SetActive(false);
        IsVR = Networking.LocalPlayer.IsUserInVR();
        HandControlChange();

    }
    public void Start1()
    {
        CameraRender = (RenderTexture[])MainUdon.GetProgramVariable("CameraRender");
        CameraDisplay.material.SetTexture("_MainTex", CameraRender[SystemIndex]);
        var Udon = Camera[SystemIndex].transform.GetChild(0).GetComponent<UdonBehaviour>();
        Udon.SetProgramVariable("Index", SystemIndex);
        Udon.SetProgramVariable("cameraViewController", this);

    }

    //系统启用状态切换
    public void SystemOpen()
    {
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        SystemOpenState = !SystemOpenState;
        SystemOpenState1 = false;
        systemOpenImage1.color = Color.black;

        var Udon = Camera[SystemIndex].transform.GetChild(0).GetComponent<UdonBehaviour>();
        Udon.SetProgramVariable("Index", SystemIndex);
        Udon.SetProgramVariable("cameraViewController", this);
        if (SystemOpenState)
        {
            Networking.SetOwner(Networking.LocalPlayer, Udon.gameObject);

            SystemOpenImage.color = Color.white;
        }
        else
        {
            SystemOpenImage.color = Color.black;
            SystemOFF();
            return;

        }
        SystemOpenObject.SetActive(true);
        SystemOpenObject1.SetActive(false);
        FOVLimitState = false;
        FOVLimitImage.color = Color.black;
        FOVLimitObject.SetActive(false);
        systemOpenImage1.color = Color.black;
        SystemOpenState1 = false;
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "ControlOFF");
    }
    public void SystemOpen1()
    {
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        SystemOpenState1 = !SystemOpenState1;
        SystemOpenState = false;
        SystemOpenImage.color = Color.black;

        var Udon = Camera[SystemIndex].transform.GetChild(0).GetComponent<UdonBehaviour>();
        Udon.SetProgramVariable("Index", SystemIndex);
        Udon.SetProgramVariable("cameraViewController", this);
        if (SystemOpenState1)
        {
            Networking.SetOwner(Networking.LocalPlayer, Udon.gameObject);

            systemOpenImage1.color = Color.white;
        }
        else
        {
            systemOpenImage1.color = Color.black;
            SystemOFF();
            return;
        }
        SystemOpenImage.color = Color.black;
        SystemOpenState = false;
        SystemOpenObject.SetActive(false);

        FOVLimitState = false;
        FOVLimitImage.color = Color.black;
        FOVLimitObject.SetActive(false);

        SystemOpenObject1.SetActive(true);

        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "ControlOFF");
    }

    public void SystemOFF()
    {
        SystemOpenState = false;
        SystemOpenState1 = false;
        FOVLimitState = false;
        SystemOpenImage.color = Color.black;
        systemOpenImage1.color = Color.black;
        FOVLimitImage.color = Color.black;
        SystemOpenObject.SetActive(false);
        SystemOpenObject1.SetActive(false);
        FOVLimitObject.SetActive(false);
    }

    public void ControlOFF()
    {
        if (!Networking.IsOwner(this.gameObject))
        {
            SystemOpenState = false;
            SystemOpenState1 = false;
            FOVLimitState = false;
            SystemOpenImage.color = Color.black;
            systemOpenImage1.color = Color.black;
            FOVLimitImage.color = Color.black;
            SystemOpenObject.SetActive(false);
            SystemOpenObject1.SetActive(false);
            FOVLimitObject.SetActive(false);
        }
    }

    public void FOVLimitOpen()
    {
        FOVLimitState = !FOVLimitState;
        if (FOVLimitState)
        {
            FOVLimitImage.color = Color.white;
            FOVLimitObject.SetActive(true);
            SystemOpenState = false;
            SystemOpenState1 = false;
            SystemOpenImage.color = Color.black;
            systemOpenImage1.color = Color.black;
            SystemOpenObject.SetActive(false);
            SystemOpenObject1.SetActive(false);
        }
        else
        {
            SystemOFF();
        }
    }

    //Slider速率控制
    public void FOVControlChange()
    {
        FOVControlValue = FOVControl.value;
        FOVControlText.text = FOVControlValue.ToString("F3");
    }

    //倍率控制
    public void FOVRateChange()
    {
        FOVRateValue = FOVRate.value;
        FOVRateText.text = FOVRateValue.ToString("F3");
    }

    //直接调用
    public void FOVSliderChange()
    {
        Fovslidervalue = FOVSlider.value;
        FOVSliderText.text = Fovslidervalue.ToString("F3");
    }

    //FOV计算(主)/同步（副）
    void Update()
    {
        if (SystemOpenState)
        {
            FOV = Camera[SystemIndex].GetGateFittedFieldOfView();
            //FOV += -FOVControlValue * FOVRateValue * Time.deltaTime;
            if (HandControlState)
            {
                FOV += -FOVControlValue * FOVRateValue * Time.deltaTime * HandControlValue;
            }
            else
            {
                FOV += -FOVControlValue * FOVRateValue * Time.deltaTime;
            }

            if (FOV > FOVMaxValue)
            {
                FOV = FOVMaxValue;
            }
            if (FOV < FOVMinValue)
            {
                FOV = FOVMinValue;
            }
            Camera[SystemIndex].fieldOfView = FOV;
            FOVText.text = FOV.ToString();
            FOVSlider.value = FOV;
            FOVSliderText.text = FOV.ToString("F3");
        }
        else if (SystemOpenState1)
        {
            FOV = Camera[SystemIndex].GetGateFittedFieldOfView();
            //FOV += (Fovslidervalue - FOV) * FOVBufferValue * Time.deltaTime;
            if (HandControlState)
            {
                FOV += (Fovslidervalue - FOV) * FOVBufferValue * Time.deltaTime * HandControlValue;
            }
            else
            {
                FOV += (Fovslidervalue - FOV) * FOVBufferValue * Time.deltaTime;
            }
            if (FOV > FOVMaxValue)
            {
                FOV = FOVMaxValue;
            }
            if (FOV < FOVMinValue)
            {
                FOV = FOVMinValue;
            }
            Camera[SystemIndex].fieldOfView = FOV;
            FOVText.text = FOV.ToString("F3");
        }
        else
        {
            float FOVL;
            FOVL = Camera[SystemIndex].GetGateFittedFieldOfView();
            FOVL += (FOV - FOVL) * Time.deltaTime * Compensation;
            Camera[SystemIndex].fieldOfView = FOVL;
            FOVText.text = FOV.ToString();
            FOVSlider.value = FOV;
            FOVSliderText.text = FOV.ToString("F3");
        }
    }

    public void IndexUp()
    {
        SystemIndex++;
        if (SystemIndex > System.Length - 1)
        {
            SystemIndex = System.Length - 1;
        }
        CameraDisplay.material.SetTexture("_MainTex", CameraRender[SystemIndex]);
        FOV = Camera[SystemIndex].GetGateFittedFieldOfView();
        SystemIndexText.text = SystemIndex.ToString();
        FOVSlider.value = FOV;
        FOVSliderText.text = FOV.ToString("F3");
    }
    public void IndexDown()
    {
        SystemIndex--;
        if (SystemIndex < 0)
        {
            SystemIndex = 0;
        }
        CameraDisplay.material.SetTexture("_MainTex", CameraRender[SystemIndex]);
        FOV = Camera[SystemIndex].GetGateFittedFieldOfView();
        SystemIndexText.text = SystemIndex.ToString();
        FOVSlider.value = FOV;
        FOVSliderText.text = FOV.ToString("F3");
    }

    public void MaxValueChange()
    {
        FOVMaxValue = FOVMaxSlider.value;
        if (FOVSlider.value > FOVMaxValue)
        {
            FOVSlider.value = FOVMaxValue;
            FOVSliderText.text = FOVMaxValue.ToString("F3");
        }
        FOVSlider.maxValue = FOVMaxValue;

        FOVMaxText.text = FOVMaxValue.ToString("F3");
        CheckFOV();
    }

    public void MinValueChange()
    {
        FOVMinValue = FOVMinSlider.value;
        if (FOVSlider.value < FOVMinValue)
        {
            FOVSlider.value = FOVMinValue;
            FOVSliderText.text = FOVMinValue.ToString("F3");
        }
        FOVSlider.minValue = FOVMinValue;
        FOVMinText.text = FOVMinValue.ToString("F3");

        CheckFOV();
    }

    //缓冲值
    public void FOVBufferSliderChange()
    {
        FOVBufferValue = FOVBufferSlider.value;
        FOVBufferText.text = FOVBufferValue.ToString("F3");
    }

    private void CheckFOV()
    {
        if (FOV > FOVMaxValue)
        {
            FOV = FOVMaxValue;
        }
        if (FOV < FOVMinValue)
        {
            FOV = FOVMinValue;
        }
        Camera[SystemIndex].fieldOfView = FOV;
    }

    public void HandControlChange()
    {
        if (!IsVR)
        {
            HandControlToggle.SetIsOnWithoutNotify(false);
            return;
        }
        HandControlState = HandControlToggle.isOn;
    }

    public override void InputLookVertical(float value, UdonInputEventArgs args)
    {
        if (!HandControlState)
        {
            return;
        }
        if (value < 0.1 && value > -0.1)
        {
            HandControlValue = 0;
            return;
        }
        HandControlValue = value;
    }

}
