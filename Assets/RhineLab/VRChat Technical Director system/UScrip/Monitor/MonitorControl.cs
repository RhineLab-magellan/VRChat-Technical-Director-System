
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using VRC.Udon.Common;

public class MonitorControl : UdonSharpBehaviour
{
    public RenderTexture[] RenderTEX;
    private short TEXIndex = 0;

    private float ReFloat = 0;

    private Vector3 LocaPosition;
    private Quaternion LocaRotation;

    public Text TEXIndexText;

    public MeshRenderer MeshRender;
    public Transform Monitor;

    private VRC_Pickup pickup;

    private Vector3 MonitorSc;

    private short IDs;

    public Text IDsText;

    public string[] MonitorNames = { "None", "localScale X", "localScale Y", "Switch Camera", "None" };

    private bool IsUsing = false;

    void Start()
    {
        TEXchaneger();
        pickup = Monitor.GetComponent<VRC_Pickup>();
        LocaPosition = this.transform.localPosition;
        LocaRotation = this.transform.localRotation;
        IDsText.text = IDs.ToString() + ":" + MonitorNames[IDs];
    }

    public override void OnPickupUseDown()
    {
        IDs++;
        if (IDs == 0)
        {
            IsUsing = false;
        }
        else
        {
            IsUsing = true;
        }
        if (IDs > 4)
        {
            IDs = 0;
        }
        if (IDs < MonitorNames.Length)
        {
            pickup.InteractionText = MonitorNames[IDs];
            IDsText.text = IDs.ToString() + ":" + MonitorNames[IDs];
        }

    }

    public override void InputLookVertical(float value, UdonInputEventArgs args)
    {
        if (!IsUsing)
        {
            return;
        }
        MonitorSc = Monitor.localScale;
        if (IDs == 1)
        {
            MonitorSc.x += value;
        }
        else if (IDs == 2)
        {
            MonitorSc.y += value;
        }
        else if (IDs == 3)
        {
            ReFloat += value;
            if (ReFloat > 5f)
            {
                TEXIndex++;
                if (TEXIndex > RenderTEX.Length - 1)
                {
                    TEXIndex = 0;
                }
                TEXchaneger();
            }
            if (ReFloat < -5f)
            {
                TEXIndex--;
                if (TEXIndex < 0)
                {
                    TEXIndex = (short)(RenderTEX.Length - 1);
                }
                TEXchaneger();
            }

        }
        Monitor.localScale = MonitorSc;
    }

    private void TEXchaneger()
    {
        MeshRender.material.SetTexture("_MainTex", RenderTEX[TEXIndex]);
        TEXIndexText.text = TEXIndex.ToString();
    }

    public override void OnDrop()
    {
        this.transform.localPosition = LocaPosition;
        this.transform.localRotation = LocaRotation;
        IsUsing = false;
        ReFloat = 0;
    }
    public override void OnPickup()
    {
        if (IDs == 0)
        {
            IsUsing = false;
        }
        else
        {
            IsUsing = true;
        }
        if (IDs > 4)
        {
            IDs = 0;
        }
        if (IDs < MonitorNames.Length)
        {
            pickup.InteractionText = MonitorNames[IDs];
            IDsText.text = IDs.ToString() + ":" + MonitorNames[IDs];
        }
        ReFloat = 0;
    }




}
