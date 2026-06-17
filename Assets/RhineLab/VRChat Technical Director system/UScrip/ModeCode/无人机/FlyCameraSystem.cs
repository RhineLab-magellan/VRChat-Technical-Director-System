
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

public class FlyCameraSystem : UdonSharpBehaviour
{
    [Header("飞行参数")]
    [Tooltip("飞行速度")]
    //无人机速度基础倍数
    public float DroneSpeed = 1;

    //QE每分钟旋转角度
    [Tooltip("QE每秒旋转角度")]

    public float QERotationBase = 10;
    private float QERotation = 10;

    public float QERotationMultiple = 1;

    private bool IsQERotationMultiple = false;

    //常规参数
    private bool IsUse = false;

    //上下
    private float Positionfront;

    //左右
    private float PositionRight;

    private float RotationHorizontal;

    private float RotationVertical;

    private float UpDown;

    //无人机运动向量
    private Vector3 DroneVector;



    //无人机速度乘积倍数
    private float DroneSpeedMultiple = 1;

    //无人机下机位置
    public Transform DroneDownPosition;

    //相对模式和绝对模式
    private bool IsRelativeMode = true;
    public Toggle ToggleRelativeMode;



    //无人机相关
    public GameObject Drone;
    private Transform DroneTransform;
    private Rigidbody DroneRigidbody;

    //屏幕
    public GameObject Screen;
    private Transform ScreenTransform;

    private Transform TrackingTransform;
    private Vector3 ScreenPosition;

    //PC头罩
    public GameObject HeadCover;

    //VR运动相关
    //public Transform CentreTransform;
    //public Transform VRTransform;

    //VR判断相关
    private bool IsVR;

    //本地玩家
    public VRCPlayerApi LocalPlayer;

    //回城下车
    //public Transform ReturnPosition;

    //VR无人机方位
    public Transform VRDroneTransform;
    //private Vector3 VRDronePosition;

    //速度倍率条
    public Slider SpeedMultipleSlider;
    //速度倍率文本
    public Text SpeedMultipleText;
    //缓动条
    public Slider DampSlider;
    //缓动文本
    public Text DampText;
    //缓动值
    private float DampValue = 0.1f;

    //控制面板
    public GameObject ControlPanel;
    //自动旋转
    private bool AutoRound;

    private bool ResetAutoRound = false;

    private VRCPlayerApi.TrackingData TrackingData;
    //跟踪玩家
    public Transform PlayerTracking;

    public Toggle ToggleTracking;

    [HideInInspector]
    public UdonBehaviour Managerudon;



    void Start()
    {
        DroneTransform = Drone.transform;
        ControlPanel.SetActive(false);
        DroneRigidbody = Drone.GetComponent<Rigidbody>();
        LocalPlayer = Networking.LocalPlayer;
        HeadCover.SetActive(false);
        VRDroneTransform.gameObject.SetActive(false);
        QERotation = QERotationBase;
        ScreenPosition = new Vector3(0, 0, 0.1f);
        ScreenTransform = ControlPanel.transform.parent.transform;
        TrackingTransform = ControlPanel.transform;
        TrackingTransform.localPosition = ScreenPosition;
        SpeedMultipleTextUpdate();
        DampTextUpdate();
        /*
        CentreTransform.gameObject.SetActive(false);
        VRTransform.gameObject.SetActive(false);
        */
    }

    public override void Interact()
    {
        LocalPlayer.UseAttachedStation();
    }




