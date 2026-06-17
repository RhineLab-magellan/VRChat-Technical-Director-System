
using UdonSharp;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using static VRC.SDKBase.VRCPlayerApi;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class UserControlPanle : UdonSharpBehaviour
{
    public Transform LocalPanle;


    public Transform[] ControlObject;

    public Toggle[] ControlToggle;
    public Transform TPPlane;

    private UdonBehaviour UsingObject;
    private Transform Local;

    [Header("Control")]
    public Image[] ControlImage;

    public Color[] Colors;

    public bool IsVR;

    private short Mode = 0;

    public void Start()
    {
        for (int i = 0; i < ControlToggle.Length; i++)
        {
            ControlToggle[i].SetIsOnWithoutNotify(false);
            ControlToggle[i].transform.GetChild(0).GetComponent<Text>().text = ControlObject[i].name;
        }
        IsVR = Networking.LocalPlayer.IsUserInVR();

    }
    public void ToggleControl()
    {
        for (int i = 0; i < ControlToggle.Length; i++)
        {
            if (ControlToggle[i].isOn)
            {
                UsingUpdate(i);
                ControlToggle[i].SetIsOnWithoutNotify(false);
                break;
            }
        }
    }

    private void UsingUpdate(int index)
    {
        if (UsingObject != null)
        { UsingObject.SendCustomEvent("ReturnTo"); }
        ControlObject[index].SetPositionAndRotation(TPPlane.position, TPPlane.rotation);
        UsingObject = ControlObject[index].GetComponent<UdonBehaviour>();
        Local = ControlObject[index];
    }

    public void UseWorldPosition()
    {
        if (UsingObject != null)
        {
            UsingObject.SendCustomEvent("UseWorldPosition");
        }
    }
    public void UsePlayerPosition()
    {
        if (UsingObject != null)
        {
            UsingObject.SendCustomEvent("UsePlayerPosition");
        }
    }

    public void SetPlaneX()
    {
        if (Mode == 1)
        {
            Mode = 0;
            UpdateImage();
            return;
        }
        Mode = 1;
        UpdateImage();
    }

    public void SetPlaneY()
    {
        if (Mode == 2)
        {
            Mode = 0;
            UpdateImage();
            return;
        }
        Mode = 2;
        UpdateImage();
    }

    public void SetPlaneDefect()
    {
        Mode = 0;
        UpdateImage();
        if (UsingObject != null)
        {
            UsingObject.SendCustomEvent("SetPlaneDefect");
        }
    }

    private void UpdateImage()
    {
        if (Mode == 0)
        {
            ControlImage[0].color = Colors[1];
            ControlImage[1].color = Colors[0];
            ControlImage[2].color = Colors[0];
        }
        else if (Mode == 1)
        {
            ControlImage[0].color = Colors[0];
            ControlImage[1].color = Colors[1];
            ControlImage[2].color = Colors[0];
        }
        else
        {
            ControlImage[0].color = Colors[0];
            ControlImage[1].color = Colors[0];
            ControlImage[2].color = Colors[1];
        }
    }

    public void Update()
    {
        if (!IsVR)
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                TPLocal();
            }
            return;
        }


    }
    private void TPLocal()
    {
        VRCPlayerApi tracking = Networking.LocalPlayer;
        Vector3 headPos = tracking.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        LocalPanle.position = tracking.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
        LocalPanle.LookAt(LocalPanle.position * 2 - headPos);
    }
    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if (!IsVR) return;

        if (args.handType == HandType.LEFT)
        {

        }
    }

    public override void InputLookVertical(float value, UdonInputEventArgs args)
    {
        if (!IsVR)
        {
            return;
        }
        if (Mode == 0)
        {
            return;
        }
        if (Mode == 1)
        {
            Local.localScale = new Vector3(value, 0, 0) + Local.localScale;
        }
        else if (Mode == 2)
        {
            Local.localScale = new Vector3(0, value, 0) + Local.localScale;
        }
        Debug.Log(value);
    }

}
