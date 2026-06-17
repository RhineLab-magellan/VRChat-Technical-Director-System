using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Common;
using UnityEngine.UI;
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class RelativeControl : UdonSharpBehaviour
{
    [Tooltip("相对位置追踪点")]
    public Transform CameraPoint;

    public Transform PlayerTracking;

    public Transform Transform;

    public GameObject CenterPointMoves;

    [Tooltip("相对角度追踪点")]
    private Transform CameraRotationPoint;
    [UdonSynced] public float MultiplyValue;

    public Slider MultiplySlider;
    public Text MultiplyText;

    [UdonSynced] public bool AutoRound;
    //Tracking 0 = 玩家 1 = 中心点

    [UdonSynced] private bool Tracking;

    [UdonSynced] private bool Isoverlay;
    public Toggle IsOverlayToggle;


    //PickUp
    public bool IsPickUp;

    public GameObject Screen;

    private VRCPlayerApi LocalPlayer;

    public UdonBehaviour Managerudon;


    void Start()
    {
        LocalPlayer = Networking.LocalPlayer;
        MultiplySlider.value = MultiplyValue;

        //Transform = CameraPoint.parent;
    }

    void Update()
    {
        Vector3 MultiplyVector = this.transform.localPosition;
        MultiplyVector.x *= MultiplyValue;
        MultiplyVector.y *= MultiplyValue;
        MultiplyVector.z *= MultiplyValue;
        CameraPoint.localPosition = MultiplyVector;
        if (AutoRound)
        {
            if (Tracking)
            {
                CameraPoint.LookAt(PlayerTracking);
                if (Isoverlay)
                {
                    float zAngle = this.transform.rotation.eulerAngles.z;
                    // 创建绕 Z 轴旋转的四元数
                    Quaternion zRotation = Quaternion.Euler(0, 0, zAngle);
                    // 叠加旋转
                    CameraPoint.rotation = zRotation * CameraPoint.rotation;
                }

            }
            else
            {
                CameraPoint.LookAt(Transform);
                if (Isoverlay)
                {
                    float zAngle = this.transform.rotation.eulerAngles.z;
                    // 创建绕 Z 轴旋转的四元数
                    Quaternion zRotation = Quaternion.Euler(0, 0, zAngle);
                    // 叠加旋转
                    CameraPoint.rotation = zRotation * CameraPoint.rotation;
                }
            }
        }
        else
        {
            CameraPoint.rotation = this.transform.rotation;
        }
    }

    public void OnEnable()
    {
        Screen.SetActive(true);
        if (CenterPointMoves != null)
        {
            CenterPointMoves.SetActive(true);
        }
    }
    public void OnDisable()
    {
        Screen.SetActive(false);
        if (CenterPointMoves != null)
        {
            CenterPointMoves.SetActive(false);
        }
    }

    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        if (!IsPickUp)
        {
            return;
        }
        if (value == false)
        {
            return;
        }

        else if (args.handType == HandType.LEFT)
        {
            AutoRound = !AutoRound;
        }
        Debug.Log(Tracking + "" + AutoRound);
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.All, nameof(TrackingChanger), Tracking, AutoRound);
        //RequestSerialization();
    }

    public override void InputGrab(bool value, UdonInputEventArgs args)
    {
        if (!IsPickUp)
        {
            return;
        }
        if (value == false)
        {
            return;
        }
        if (args.handType == HandType.LEFT)
        {
            Tracking = !Tracking;
            NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.All, nameof(TrackingChanger), Tracking, AutoRound);
            //RequestSerialization();
        }
    }


    public override void OnPickup()
    {
        IsPickUp = true;
    }

    public override void OnDrop()
    {
        IsPickUp = false;
    }

    [NetworkCallable]
    public void TrackingChanger(bool Tracking, bool AutoRound)
    {
        this.Tracking = Tracking;
        this.AutoRound = AutoRound;
    }

    public void MultiplySliderChanger()
    {
        MultiplyValue = MultiplySlider.value;
        MultiplyText.text = MultiplyValue.ToString("0.00");
    }
    public void IsOverlayChanger()
    {
        Isoverlay = IsOverlayToggle.isOn;
        //RequestSerialization();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (player == LocalPlayer)
        {
            Managerudon.SendCustomEvent("ChangerUsingPlayer");
            Debug.Log("OnOwnershipTransferred");
        }
    }


}