    //进入操控初始化
    public override void OnStationEntered(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            IsVR = Networking.LocalPlayer.IsUserInVR();
            Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
            Networking.SetOwner(Networking.LocalPlayer, Drone);
            ControlPanel.SetActive(true);
            this.transform.GetChild(0).gameObject.SetActive(false);
            IsUse = true;
            if (IsVR)
            {
                //VR操作
                HeadCover.SetActive(false);
                //CentreTransform.gameObject.SetActive(true);
                //VRTransform.gameObject.SetActive(true);
                VRDroneTransform.gameObject.SetActive(true);
            }
            else
            {
                //PC操作
                HeadCover.SetActive(true);
                //CentreTransform.gameObject.SetActive(false);
                //VRTransform.gameObject.SetActive(false);
                VRDroneTransform.gameObject.SetActive(false);
            }
        }
    }

    //退出操控
    public override void OnStationExited(VRCPlayerApi player)
    {
        if (player.isLocal)
        {
            IsUse = false;
            DroneRigidbody.velocity = new Vector3(0, 0, 0);
            DroneRigidbody.angularVelocity = new Vector3(0, 0, 0);
            Screen.SetActive(false);
            ControlPanel.SetActive(false);
            this.transform.GetChild(0).gameObject.SetActive(true);
        }
    }




    //PC操作
    public override void InputMoveHorizontal(float value, UdonInputEventArgs args)
    {
        if (IsUse)
        {
            Positionfront = value;
        }
    }

    public override void InputMoveVertical(float value, UdonInputEventArgs args)
    {
        if (IsUse)
        {
            PositionRight = value;
        }
    }

    public override void InputLookHorizontal(float value, UdonInputEventArgs args)
    {
        if (IsUse)
        {
            RotationHorizontal = value;
        }
    }

    public override void InputLookVertical(float value, UdonInputEventArgs args)
    {
        if (IsUse)
        {
            RotationVertical = value;
        }
    }

    public override void InputGrab(bool value, UdonInputEventArgs args)
    {
        if (IsUse)
        {
            if (IsVR)
            {
                if (value == false)
                {
                    UpDown = 0;
                    return;
                }
                if (args.handType == HandType.LEFT)
                {
                    UpDown = -1;
                }
            }
        }
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (IsUse)
        {
            if (IsVR)
            {
                if (value == false)
                {
                    UpDown = 0;
                    return;
                }
                if (args.handType == HandType.LEFT)
                {
                    UpDown = 1;
                }
            }
        }
    }



    /*public override void InputDrop(bool value, UdonInputEventArgs args)
    {
        InputCheck(value, args);
    }*/


    private void InputCheck(bool value, UdonInputEventArgs args)
    {
        if (IsUse)
        {
            if (IsVR)
            {
                if (value == false)
                {
                    UpDown = 0;
                    return;
                }
                if (args.handType == HandType.LEFT)
                {
                    UpDown = 1;
                }
                else
                {
                    UpDown = -1;
                }
            }
        }
    }




    private void OnEnable()
    {
        Screen.SetActive(true);
    }

    private void OnDisable()
    {
        Screen.SetActive(false);
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (IsUse)
        {
            if (player.isLocal)
            {
                OnStationExited(player);
                player.TeleportTo(DroneDownPosition.position, DroneDownPosition.rotation);
            }
        }

    }



    private void FixedUpdate()
    {
        if (IsUse)
        {
            //DroneVector（飞行方向） * DroneSpeed（基础速度） * DroneSpeedMultiple（速度乘积倍数）
            if (IsVR)
            {
                //VR操作
                DroneVector = new Vector3(Positionfront, UpDown, PositionRight);
                if (IsRelativeMode)
                {
                    DroneRigidbody.AddForce(DroneVector * DroneSpeed * DroneSpeedMultiple);
                }
                else
                {
                    DroneRigidbody.AddForce(DroneTransform.TransformDirection(DroneVector) * DroneSpeed * DroneSpeedMultiple);
                }
                if (AutoRound)
                {
                    DroneRigidbody.transform.LookAt(PlayerTracking);
                }
                else
                {

                    DroneTransform.position = VRDroneTransform.position;
                }
            }
            else
            {
                //PC操控

                //前后左右移动
                DroneVector = new Vector3(Positionfront, UpDown, PositionRight);
                //DampValue
                Vector3 DampVector = DroneRigidbody.velocity;
                DampVector = Vector3.Lerp(DampVector, DroneVector * DroneSpeed * DroneSpeedMultiple, DampValue);

                if (IsRelativeMode)
                {
                    DroneRigidbody.AddForce(DroneTransform.TransformDirection(DampVector));
                }
                else
                {
                    DroneRigidbody.AddForce(DampVector);

                }
                if (AutoRound)
                {
                    DroneRigidbody.transform.LookAt(PlayerTracking);
                }
                else
                {
                    //PC视野跟踪
                    //TrackingData = LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                    DroneRigidbody.rotation = LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
                }





            }
        }

    }

    private void Update()
    {
        if (IsUse)
        {
            if (IsVR)
            {
                //VR操作

            }
            else
            {
                //PC操控

                //上下移动
                if (Input.GetKey(KeyCode.Space))
                {
                    UpDown = 1;
                }
                else if (Input.GetKey(KeyCode.LeftShift))
                {
                    UpDown = -1;
                }
                else
                {
                    UpDown = 0;
                }
                //运动倍率

                //位置旋转
                if (Input.GetKey(KeyCode.Q))
                {
                    this.transform.Rotate(0, 360 - (QERotation * Time.deltaTime * 5), 0);
                }
                if (Input.GetKey(KeyCode.E))
                {
                    this.transform.Rotate(0, QERotation * Time.deltaTime * 5, 0);
                }
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    IsQERotationMultiple = !IsQERotationMultiple;
                    if (IsQERotationMultiple)
                    {
                        QERotation = QERotationMultiple;
                    }
                    else
                    {
                        QERotation = QERotationBase;
                    }
                }
                if (Input.GetKey(KeyCode.N))
                {
                    DroneSpeedMultiple += Time.deltaTime;
                    float DroneSpeedMultipleValue = DroneSpeedMultiple;
                    if (DroneSpeedMultipleValue < 0)
                    {
                        DroneSpeedMultiple = DroneSpeedMultipleValue;
                    }
                    SpeedMultipleSlider.SetValueWithoutNotify(DroneSpeedMultiple);
                    SpeedMultipleTextUpdate();
                }
                if (Input.GetKey(KeyCode.M))
                {
                    DroneSpeedMultiple -= Time.deltaTime;
                    if (DroneSpeedMultiple < 0)
                    {
                        DroneSpeedMultiple = 0;
                    }
                    SpeedMultipleSlider.SetValueWithoutNotify(DroneSpeedMultiple);
                    SpeedMultipleTextUpdate();
                }
                if (Input.GetKey(KeyCode.J))
                {
                    DampValue += Time.deltaTime * 0.3f;
                    if (DampValue > 1)
                    {
                        DampValue = 1;
                    }
                    DampSlider.SetValueWithoutNotify(DampValue);
                    DampTextUpdate();
                }
                if (Input.GetKey(KeyCode.K))
                {
                    DampValue -= Time.deltaTime * 0.3f;
                    if (DampValue < 0.01)
                    {
                        DampValue = 0.01f;
                    }

                    DampSlider.SetValueWithoutNotify(DampValue);
                    DampTextUpdate();
                }
                if (Input.GetKey(KeyCode.T))
                {
                    if (ResetAutoRound)
                    {
                        AutoRound = !AutoRound;
                        ResetAutoRound = false;
                        ToggleTracking.SetIsOnWithoutNotify(AutoRound);
                    }
                }
                else
                {
                    ResetAutoRound = true;
                }
                if (Input.GetKey(KeyCode.UpArrow))
                {
                    ScreenPosition += new Vector3(0, Time.deltaTime * 0.1f, 0);
                    TrackingTransform.localPosition = ScreenPosition;
                }
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    ScreenPosition += new Vector3(0, -Time.deltaTime * 0.1f, 0);
                    TrackingTransform.localPosition = ScreenPosition;
                }
                if (Input.GetKey(KeyCode.RightArrow))
                {
                    ScreenPosition += new Vector3(Time.deltaTime * 0.1f, 0, 0);
                    TrackingTransform.localPosition = ScreenPosition;
                }
                if (Input.GetKey(KeyCode.LeftArrow))
                {
                    ScreenPosition += new Vector3(-Time.deltaTime * 0.1f, 0, 0);
                    TrackingTransform.localPosition = ScreenPosition;
                }
                //切换相对模式和绝对模式
                if (Input.GetKey(KeyCode.U))
                {
                    IsRelativeMode = !IsRelativeMode;
                    ToggleRelativeMode.SetIsOnWithoutNotify(IsRelativeMode);
                }
                //
                TrackingData = LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                //Debug.Log(TrackingData.position);
                ScreenTransform.position = TrackingData.position;
                ScreenTransform.rotation = TrackingData.rotation;
                TrackingTransform.LookAt(ScreenTransform);
                //TrackingTransform.rotation = Quaternion.Euler(0, 180, 0) * TrackingTransform.rotation;

            }
        }
    }
    //速度倍率
    public void SpeedMultipleUpdate()
    {
        DroneSpeedMultiple = SpeedMultipleSlider.value;
        SpeedMultipleText.text = DroneSpeedMultiple.ToString("0.00");
    }

    //速度倍率文本更新
    private void SpeedMultipleTextUpdate()
    {
        SpeedMultipleText.text = DroneSpeedMultiple.ToString("0.00");
    }
    //缓动条更新
    public void CacheSliderUpdate()
    {
        DampValue = DampSlider.value;
        DampTextUpdate();
    }

    //缓动文本更新
    private void DampTextUpdate()
    {
        DampText.text = DampValue.ToString("0.00");
    }

    //切换跟踪玩家
    public void ToggleTrackingChanger()
    {
        AutoRound = ToggleTracking.isOn;

    }

    //切换相对模式和绝对模式
    public void ToggleRelativeModeChanger()
    {
        IsRelativeMode = ToggleRelativeMode.isOn;
    }


    /*public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (IsUse)
        {
            if (player.isLocal)
            {
                OnStationExited(player);
                player.TeleportTo(ReturnPosition.position, ReturnPosition.rotation);
            }
        }
    }*/

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (player == LocalPlayer)
        {
            Managerudon.SendCustomEvent("ChangerUsingPlayer");
            Debug.Log("OnOwnershipTransferred");
        }
    }
}
