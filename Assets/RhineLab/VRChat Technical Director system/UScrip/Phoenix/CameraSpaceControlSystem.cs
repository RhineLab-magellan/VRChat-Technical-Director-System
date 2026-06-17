
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CameraSpaceControlSystem : UdonSharpBehaviour
{
    public UdonBehaviour MainUdon;

    //相机目标
    private GameObject[] SystemUdon;
    private UdonBehaviour[] Targets;

    //相机画面
    private RenderTexture[] CameraRender;

    //显示器
    public MeshRenderer CameraDisplay;

    //相机位置滑块
    public Slider[] CameraSlider;

    //Slider数值显示
    public Text[] CameraSliderText;
    //相机旋转滑块
    public Slider[] CameraRotationSlider;
    //Slider数值显示
    public Text[] CameraRotationSliderText;

    //相机索引
    private int CameraIndex = 0;

    public Text CameraIndexText;
    //相机位置
    private Vector3 transformPosition;
    //相机旋转
    private Quaternion transformRotation;

    //速度系统
    /*
        public Toggle[] UpToggle;
        public Toggle[] DownToggle;

        private Image[] UpImage;
        private Image[] DownImage;

        private bool[] UpBool;
        private bool[] DownBool;

        //速度滑条
        public Slider[] SpeedSlider;

        private float[] Speed;

    */
    //
    public Toggle[] PositionToggle;
    public Toggle[] RotationToggle;

    //Slide位置标识
    public Toggle[] ExponentToggle;
    //
    private short Exponent = -1;
    public Text ExponentText;

    //指代Slider
    private short[] TempInt = new short[0];
    private Slider TempSlider;

    //指数
    public Slider ExSlider;
    private short ExValue = 0;

    public Text ExValueText;
    //倍数
    public Slider ESlider;
    private float EValue = 0;
    public Text EValueText;

    //
    private float CorrectionValue = 1f;

    //手柄控制
    public Toggle HandControlToggle;
    private bool HandControlState = false;

    private bool KeyboardControlState = false;

    private bool IsVR = false;

    private float HandControlValue = 0;

    private short TempI;

    private short TempBool = -1;
    //PositionSpeed
    public Slider PositionSpeedSlider;
    private float PostitonSpeed = 0.1f;

    //RotationSpeed
    public Slider RotationSpeedSlider;
    private float RotationSpeed = 0.1f;

    //PositionSpeedText
    public Text PositionSpeedText;
    //RotationSpeedText
    public Text RotationSpeedText;


    void Start()
    {
        //更新文本
        SystemUdon = (GameObject[])MainUdon.GetProgramVariable("System");
        CameraIndexText.text = CameraIndex.ToString();
        IsVR = Networking.LocalPlayer.IsUserInVR();
        HandControlChange();
        SendCustomEventDelayedFrames(nameof(Start1), 2);
        //获取速度系统组件
        /*
        UpImage = new Image[UpToggle.Length];
        DownImage = new Image[DownToggle.Length];
        for (int i = 0; i < UpToggle.Length; i++)
        {
            UpImage[i] = UpToggle[i].GetComponent<Image>();
            DownImage[i] = DownToggle[i].GetComponent<Image>();
        }
        */
    }
    public void Start1()
    {
        Targets = new UdonBehaviour[SystemUdon.Length];
        CameraRender = (RenderTexture[])MainUdon.GetProgramVariable("CameraRender");
        CameraDisplay.material.SetTexture("_MainTex", CameraRender[CameraIndex]);
        for (int i = 0; i < SystemUdon.Length; i++)
        {
            Targets[i] = SystemUdon[i].GetComponent<ControlCenter>().CAMTransform.GetComponent<UdonBehaviour>();
        }
        OnPositionSpeedSlider();
        UpdatePositionSliderText();
        UpdateRotationSliderText();
    }
    //当位置更新
    public void OnPositionSlider()
    {
        transformPosition = new Vector3(CameraSlider[0].value, CameraSlider[1].value, CameraSlider[2].value);
        SetTarget();
        UpdatePositionSliderText();
    }

    //当旋转更新
    public void OnRotationSlider()
    {
        transformRotation = Quaternion.Euler(CameraRotationSlider[0].value, CameraRotationSlider[1].value, CameraRotationSlider[2].value);
        SetTarget();
        UpdateRotationSliderText();
    }
    public void SetTarget()
    {
        UdonBehaviour target = Targets[CameraIndex];
        if (!Networking.IsOwner(target.gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, target.gameObject);
        }
        target.SetProgramVariable("Position", transformPosition);
        target.SetProgramVariable("Rotation", transformRotation);
        target.SendCustomEvent("CallNetworkSerialization");
    }

    //选则相机
    public void IndexUp()
    {
        CameraIndex++;
        if (CameraIndex >= Targets.Length)
        {
            CameraIndex = 0;
        }
        //更新文本
        CameraIndexText.text = CameraIndex.ToString();
        ResetParameter();
    }

    //选则相机
    public void IndexDown()
    {
        if (CameraIndex > 0)
        {
            CameraIndex = Targets.Length - 1;
        }
        else
        {
            CameraIndex = Targets.Length;
        }
        //更新文本
        CameraIndexText.text = CameraIndex.ToString();
        ResetParameter();
    }
    //重新设置参数
    public void ResetParameter()
    {
        if (CameraIndex >= Targets.Length)
        {
            CameraIndex = Targets.Length - 1;
        }
        UdonBehaviour target = Targets[CameraIndex];
        target.SendCustomEvent("CheckOwner");
        target.SendCustomEvent("GetTransform");
        transformPosition = (Vector3)target.GetProgramVariable("Position");
        transformRotation = (Quaternion)target.GetProgramVariable("Rotation");
        //更新滑块
        CameraSlider[0].SetValueWithoutNotify(transformPosition.x);
        CameraSlider[1].SetValueWithoutNotify(transformPosition.y);
        CameraSlider[2].SetValueWithoutNotify(transformPosition.z);
        Vector3 eulerAngles = transformRotation.eulerAngles;
        CameraRotationSlider[0].SetValueWithoutNotify(eulerAngles.x);
        CameraRotationSlider[1].SetValueWithoutNotify(eulerAngles.y);
        CameraRotationSlider[2].SetValueWithoutNotify(eulerAngles.z);
        //更新文本
        UpdatePositionSliderText();
        UpdateRotationSliderText();
        //更新相机画面
        CameraDisplay.material.SetTexture("_MainTex", CameraRender[CameraIndex]);
    }

    //更新Slider的文本数值显示w
    public void UpdatePositionSliderText()
    {
        CameraSliderText[0].text = CameraSlider[0].value.ToString("0.00");
        CameraSliderText[1].text = CameraSlider[1].value.ToString("0.00");
        CameraSliderText[2].text = CameraSlider[2].value.ToString("0.00");
    }

    private void UpdateRotationSliderText()
    {
        CameraRotationSliderText[0].text = CameraRotationSlider[0].value.ToString("0.00");
        CameraRotationSliderText[1].text = CameraRotationSlider[1].value.ToString("0.00");
        CameraRotationSliderText[2].text = CameraRotationSlider[2].value.ToString("0.00");
    }

    public void PositionReset()
    {
        int PositionIndex = -1;
        for (int i = 0; i < PositionToggle.Length; i++)
        {
            if (PositionToggle[i].isOn)
            {
                PositionIndex = i;
                PositionToggle[i].SetIsOnWithoutNotify(false);
                break;
            }
        }
        if (PositionIndex == -1)
        {
            return;
        }
        CameraSlider[PositionIndex].SetValueWithoutNotify(0);
        OnPositionSlider();
    }

    public void RotationReset()
    {
        int RotationIndex = -1;
        for (int i = 0; i < RotationToggle.Length; i++)
        {
            if (RotationToggle[i].isOn)
            {
                RotationIndex = i;
                RotationToggle[i].SetIsOnWithoutNotify(false);
                break;
            }
        }
        if (RotationIndex == -1)
        {
            return;
        }
        CameraRotationSlider[RotationIndex].SetValueWithoutNotify(0);
        OnRotationSlider();
    }

    public void ReTryOwner()
    {
        UdonBehaviour target = Targets[CameraIndex];
        if (Networking.IsOwner(target.gameObject))
        {
            return;
        }
        Networking.SetOwner(Networking.LocalPlayer, target.gameObject);
        target.SendCustomEvent("CheckOwner");
        target.SendCustomEvent("GetTransform");
    }

    public void ExponentToggleUpdate()
    {
        short ExponentIndex = -1;
        for (short i = 0; i < ExponentToggle.Length; i++)
        {
            if (ExponentToggle[i].isOn)
            {
                ExponentIndex = i;
                ExponentToggle[i].SetIsOnWithoutNotify(false);
                break;
            }
        }
        for (short i = 0; i < ExponentToggle.Length; i++)
        {
            ExponentToggle[i].SetIsOnWithoutNotify(false);
        }
        if (ExponentIndex == -1)
        {
            return;
        }

        Exponent = (short)(ExponentIndex + 1);
        //1:新参数 0:反转参数 -1:删除参数
        short IsNew = 1;

        for (short i = 0; i < TempInt.Length; i++)
        {
            if (TempInt[i] == -Exponent)
            {
                TempInt[i] = 0;
                IsNew = -1;
            }
            if (TempInt[i] == Exponent)
            {
                TempInt[i] = (short)-Exponent;
                IsNew = 0;
            }
        }
        //新参数——扩容并新增参数
        if (IsNew == 1)
        {
            short[] TempIntNew = new short[TempInt.Length + 1];
            for (short i = 0; i < TempInt.Length; i++)
            {
                TempIntNew[i] = TempInt[i];
            }
            TempIntNew[TempInt.Length] = Exponent;
            TempInt = TempIntNew;
        }
        //删除参数
        if (IsNew == -1)
        {
            short[] TempIntNew = new short[TempInt.Length - 1];
            short j = 0;
            for (short i = 0; i < TempInt.Length; i++)
            {
                if (TempInt[i] == 0)
                {
                    continue;
                }
                TempIntNew[j] = TempInt[i];
                j++;
            }
            TempInt = TempIntNew;
        }

        ExponentText.text = "-";
        for (short i = 0; i < TempInt.Length; i++)
        {
            ExponentText.text += TempInt[i].ToString() + ",";
        }
    }

    public void OnExSlider()
    {
        ExValue = (short)ExSlider.value;
        ExValueText.text = ExValue.ToString();
    }

    public void OnESlider()
    {
        EValue = ESlider.value;
        EValueText.text = EValue.ToString("0.00");
    }

    public void Update()
    {
        if (KeyboardControlState)
        {
            KeyBoardContol();
        }
        if (Exponent == -1)
        {
            return;
        }
        if (ExValue == 0)
        {
            return;
        }
        for (short i = 0; i < TempInt.Length; i++)
        {

            TempI = TempInt[i];
            if (TempI < 0)
            {
                TempBool = -1;
                TempI = (short)(-TempI - 1);
            }
            else
            {
                TempBool = 1;
                TempI -= 1;
            }
            if (TempI < 3)
            {
                TempSlider = CameraSlider[TempI];
                CorrectionValue = PostitonSpeed;
            }
            else
            {
                TempSlider = CameraRotationSlider[TempI - 3];
                if (TempSlider.value == 181 || TempSlider.value == -181)
                {
                    TempSlider.SetValueWithoutNotify(-TempSlider.value);
                }
                CorrectionValue = RotationSpeed;
            }

            if (HandControlState)
            {
                TempSlider.value = TempSlider.value + HandControlValue * ExValue * EValue * Time.deltaTime * CorrectionValue * TempBool;
            }
            else
            {
                TempSlider.value = TempSlider.value + ExValue * EValue * Time.deltaTime * CorrectionValue * TempBool;
            }
        }

    }

    private void KeyBoardContol()
    {
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            ExponentToggle[0].SetIsOnWithoutNotify(true);
            ExponentToggleUpdate();
        }

        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            ExponentToggle[1].SetIsOnWithoutNotify(true);
            ExponentToggleUpdate();
        }

        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            ExponentToggle[2].SetIsOnWithoutNotify(true);
            ExponentToggleUpdate();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            ExponentToggle[3].SetIsOnWithoutNotify(true);
            ExponentToggleUpdate();
        }

        if (Input.GetKeyDown(KeyCode.Semicolon))
        {
            ExponentToggle[4].SetIsOnWithoutNotify(true);
            ExponentToggleUpdate();
        }

        if (Input.GetKeyDown(KeyCode.Quote))
        {
            ExponentToggle[5].SetIsOnWithoutNotify(true);
            ExponentToggleUpdate();
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            ExSlider.SetValueWithoutNotify((ExSlider.value + 1) % 2);
            OnExSlider();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            int Temp = (int)ExSlider.value - 1;
            if (Temp < -1)
            {
                Temp = 0;
            }
            ExSlider.SetValueWithoutNotify(Temp);
            OnExSlider();
        }
    }

    public void HandControlChange()
    {
        if (!IsVR)
        {
            KeyboardControlState = HandControlToggle.isOn;
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

    public void OnPositionSpeedSlider()
    {
        PostitonSpeed = PositionSpeedSlider.value;
        PositionSpeedText.text = PostitonSpeed.ToString("0.00");
    }

    public void OnRotationSpeedSlider()
    {
        RotationSpeed = RotationSpeedSlider.value;
        RotationSpeedText.text = RotationSpeed.ToString("0.00");
    }

    /*
    public void OnUpToggleUpdate()
    {
        bool UpBoolTemp;
        for (int i = 0; i < UpToggle.Length; i++)
        {
            UpBoolTemp = UpToggle[i].isOn;
            if(DownBool[i]==UpBoolTemp)
            {
                UpBool[i] = false;
                DownBool[i] = false;
            }
            else
            {
                UpBool[i] = UpBoolTemp;
            }
        }
        UpdateSpeedSystemIcon();

    }

    public void OnDownToggleUpdate()
    {
        bool DownBoolTemp;
        for (int i = 0; i < DownToggle.Length; i++)
        {
            DownBoolTemp = DownToggle[i].isOn;
            if(DownBoolTemp==UpBool[i])
            {
                UpBool[i] = false;
                DownBool[i] = false;
            }
            else
            {
                DownBool[i] = DownBoolTemp;
            }
            UpdateSpeedSystemIcon();

        }

    }
            
        //更新速度系统图标
        private void UpdateSpeedSystemIcon()
        {
            for (int i = 0; i < UpToggle.Length; i++)
            {
                if(UpBool[i])
                {
                    UpImage[i].color = Color.green;
                }
                else
                {
                    UpImage[i].color = Color.white;
                }
            }
                for (int i = 0; i < DownToggle.Length; i++)
                {
                    if(DownBool[i])
                    {
                        DownImage[i].color = Color.green;
                    }
                    else
                    {
                        DownImage[i].color = Color.white;
                    }
                }
        }
    

    */

}
