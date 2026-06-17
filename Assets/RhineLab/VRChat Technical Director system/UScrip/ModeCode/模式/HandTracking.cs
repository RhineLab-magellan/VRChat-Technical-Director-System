using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class HandTracking : UdonSharpBehaviour
{


    [Header("依赖项")]
    public Transform CameraPoint;

    public UdonBehaviour Controller;
    [Tooltip("UI面板")]
    public GameObject Canvans;
    [UdonSynced] private bool CanvansActive;

    public Slider RotationOffsetX;
    [UdonSynced] private float RotationOffsetXValue;
    public Text RotationOffsetXText;

    public Slider RotationOffsetY;
    [UdonSynced] private float RotationOffsetYValue;

    public Text RotationOffsetYText;

    public Slider RotationOffsetZ;
    [UdonSynced] private float RotationOffsetZValue;

    public Text RotationOffsetZText;

    public Slider PositionOffset;
    [UdonSynced] private float PositionOffsetValue;

    public Text PositionOffsetText;

    public Slider SlarpingSpeed;
    [UdonSynced] private float SlarpingSpeedValue;
    public Text SlarpingSpeedText;

    private float SendDataTimer;
    private bool SendDataBool;

    public GameObject Plane;
    private bool PlaneActive;

    public Image ImagePlane;

    [UdonSynced] public bool AutoTracking;
    public Transform AutoTrackingTarget;

    public Image AutoTrackingImage;

    //旋转方向(0-1正-2反)
    [UdonSynced] public int RotationOffsetXRound;
    [UdonSynced] private int RotationOffsetYRound;
    [UdonSynced] private int RotationOffsetZRound;

    //速度
    [UdonSynced] public float RotationOffsetXRoundMult;
    [UdonSynced] private float RotationOffsetYRoundMult;
    [UdonSynced] private float RotationOffsetZRoundMult;

    private float DeltaTime = 0;

    //public UdonBehaviour Managerudon;
    //private VRCPlayerApi LocalPlayer;

    void Start()
    {
        //LocalPlayer = Networking.LocalPlayer;
        OnEnable();
    }

    private void OnEnable()
    {
        SlarpingSpeedValue = (float)Controller.GetProgramVariable("SlarpV");
        //NetworkingUpdate();
        OnDeserialization();
    }

    public void ToggleCanvans()
    {
        CanvansActive = !CanvansActive;
        Canvans.SetActive(CanvansActive);
    }

    public void TogglePlane()
    {
        PlaneActive = !PlaneActive;
        Plane.SetActive(PlaneActive);
        if (PlaneActive)
        {
            ImagePlane.color = Color.white;
        }
        else
        {
            ImagePlane.color = Color.black;
        }
    }

    public void ToggleAutoTracking()
    {
        AutoTracking = !AutoTracking;
        if (AutoTracking)
        {
            AutoTrackingImage.color = Color.white;
        }
        else
        {
            AutoTrackingImage.color = Color.black;
        }
        NetworkingUpdate();
        OnDeserialization();
    }
    public void UpdateRotationOffsetX()
    {


        RotationOffsetXValue = RotationOffsetX.value;
        NetworkingUpdate();
        OnDeserialization();
    }

    public void UpdateRotationOffsetX0()
    {
        RotationOffsetXValue = 0;
        NetworkingUpdate();
        OnDeserialization();
    }
    public void UpdateRotationOffsetY()
    {

        RotationOffsetYValue = RotationOffsetY.value;
        NetworkingUpdate();
        OnDeserialization();
    }

    public void UpdateRotationOffsetY0()
    {
        RotationOffsetYValue = 0;
        NetworkingUpdate();
        OnDeserialization();
    }
    public void UpdateRotationOffsetZ()
    {
        RotationOffsetZValue = RotationOffsetZ.value;
        NetworkingUpdate();
        OnDeserialization();
    }
    public void UpdateRotationOffsetZ0()
    {
        RotationOffsetZValue = 0;
        NetworkingUpdate();
        OnDeserialization();
    }
    public void UpdatePositionOffset()
    {
        PositionOffsetValue = PositionOffset.value;
        NetworkingUpdate();
        OnDeserialization();
    }
    public void UpdatePositionOffset0()
    {
        PositionOffsetValue = 0;
        NetworkingUpdate();
        OnDeserialization();
    }

    public void UpdateSlarpingSpeed()
    {
        SlarpingSpeedValue = SlarpingSpeed.value;
        NetworkingUpdate();
        OnDeserialization();
    }

    private void NetworkingUpdate()
    {
        SendDataBool = true;
        SendDataTimer = 0f;
    }

    public override void OnDeserialization()
    {
        if (!Networking.IsOwner(gameObject))
        {
            RotationOffsetX.SetValueWithoutNotify(RotationOffsetXValue);
            RotationOffsetY.SetValueWithoutNotify(RotationOffsetYValue);
            RotationOffsetZ.SetValueWithoutNotify(RotationOffsetZValue);
            PositionOffset.SetValueWithoutNotify(PositionOffsetValue);
            SlarpingSpeed.SetValueWithoutNotify(SlarpingSpeedValue);
        }
        Controller.SetProgramVariable("SlarpV", SlarpingSpeedValue);

        if (!AutoTracking)
        {
            CameraPoint.localRotation = Quaternion.Euler(RotationOffsetXValue, RotationOffsetYValue, RotationOffsetZValue);
            CameraPoint.localPosition = new Vector3(0, 0, PositionOffsetValue);
        }
        RotationOffsetXText.text = RotationOffsetXValue.ToString("0.00");
        RotationOffsetYText.text = RotationOffsetYValue.ToString("0.00");
        RotationOffsetZText.text = RotationOffsetZValue.ToString("0.00");
        PositionOffsetText.text = PositionOffsetValue.ToString("0.00");
        SlarpingSpeedText.text = SlarpingSpeedValue.ToString("0.00");
        if (PlaneActive)
        {
            ImagePlane.color = Color.white;
        }
        else
        {
            ImagePlane.color = Color.black;
        }
        if (AutoTracking)
        {
            AutoTrackingImage.color = Color.white;
        }
        else
        {
            AutoTrackingImage.color = Color.black;
        }

    }

    private void Update()
    {
        DeltaTime = Time.deltaTime;
        if (RotationOffsetXRound == 1)
        {
            RotationOffsetXValue += RotationOffsetXRoundMult * DeltaTime;
            if (RotationOffsetXValue >= 360f)
            {
                RotationOffsetXValue -= 360f;
            }
        }
        else if (RotationOffsetXRound == 2)
        {
            RotationOffsetXValue -= RotationOffsetXRoundMult * DeltaTime;
            if (RotationOffsetXValue <= 0f)
            {
                RotationOffsetXValue += 360f;
            }
        }


        if (RotationOffsetYRound == 1)
        {
            RotationOffsetYValue += RotationOffsetYRoundMult * DeltaTime;
            if (RotationOffsetYValue >= 360f)
            {
                RotationOffsetYValue -= 360f;
            }
        }
        else if (RotationOffsetYRound == 2)
        {
            RotationOffsetYValue -= RotationOffsetYRoundMult * DeltaTime;
            if (RotationOffsetYValue <= 0f)
            {
                RotationOffsetYValue += 360f;
            }
        }


        if (RotationOffsetZRound == 1)
        {
            RotationOffsetZValue += RotationOffsetZRoundMult * DeltaTime;
            if (RotationOffsetZValue >= 360f)
            {
                RotationOffsetZValue -= 360f;
            }
        }
        else if (RotationOffsetZRound == 2)
        {
            RotationOffsetZValue -= RotationOffsetZRoundMult * DeltaTime;
            if (RotationOffsetZValue <= 0f)
            {
                RotationOffsetZValue += 360f;
            }
        }

        if (RotationOffsetXRound != 0 || RotationOffsetYRound != 0 || RotationOffsetZRound != 0)
        {
            // 这里添加需要执行的语句
            CameraPoint.localRotation = Quaternion.Euler(RotationOffsetXValue, RotationOffsetYValue, RotationOffsetZValue);
            CameraPoint.localPosition = new Vector3(0, 0, PositionOffsetValue);

        }


        if (SendDataBool)
        {
            SendDataTimer += DeltaTime;
            if (SendDataTimer >= 1f)
            {
                SendDataTimer = 0f;
                SendData();
                SendDataBool = false;
            }
        }

        if (AutoTracking)
        {
            Quaternion TrackingRotation = Quaternion.LookRotation(AutoTrackingTarget.position - CameraPoint.position);



            CameraPoint.rotation = Quaternion.Euler(TrackingRotation.eulerAngles.x + RotationOffsetXValue, TrackingRotation.eulerAngles.y + RotationOffsetYValue, TrackingRotation.eulerAngles.z + RotationOffsetZValue);
        }





    }

    private void SendData()
    {
        if (!Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        RequestSerialization();
    }

    public void AnotherUse()
    {
        RotationOffsetX.SetValueWithoutNotify(RotationOffsetXValue);
        RotationOffsetY.SetValueWithoutNotify(RotationOffsetYValue);
        RotationOffsetZ.SetValueWithoutNotify(RotationOffsetZValue);
        PositionOffset.SetValueWithoutNotify(PositionOffsetValue);
        SlarpingSpeed.SetValueWithoutNotify(SlarpingSpeedValue);
        OnDeserialization();
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject))
        {
            RequestSerialization();
        }
    }

    /*public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (player == LocalPlayer)
        {
            Managerudon.SendCustomEvent("ChangerUsingPlayer");
            Debug.Log("OnOwnershipTransferred");
        }
    }*/
}
