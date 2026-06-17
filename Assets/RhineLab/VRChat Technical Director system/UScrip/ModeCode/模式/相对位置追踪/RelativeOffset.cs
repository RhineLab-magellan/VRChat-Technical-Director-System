
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace ArkMagellan.Relative.Offset
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class RelativeOffset : UdonSharpBehaviour
    {


        [Header("依赖项")]

        public GameObject[] CameraPoint;
        [UdonSynced] private int CameraPointIndex;

        public Text IndexText;
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
        [UdonSynced] private float SlarpingSpeedValue = 1f;
        public Text SlarpingSpeedText;

        private float SendDataTimer;
        private bool SendDataBool;

        private bool PlaneActive;

        public Image ImagePlane;

        [UdonSynced] private bool AutoTracking;

        public Image AutoTrackingImage;
        //旋转方向(0-1正-2反)
        [UdonSynced] private int RotationOffsetXRound;
        [UdonSynced] private int RotationOffsetYRound;
        [UdonSynced] private int RotationOffsetZRound;

        public Text RotationOffsetXRoundText;
        public Text RotationOffsetYRoundText;

        public Text RotationOffsetZRoundText;

        //速度
        [UdonSynced] private float RotationOffsetXRoundMult;
        [UdonSynced] private float RotationOffsetYRoundMult;
        [UdonSynced] private float RotationOffsetZRoundMult;

        public Text RotationOffsetXRoundMultText;
        public Text RotationOffsetYRoundMultText;
        public Text RotationOffsetZRoundMultText;

        //设置速度

        //指代需要设置的速度

        public Slider RotationOffsetRoundSliderX;

        public Text RotationOffsetRoundSliderTextX;
        public Slider RotationOffsetRoundSliderY;
        public Text RotationOffsetRoundSliderTextY;
        public Slider RotationOffsetRoundSliderZ;
        public Text RotationOffsetRoundSliderTextZ;

        public UdonBehaviour Udon1;

        private bool UseRightHand;

        public Image RightHandImage;



        void Start()
        {
            SendCustomEventDelayedSeconds(nameof(IndexChanger), 1f);
        }

        private void OnEnable()
        {
            OnDeserialization();
        }

        public void IndexUp()
        {
            CameraPointIndex++;
            if (CameraPointIndex >= CameraPoint.Length)
            {
                CameraPointIndex = 0;
            }
            IndexChanger();
        }

        public void IndexDown()
        {
            CameraPointIndex--;
            if (CameraPointIndex < 0)
            {
                CameraPointIndex = CameraPoint.Length - 1;
            }
            IndexChanger();
        }

        public void IndexChanger()
        {
            UdonBehaviour Udon = CameraPoint[CameraPointIndex].GetComponent<UdonBehaviour>();
            Udon1 = Udon;
            RotationOffsetXValue = (float)Udon.GetProgramVariable(nameof(RotationOffsetXValue));
            RotationOffsetYValue = (float)Udon.GetProgramVariable(nameof(RotationOffsetYValue));
            RotationOffsetZValue = (float)Udon.GetProgramVariable(nameof(RotationOffsetZValue));
            PositionOffsetValue = (float)Udon.GetProgramVariable(nameof(PositionOffsetValue));
            SlarpingSpeedValue = (float)Udon.GetProgramVariable(nameof(SlarpingSpeedValue));
            PlaneActive = (bool)Udon.GetProgramVariable(nameof(PlaneActive));
            AutoTracking = (bool)Udon.GetProgramVariable(nameof(AutoTracking));

            RotationOffsetXRound = (int)Udon.GetProgramVariable(nameof(RotationOffsetXRound));
            RotationOffsetYRound = (int)Udon.GetProgramVariable(nameof(RotationOffsetYRound));
            RotationOffsetZRound = (int)Udon.GetProgramVariable(nameof(RotationOffsetZRound));
            RotationOffsetXRoundMult = (float)Udon.GetProgramVariable(nameof(RotationOffsetXRoundMult));
            RotationOffsetYRoundMult = (float)Udon.GetProgramVariable(nameof(RotationOffsetYRoundMult));
            RotationOffsetZRoundMult = (float)Udon.GetProgramVariable(nameof(RotationOffsetZRoundMult));

            RotationOffsetRoundSliderX.SetValueWithoutNotify(RotationOffsetXRoundMult);
            RotationOffsetRoundSliderY.SetValueWithoutNotify(RotationOffsetYRoundMult);
            RotationOffsetRoundSliderZ.SetValueWithoutNotify(RotationOffsetZRoundMult);





            RotationOffsetX.SetValueWithoutNotify(RotationOffsetXValue);
            RotationOffsetY.SetValueWithoutNotify(RotationOffsetYValue);
            RotationOffsetZ.SetValueWithoutNotify(RotationOffsetZValue);
            PositionOffset.SetValueWithoutNotify(PositionOffsetValue);
            SlarpingSpeed.SetValueWithoutNotify(SlarpingSpeedValue);
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
            OwnerCheck();
            OnDeserialization();
        }

        public void ToggleCanvans()
        {
            CanvansActive = !CanvansActive;
            Canvans.SetActive(CanvansActive);
            OwnerCheck();
            OnDeserialization();
        }

        public void TogglePlane()
        {
            PlaneActive = !PlaneActive;
            if (PlaneActive)
            {
                ImagePlane.color = Color.white;
            }
            else
            {
                ImagePlane.color = Color.black;
            }
            OwnerCheck();
            OnDeserialization();
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
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdateRotationOffsetX()
        {
            RotationOffsetXValue = RotationOffsetX.value;
            OwnerCheck();
            OnDeserialization();
        }

        public void UpdateRotationOffsetX0()
        {
            RotationOffsetXValue = 0;
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdateRotationOffsetY()
        {
            OwnerCheck();
            RotationOffsetYValue = RotationOffsetY.value;
            OnDeserialization();
        }

        public void UpdateRotationOffsetY0()
        {
            RotationOffsetYValue = 0;
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdateRotationOffsetZ()
        {
            RotationOffsetZValue = RotationOffsetZ.value;
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdateRotationOffsetZ0()
        {
            RotationOffsetZValue = 0;
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdatePositionOffset()
        {
            PositionOffsetValue = PositionOffset.value;
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdatePositionOffset0()
        {
            PositionOffsetValue = 0;
            OwnerCheck();
            OnDeserialization();
        }

        public void UpdateSlarpingSpeed()
        {
            SlarpingSpeedValue = SlarpingSpeed.value;
            OwnerCheck();
            OnDeserialization();
        }
        #region 自动旋转相关

        public void UpdateRotationOffsetXRoundUp()
        {
            if (RotationOffsetXRound == 1)
            {
                RotationOffsetXRound = 0;
            }
            else
            {
                RotationOffsetXRound = 1;
            }
            OwnerCheck();
            OnDeserialization();
        }

        public void UpdateRotationOffsetXRoundDown()
        {
            if (RotationOffsetXRound == 2)
            {
                RotationOffsetXRound = 0;
            }
            else
            {
                RotationOffsetXRound = 2;
            }
            OwnerCheck();
            OnDeserialization();
        }

        public void UpdateRotationOffsetYRoundUp()
        {
            if (RotationOffsetYRound == 1)
            {
                RotationOffsetYRound = 0;
            }
            else
            {
                RotationOffsetYRound = 1;
            }
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdateRotationOffsetYRoundDown()
        {
            if (RotationOffsetYRound == 2)
            {
                RotationOffsetYRound = 0;
            }
            else
            {
                RotationOffsetYRound = 2;
            }
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdateRotationOffsetZRoundUp()
        {
            if (RotationOffsetZRound == 1)
            {
                RotationOffsetZRound = 0;
            }
            else
            {
                RotationOffsetZRound = 1;
            }
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdateRotationOffsetZRoundDown()
        {
            if (RotationOffsetZRound == 2)
            {
                RotationOffsetZRound = 0;
            }
            else
            {
                RotationOffsetZRound = 2;
            }
            OwnerCheck();
            OnDeserialization();
        }

        public void UpdateRotationOffsetRoundSliderX()
        {
            RotationOffsetXRoundMult = RotationOffsetRoundSliderX.value;
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdateRotationOffsetRoundSliderY()
        {
            RotationOffsetYRoundMult = RotationOffsetRoundSliderY.value;
            OwnerCheck();
            OnDeserialization();
        }
        public void UpdateRotationOffsetRoundSliderZ()
        {
            RotationOffsetZRoundMult = RotationOffsetRoundSliderZ.value;
            OwnerCheck();
            OnDeserialization();
        }

        private void OwnerCheck()
        {
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
        }







        #endregion

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
            else
            {
                SendDataBool = true;
                SendDataTimer = 0f;
            }

            RotationOffsetXText.text = RotationOffsetXValue.ToString("0.00");
            RotationOffsetYText.text = RotationOffsetYValue.ToString("0.00");
            RotationOffsetZText.text = RotationOffsetZValue.ToString("0.00");
            PositionOffsetText.text = PositionOffsetValue.ToString("0.00");
            SlarpingSpeedText.text = SlarpingSpeedValue.ToString("0.00");
            IndexText.text = (CameraPointIndex).ToString();
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

            //自动旋转相关
            RotationOffsetXRoundText.text = RoundText(RotationOffsetXRound);
            RotationOffsetYRoundText.text = RoundText(RotationOffsetYRound);
            RotationOffsetZRoundText.text = RoundText(RotationOffsetZRound);

            RotationOffsetXRoundMultText.text = RotationOffsetXRoundMult.ToString("0.00");
            RotationOffsetYRoundMultText.text = RotationOffsetYRoundMult.ToString("0.00");
            RotationOffsetZRoundMultText.text = RotationOffsetZRoundMult.ToString("0.00");


            RotationOffsetRoundSliderTextX.text = RotationOffsetRoundSliderX.value.ToString("0.00");
            RotationOffsetRoundSliderTextY.text = RotationOffsetRoundSliderY.value.ToString("0.00");
            RotationOffsetRoundSliderTextZ.text = RotationOffsetRoundSliderZ.value.ToString("0.00");


            // 发送数据
            UdonBehaviour Udon = CameraPoint[CameraPointIndex].GetComponent<UdonBehaviour>();
            Udon.SetProgramVariable(nameof(RotationOffsetXValue), RotationOffsetXValue);
            Udon.SetProgramVariable(nameof(RotationOffsetYValue), RotationOffsetYValue);
            Udon.SetProgramVariable(nameof(RotationOffsetZValue), RotationOffsetZValue);
            Udon.SetProgramVariable(nameof(PositionOffsetValue), PositionOffsetValue);
            Udon.SetProgramVariable(nameof(SlarpingSpeedValue), SlarpingSpeedValue);
            Udon.SetProgramVariable(nameof(PlaneActive), PlaneActive);
            Udon.SetProgramVariable(nameof(AutoTracking), AutoTracking);
            Udon.SetProgramVariable(nameof(RotationOffsetXRound), RotationOffsetXRound);
            Udon.SetProgramVariable(nameof(RotationOffsetYRound), RotationOffsetYRound);
            Udon.SetProgramVariable(nameof(RotationOffsetZRound), RotationOffsetZRound);
            Udon.SetProgramVariable(nameof(RotationOffsetXRoundMult), RotationOffsetXRoundMult);
            Udon.SetProgramVariable(nameof(RotationOffsetYRoundMult), RotationOffsetYRoundMult);
            Udon.SetProgramVariable(nameof(RotationOffsetZRoundMult), RotationOffsetZRoundMult);
            Udon.SendCustomEvent("AnotherUse");
        }

        private string RoundText(int Round)
        {
            if (Round == 0)
            {
                return "无";
            }
            else if (Round == 1)
            {
                return "正";
            }
            else if (Round == 2)
            {
                return "反";
            }
            else
            {
                return "未知";
            }
        }

        private void Update()
        {
            if (SendDataBool)
            {
                SendDataTimer += Time.deltaTime;
                if (SendDataTimer >= 1f)
                {
                    SendDataTimer = 0f;
                    SendData();
                    SendDataBool = false;
                }
            }
            if (RotationOffsetXRound != 0)
            {
                RotationOffsetXValue = (float)Udon1.GetProgramVariable(nameof(RotationOffsetXValue));
                RotationOffsetX.SetValueWithoutNotify(RotationOffsetXValue);
                RotationOffsetXText.text = RotationOffsetXValue.ToString("0.00");
            }
            if (RotationOffsetYRound != 0)
            {
                RotationOffsetYValue = (float)Udon1.GetProgramVariable(nameof(RotationOffsetYValue));
                RotationOffsetY.SetValueWithoutNotify(RotationOffsetYValue);
                RotationOffsetYText.text = RotationOffsetYValue.ToString("0.00");
            }
            if (RotationOffsetZRound != 0)
            {
                RotationOffsetZValue = (float)Udon1.GetProgramVariable(nameof(RotationOffsetZValue));
                RotationOffsetZ.SetValueWithoutNotify(RotationOffsetZValue);
                RotationOffsetZText.text = RotationOffsetZValue.ToString("0.00");
            }
        }

        private void SendData()
        {
            RequestSerialization();
        }

        public override void InputUse(bool value, UdonInputEventArgs args)
        {
            if (args.handType != HandType.LEFT)
            {
                return;
            }
            if (!UseRightHand)
            {
                return;
            }
            if (value)
            {
                ToggleAutoTracking();
            }
        }

        public void ToggleUseRightHand()
        {
            UseRightHand = !UseRightHand;
            if (UseRightHand)
            {
                RightHandImage.color = Color.white;
            }
            else
            {
                RightHandImage.color = Color.black;
            }
        }

    }
}
