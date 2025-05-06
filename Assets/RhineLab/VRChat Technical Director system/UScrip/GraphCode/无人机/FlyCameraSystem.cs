
using UdonSharp;
using UnityEditor.SearchService;
using UnityEngine;
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
    [Tooltip("QE每分钟旋转角度")]
    public float QERotation = 10;

    //常规参数
    private bool IsUse = false;

    //上下
    private float front;

    //左右
    private float Right;

    private float UpDown;

    //无人机运动向量
    private Vector3 DroneVector;



    //无人机速度乘积倍数
    private float DroneSpeedMultiple = 1;

    //无人机下机位置
    public Transform DroneDownPosition;



    //无人机相关
    public GameObject Drone;
    private Transform DroneTransform;
    private Rigidbody DroneRigidbody;

    //屏幕
    public GameObject Screen;

    //PC头罩
    public GameObject HeadCover;

    //VR运动相关
    public Transform CentreTransform;
    public Transform VRTransform;

    //VR判断相关
    private bool IsVR;

    //本地玩家
    public VRCPlayerApi LocalPlayer;

    void Start()
    {
        DroneTransform = Drone.transform;
        DroneRigidbody = Drone.GetComponent<Rigidbody>();
        LocalPlayer = Networking.LocalPlayer;
        HeadCover.SetActive(false);
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
            IsUse = true;
            if (IsVR)
            {
                HeadCover.SetActive(false);
                CentreTransform.gameObject.SetActive(true);
                VRTransform.gameObject.SetActive(true);
            }
            else
            {
                //PC操作
                HeadCover.SetActive(true);
                CentreTransform.gameObject.SetActive(false);
                VRTransform.gameObject.SetActive(false);
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
        }
    }

    //PC操作
    public override void InputMoveHorizontal(float value, UdonInputEventArgs args)
    {
        if (IsUse)
        {
            front = value;
        }
    }

    public override void InputMoveVertical(float value, UdonInputEventArgs args)
    {
        if (IsUse)
        {
            Right = value;
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
            if (IsVR)
            {
                //VR操作
                DroneVector = CentreTransform.position - VRTransform.position;
                DroneRigidbody.AddForce(DroneVector * DroneSpeed * DroneSpeedMultiple);

            }
            else
            {
                //PC操控

                //前后左右移动
                DroneVector = new Vector3(-front, UpDown, -Right);

                DroneRigidbody.AddRelativeForce(DroneVector * DroneSpeed * DroneSpeedMultiple);

                //PC视野跟踪

                Quaternion Rotation = LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
                DroneTransform.rotation = Rotation;

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
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    UpDown = 1;
                }
                else if (Input.GetKey(KeyCode.LeftAlt))
                {
                    UpDown = -1;
                }
                else
                {
                    UpDown = 0;
                }
                //运动倍率
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    DroneSpeedMultiple = 5f;
                }
                else
                {
                    DroneSpeedMultiple = 1f;
                }
                //位置旋转
                if (Input.GetKey(KeyCode.Q))
                {
                    this.transform.Rotate(0, 360 - (QERotation * Time.deltaTime), 0);
                }
                if (Input.GetKey(KeyCode.E))
                {
                    this.transform.Rotate(0, QERotation * Time.deltaTime, 0);
                }
            }
        }
    }

}
