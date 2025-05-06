

using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class CameraViewControl : UdonSharpBehaviour
{
    //核心Udon
    public UdonBehaviour MainUdon;
    [Tooltip("同步补偿值")]
    public float Compensation;
    [Tooltip("缓动速度值")]
    public float Speed;

    //系统信息获取
    private GameObject[] System;
    private Camera[] Camera;

    private RenderTexture[] CameraRender;

    [UdonSynced] private int SystemIndex;
    public Text SystemIndexText;

    //相机系统
    public MeshRenderer CameraDisplay;

    //fov
    [UdonSynced] private float FOV;
    public Text FOVText;

    //Slider控制倍率
    public Slider FOVControl;
    private float FOVControlValue;
    public Text FOVControlText;

    //倍率
    public Slider FOVRate;
    private float FOVRateValue;
    public Text FOVRateText;

    //直接调用
    public Slider FOVSlider;
    public Text FOVSliderText;
    private float Fovslidervalue;

    //系统启用状态

    //自动模式
    private bool SystemOpenState;
    public Image SystemOpenImage;
    public GameObject SystemOpenObject;

    //手动模式
    private bool SystemOpenState1;
    public Image systemOpenImage1;
    public GameObject SystemOpenObject1;

    //初始化

    void Start()
    {
        System = (GameObject[])MainUdon.GetProgramVariable("System");
        Camera = new Camera[System.Length];
        for (int i = 0; i < System.Length; i++)
        {
            Camera[i] = System[i].transform.Find("CameraSystem").GetComponent<Camera>();
        }
        FOV = Camera[SystemIndex].GetGateFittedFieldOfView();
        SendCustomEventDelayedFrames("Start1", 1);
        FOVSliderChange();
        SystemOpenObject.SetActive(false);
        SystemOpenObject1.SetActive(false);
    }
    public void Start1()
    {
        CameraRender = (RenderTexture[])MainUdon.GetProgramVariable("CameraRender");
        CameraDisplay.material.SetTexture("_MainTex", CameraRender[SystemIndex]);

    }

    //系统启用状态切换
    public void SystemOpen()
    {
        Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        SystemOpenState = !SystemOpenState;
        SystemOpenState1 = false;
        systemOpenImage1.color = Color.black;

        var Udon = Camera[SystemIndex].GetComponent<UdonBehaviour>();
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

        var Udon = Camera[SystemIndex].GetComponent<UdonBehaviour>();
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
        SystemOpenObject1.SetActive(true);

        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "ControlOFF");
    }

    public void SystemOFF()
    {
        SystemOpenState = false;
        SystemOpenState1 = false;
        SystemOpenImage.color = Color.black;
        systemOpenImage1.color = Color.black;
        SystemOpenObject.SetActive(false);
        SystemOpenObject1.SetActive(false);
    }

    public void ControlOFF()
    {
        if (!Networking.IsOwner(this.gameObject))
        {
            SystemOpenState = false;
            SystemOpenState1 = false;
            SystemOpenImage.color = Color.black;
            systemOpenImage1.color = Color.black;
            SystemOpenObject.SetActive(false);
            SystemOpenObject1.SetActive(false);
        }
    }

    //Slider速率控制
    public void FOVControlChange()
    {
        FOVControlValue = FOVControl.value;
        FOVControlText.text = FOVControlValue.ToString();
    }

    //倍率控制
    public void FOVRateChange()
    {
        FOVRateValue = FOVRate.value;
        FOVRateText.text = FOVRateValue.ToString();
    }

    //直接调用
    public void FOVSliderChange()
    {
        Fovslidervalue = FOVSlider.value;
        FOVSliderText.text = Fovslidervalue.ToString();
    }

    //FOV计算(主)/同步（副）
    void Update()
    {
        if (SystemOpenState)
        {
            FOV = Camera[SystemIndex].GetGateFittedFieldOfView();
            FOV += -FOVControlValue * FOVRateValue * Time.deltaTime;
            Camera[SystemIndex].fieldOfView = FOV;
            FOVText.text = FOV.ToString();
            FOVSlider.value = FOV;
            FOVSliderText.text = FOV.ToString();
        }
        else if (SystemOpenState1)
        {
            FOV = Camera[SystemIndex].GetGateFittedFieldOfView();
            FOV += (Fovslidervalue - FOV) * Speed * Time.deltaTime;
            Camera[SystemIndex].fieldOfView = FOV;
            FOVText.text = FOV.ToString();
        }
        else
        {
            float FOVL;
            FOVL = Camera[SystemIndex].GetGateFittedFieldOfView();
            FOVL += (FOV - FOVL) * Time.deltaTime * Compensation;
            Camera[SystemIndex].fieldOfView = FOVL;
            FOVText.text = FOV.ToString();
            FOVSlider.value = FOV;
            FOVSliderText.text = FOV.ToString();
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
        FOVSliderText.text = FOV.ToString();
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
        FOVSliderText.text = FOV.ToString();
    }

}
