
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace ArkMagellan.Relative.Tracking
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class RelativeTracking : UdonSharpBehaviour
    {


        [Header("依赖项")]
        public Transform CameraPoint;

        public UdonBehaviour Controller;

        public GameObject ControllerObject;
        [UdonSynced] private bool CanvansActive;


        [UdonSynced] private float RotationOffsetXValue;



        [UdonSynced] private float RotationOffsetYValue;




        [UdonSynced] private float RotationOffsetZValue;



        [UdonSynced] private float PositionOffsetValue;


        [UdonSynced] private float SlarpingSpeedValue;


        private float SendDataTimer;
        private bool SendDataBool;

        public GameObject Plane;
        private bool PlaneActive;

        [UdonSynced] public bool AutoTracking;
        public Transform AutoTrackingTarget;

        //旋转方向(0-1正-2反)
        [UdonSynced] private int RotationOffsetXRound;
        [UdonSynced] private int RotationOffsetYRound;
        [UdonSynced] private int RotationOffsetZRound;

        //速度
        [UdonSynced] private float RotationOffsetXRoundMult;
        [UdonSynced] private float RotationOffsetYRoundMult;
        [UdonSynced] private float RotationOffsetZRoundMult;


        void Start()
        {
            OnEnable();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (Networking.IsOwner(gameObject))
            {
                RequestSerialization();
            }
        }


        private void OnEnable()
        {
            SlarpingSpeedValue = (float)Controller.GetProgramVariable("SlarpV");
            OnDeserialization();
        }
        public override void OnDeserialization()
        {
            if (!Networking.IsOwner(gameObject))
            {

            }
            else
            {
                SendDataBool = true;
                SendDataTimer = 0f;
            }
            Controller.SetProgramVariable("SlarpV", SlarpingSpeedValue);

            if (!AutoTracking)
            {
                CameraPoint.localRotation = Quaternion.Euler(RotationOffsetXValue, RotationOffsetYValue, RotationOffsetZValue);
                CameraPoint.localPosition = new Vector3(0, 0, PositionOffsetValue);
            }

            if (Plane != null)
            {
                if (PlaneActive)
                {
                    Plane.SetActive(true);
                }
                else
                {
                    Plane.SetActive(false);
                }
            }

        }

        private void Update()
        {
            if (RotationOffsetXRound == 1)
            {
                RotationOffsetXValue += RotationOffsetXRoundMult * Time.deltaTime;
                if (RotationOffsetXValue >= 360f)
                {
                    RotationOffsetXValue -= 360f;
                }
            }
            else if (RotationOffsetXRound == 2)
            {
                RotationOffsetXValue -= RotationOffsetXRoundMult * Time.deltaTime;
                if (RotationOffsetXValue <= 0f)
                {
                    RotationOffsetXValue += 360f;
                }
            }


            if (RotationOffsetYRound == 1)
            {
                RotationOffsetYValue += RotationOffsetYRoundMult * Time.deltaTime;
                if (RotationOffsetYValue >= 360f)
                {
                    RotationOffsetYValue -= 360f;
                }
            }
            else if (RotationOffsetYRound == 2)
            {
                RotationOffsetYValue -= RotationOffsetYRoundMult * Time.deltaTime;
                if (RotationOffsetYValue <= 0f)
                {
                    RotationOffsetYValue += 360f;
                }
            }


            if (RotationOffsetZRound == 1)
            {
                RotationOffsetZValue += RotationOffsetZRoundMult * Time.deltaTime;
                if (RotationOffsetZValue >= 360f)
                {
                    RotationOffsetZValue -= 360f;
                }
            }
            else if (RotationOffsetZRound == 2)
            {
                RotationOffsetZValue -= RotationOffsetZRoundMult * Time.deltaTime;
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
                SendDataTimer += Time.deltaTime;
                if (SendDataTimer >= 5f)
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
            else
            {
                if (ControllerObject != null)
                {

                    Quaternion TrackingRotation = ControllerObject.transform.rotation; // 直接使用控制器的旋转;
                    CameraPoint.rotation = Quaternion.Euler(TrackingRotation.eulerAngles.x + RotationOffsetXValue, TrackingRotation.eulerAngles.y + RotationOffsetYValue, TrackingRotation.eulerAngles.z + RotationOffsetZValue);
                }
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
            OnDeserialization();
        }
    }
}