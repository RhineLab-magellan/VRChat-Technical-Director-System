
using UdonSharp;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class HandGetTracking : UdonSharpBehaviour
{
    public Transform CameraPoint;
    // Modes:
    // 0 - 无锁定：CameraPoint 完全跟踪主物体的位置和旋转
    // 1 - 俯仰角锁定：CameraPoint 跟踪位置、偏航角和滚动角，俯仰角锁定为初始值
    // 2 - 俯仰+滚动锁定：CameraPoint 仅跟踪位置和偏航角，俯仰角和滚动角锁定为初始值
    // 3 - 完全锁定：CameraPoint 仅跟踪位置，旋转锁定为初始值

    public Transform TrakingTarget;
    public int Mode;

    private int LocalMode = -1;

    private float LookVertical;

    private bool OnPickUp;

    private bool IsVR;

    private Quaternion initialCamRotation;
    private Vector3 initialCamEuler;

    // 公用计算字段（避免每帧分配临时变量）
    private Quaternion initialPitchRot;
    private Quaternion initialRollRot;
    private Quaternion initialPitchRollRot;
    private Quaternion _baseRot;
    private float _roll;

    public Material Material;
    public Color[] Colors;
    public Text textMode;
    public string[] ModeString;

    public Transform TrackingPlayer;
    private bool IsTrackingPlayer;
    public Toggle toggleTrackingPlayer;

    public void Start()
    {
        IsVR = Networking.LocalPlayer.IsUserInVR();
        if (CameraPoint != null)
        {
            CaptureInitialRotation();
        }
        Material = this.GetComponent<Renderer>().material;
        OnDeserialization();
    }

    public void ToggleTrackingPlayer()
    {
        IsTrackingPlayer = toggleTrackingPlayer.isOn;
        SendCustomEventDelayedFrames(nameof(ReturnTracking), 1);

    }

    public void ReturnTracking()
    {
        if (!IsTrackingPlayer)
        {
            TrakingTarget.rotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// 将任意旋转分解为 Yaw(世界Y轴) * Pitch(局部X轴) * Roll(局部Z轴) 三个四元数
    /// 分解结果满足 rotation ≈ yaw * pitch * roll
    /// </summary>
    private void DecomposeYawPitchRoll(Quaternion rotation, out Quaternion yaw, out Quaternion pitch, out Quaternion roll)
    {
        Vector3 forward = rotation * Vector3.forward;
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = Vector3.forward;
        else
            flatForward.Normalize();
        yaw = Quaternion.LookRotation(flatForward, Vector3.up);

        Quaternion pitchRoll = Quaternion.Inverse(yaw) * rotation;
        Vector3 prForward = pitchRoll * Vector3.forward;
        float pitchDeg = -Mathf.Asin(Mathf.Clamp(prForward.y, -1f, 1f)) * Mathf.Rad2Deg;
        pitch = Quaternion.AngleAxis(pitchDeg, Vector3.right);

        roll = Quaternion.Inverse(pitch) * pitchRoll;
    }

    /// <summary>
    /// 从 CameraPoint 当前旋转捕获初始旋转参数
    /// </summary>
    Quaternion giveup;
    Quaternion giveup2;
    private void CaptureInitialRotation()
    {
        initialCamRotation = CameraPoint.rotation;
        initialCamEuler = CameraPoint.eulerAngles;

        DecomposeYawPitchRoll(initialCamRotation, out giveup, out initialPitchRot, out initialRollRot);
        initialPitchRollRot = initialPitchRot * initialRollRot;
    }

    private Quaternion _lastYawRot = Quaternion.identity;

    private void Update()
    {
        if (CameraPoint == null || TrakingTarget == null) return;
        if (IsTrackingPlayer)
        {
            TrakingTarget.LookAt(TrackingPlayer);
        }
        // 位置始终跟踪 TrakingTarget
        CameraPoint.position = TrakingTarget.position;

        switch (Mode)
        {
            case 0: // 无锁定 - 完全跟踪
                CameraPoint.rotation = TrakingTarget.rotation;
                break;

            case 2: // 俯仰角锁定：跟踪偏航+滚动，俯仰锁定为初始值
                DecomposeYawPitchRoll(TrakingTarget.rotation, out Quaternion tYaw, out giveup, out Quaternion tRoll);
                CameraPoint.rotation = tYaw * initialPitchRot * tRoll;
                break;

            case 1: // 俯仰+滚动锁定：仅跟踪偏航，俯仰和滚动锁定为初始值
                DecomposeYawPitchRoll(TrakingTarget.rotation, out Quaternion tYaw2, out giveup, out giveup2);
                CameraPoint.rotation = tYaw2 * initialPitchRollRot;
                break;

            case 3: // 完全锁定 - 不跟踪旋转
                CameraPoint.rotation = initialCamRotation;
                break;
        }
    }

    public override void OnPickupUseDown()
    {
        if (!IsVR)
        {
            Mode++;
            if (Mode > 3) Mode = 0;
            ModeChange();
        }
        else
        {
            if (Mode == 0)
            {
                IsTrackingPlayer = !IsTrackingPlayer;
                toggleTrackingPlayer.SetIsOnWithoutNotify(IsTrackingPlayer);
                SendCustomEventDelayedFrames(nameof(ReturnTracking), 1);
            }
            else
            {
                if (Mode < 2)
                {
                    Mode = 2;
                }
                CaptureInitialRotation();
                OnDeserialization();
            }
        }
    }

    public override void InputLookVertical(float value, UdonInputEventArgs args)
    {
        if (!IsVR) return;
        if (value < 0.3f && value > -0.3f) return;

        if (!OnPickUp) return;
        value *= Time.deltaTime;
        LookVertical -= value;
        if (LookVertical > 1f)
        {
            LookVertical = 0f;
            Mode++;
            if (Mode > 3) Mode = 0;
            ModeChange();
        }
        if (LookVertical < -1f)
        {
            LookVertical = 0f;
            Mode--;
            if (Mode < 0) Mode = 3;
            ModeChange();
        }

    }

    private void ModeChange()
    {
        CaptureInitialRotation();
        OnDeserialization();
    }

    public override void OnDeserialization()
    {
        if (LocalMode == Mode) return;

        LocalMode = Mode;
        if (Mode == 0)
        {
            Material.color = Colors[0];
            textMode.text = ModeString[0];
        }
        else if (Mode == 2)
        {
            Material.color = Colors[1];
            textMode.text = ModeString[1];
        }
        else if (Mode == 1)
        {
            Material.color = Colors[2];
            textMode.text = ModeString[2];
        }
        else if (Mode == 3)
        {
            Material.color = Colors[3];
            textMode.text = ModeString[3];
        }

    }


    public override void OnPickup()
    {
        OnPickUp = true;
    }

    public override void OnDrop()
    {
        OnPickUp = false;
    }
}
