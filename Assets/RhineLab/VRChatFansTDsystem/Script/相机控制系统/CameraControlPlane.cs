
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace RhineLab.VRChatFansTDsystem
{

    /// <summary>
    /// 相机位姿控制面板 — 纯本地 UI（BehaviourSyncMode.None）。
    /// 从 SystemControl 获取 CAM 数组，通过 cameraIndex 切换当前控制的机位。
    /// 6 轴控制：localPosition XYZ + localRotation XYZ（Euler 角 Slider）。
    ///
    /// 变量组织（Plane 脚本 = 区域 1+4）：
    ///   区域 1: 组件注册 — SystemControl / camArray[]
    ///   区域 4: UI 依赖 — 6 轴 Slider + 速度 Slider + 机位按钮 + 操作按钮
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CameraControlPlane : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — Inspector 直接引用
        // ==========================================

        /// <summary>导播主面板（用于获取 VRCTDCamera[] 并提取 CameraControlCAM[]）</summary>
        public SystemControl systemControl;

        // ---- 运行时构建（Start 中从 SystemControl 初始化） ----

        /// <summary>位姿微调执行系统数组（对应 systemControl.cameras[i].cameraControlCAM）</summary>
        private CameraControlCAM[] camArray;

        // ==========================================
        // 区域 4: UI 依赖
        // ==========================================

        [Header("机位切换")]
        /// <summary>机位 Up 按钮（cameraIndex++）</summary>
        public Button cameraUpButton;
        /// <summary>机位 Down 按钮（cameraIndex--）</summary>
        public Button cameraDownButton;
        /// <summary>机位指示文本（显示 "1/4" 等，可选）</summary>
        public Text cameraIndexText;

        [Header("位置 Slider（localPosition XYZ）")]
        /// <summary>X 轴位置 Slider</summary>
        public Slider posXSlider;
        /// <summary>Y 轴位置 Slider</summary>
        public Slider posYSlider;
        /// <summary>Z 轴位置 Slider</summary>
        public Slider posZSlider;

        [Header("旋转 Slider（localRotation Euler XYZ，度）")]
        /// <summary>X 轴旋转 Slider（0~360°）</summary>
        public Slider rotXSlider;
        /// <summary>Y 轴旋转 Slider</summary>
        public Slider rotYSlider;
        /// <summary>Z 轴旋转 Slider</summary>
        public Slider rotZSlider;

        [Header("速度控制")]
        /// <summary>移动速度 Slider</summary>
        public Slider speedSlider;

        [Header("操作按钮")]
        /// <summary>重置按钮（将目标重置为当前位姿）</summary>
        public Button resetButton;
        /// <summary>停止按钮（立即停止插值）</summary>
        public Button stopButton;

        // ---- 运行时私有状态 ----

        /// <summary>当前选中的 CAM 索引（[0, camArray.Length-1]，越界拒绝操作）</summary>
        private int cameraIndex;

        // ==========================================
        // Unity 生命周期
        // ==========================================

        void Start()
        {
            // 1. 从 SystemControl 构建 CAM 数组并注入引用
            if (systemControl != null && systemControl.cameras != null)
            {
                int count = systemControl.cameras.Length;
                camArray = new CameraControlCAM[count];
                for (int i = 0; i < count; i++)
                {
                    VRCTDCamera vcam = systemControl.cameras[i];
                    camArray[i] = vcam != null ? vcam.cameraControlCAM : null;
                    if (camArray[i] != null)
                    {
                        camArray[i].controlPlane = this;   // 注入回引
                    }
                }
            }

        }


        // ==========================================
        // UI 事件回调 — 由 Inspector OnValueChanged / OnClick 绑定
        // ==========================================

        /// <summary>机位 Up 按钮（cameraIndex++，拒绝越界）</summary>
        public void _OnCameraUpClicked()
        {
            if (camArray == null || camArray.Length == 0) return;
            if (cameraIndex >= camArray.Length - 1) return; // 已在末尾，拒绝操作
            cameraIndex++;
            _ApplyCameraIndex();
        }

        /// <summary>机位 Down 按钮（cameraIndex--，拒绝越界）</summary>
        public void _OnCameraDownClicked()
        {
            if (cameraIndex <= 0) return; // 已在开头，拒绝操作
            cameraIndex--;
            _ApplyCameraIndex();
        }

        /// <summary>
        /// 任意位姿 Slider 变更时调用。
        /// 将所有 6 轴 Slider 的当前值组合为位姿，下发到当前 CAM。
        /// 在 Inspector 中将各 Slider 的 OnValueChanged 绑定到此方法。
        /// </summary>
        public void _OnAnyPoseSliderChanged()
        {
            CameraControlCAM current = _GetCurrentCAM();
            if (current == null) return;

            Vector3 pos = _ReadPositionSliders();
            Vector3 euler = _ReadRotationSliders();

            current.SetTargetPose(pos, Quaternion.Euler(euler));
        }

        /// <summary>
        /// 速度 Slider 变更回调。
        /// </summary>
        public void _OnSpeedSliderChanged()
        {
            CameraControlCAM current = _GetCurrentCAM();
            if (speedSlider == null || current == null) return;
            current.SetSpeed(speedSlider.value);
        }

        /// <summary>
        /// 重置按钮回调 — 将目标重置为当前实际位姿。
        /// </summary>
        public void _OnResetClicked()
        {
            CameraControlCAM current = _GetCurrentCAM();
            if (current != null)
            {
                current.ResetToCurrent();
            }
        }

        /// <summary>
        /// 停止按钮回调 — 立即停止插值。
        /// </summary>
        public void _OnStopClicked()
        {
            CameraControlCAM current = _GetCurrentCAM();
            if (current != null)
            {
                current.StopMovement();
            }
        }

        // ==========================================
        // 公共方法 — 由 CameraControlCAM 回调
        // ==========================================

        /// <summary>
        /// 由 CameraControlCAM 调用，同步更新所有 Slider 显示值。
        /// 使用 SetValueWithoutNotify 避免触发 OnValueChanged 循环。
        /// 仅当 CAM 的 isPlaneListening=true 时才会被 CAM 调用，无需额外身份过滤。
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <param name="eulerAngles">目标旋转 Euler 角</param>
        public void _RefreshAllSliders(Vector3 position, Vector3 eulerAngles)
        {
            if (posXSlider != null) posXSlider.SetValueWithoutNotify(position.x);
            if (posYSlider != null) posYSlider.SetValueWithoutNotify(position.y);
            if (posZSlider != null) posZSlider.SetValueWithoutNotify(position.z);

            if (rotXSlider != null) rotXSlider.SetValueWithoutNotify(eulerAngles.x);
            if (rotYSlider != null) rotYSlider.SetValueWithoutNotify(eulerAngles.y);
            if (rotZSlider != null) rotZSlider.SetValueWithoutNotify(eulerAngles.z);
        }

        // ==========================================
        // 内部方法
        // ==========================================

        /// <summary>获取当前 cameraIndex 对应的 CAM（含空值防护）</summary>
        private CameraControlCAM _GetCurrentCAM()
        {
            if (camArray == null || camArray.Length == 0) return null;
            if (cameraIndex < 0 || cameraIndex >= camArray.Length) return null;
            return camArray[cameraIndex];
        }

        /// <summary>
        /// 应用 cameraIndex：边界检查 → 设置 isPlaneListening 标志 → 刷新 UI。
        /// 仅当前 CAM 的 isPlaneListening=true，其余为 false。
        /// </summary>
        private void _ApplyCameraIndex()
        {
            if (camArray == null || camArray.Length == 0) return;

            // 边界检查（拒绝越界）
            if (cameraIndex < 0) cameraIndex = 0;
            if (cameraIndex >= camArray.Length) cameraIndex = camArray.Length - 1;

            // 设置监听标志：当前 CAM=true，其余=false
            for (int i = 0; i < camArray.Length; i++)
            {
                if (camArray[i] != null)
                {
                    camArray[i].isPlaneListening = (i == cameraIndex);
                }
            }

            // 从当前 CAM 读取状态并刷新所有 Slider
            CameraControlCAM current = camArray[cameraIndex];
            if (current != null)
            {
                _RefreshAllSliders(current.TargetPosition, current.TargetRotationEuler);
                if (speedSlider != null)
                {
                    speedSlider.SetValueWithoutNotify(current.speed);
                }
            }

            // 更新机位文本
            if (cameraIndexText != null)
            {
                cameraIndexText.text = string.Format("{0}/{1}", cameraIndex + 1, camArray.Length);
            }
        }

        /// <summary>从位置 Slider 读取当前值组成 Vector3</summary>
        private Vector3 _ReadPositionSliders()
        {
            return new Vector3(
                posXSlider != null ? posXSlider.value : 0f,
                posYSlider != null ? posYSlider.value : 0f,
                posZSlider != null ? posZSlider.value : 0f
            );
        }

        /// <summary>从旋转 Slider 读取当前值组成 Euler Vector3</summary>
        private Vector3 _ReadRotationSliders()
        {
            return new Vector3(
                rotXSlider != null ? rotXSlider.value : 0f,
                rotYSlider != null ? rotYSlider.value : 0f,
                rotZSlider != null ? rotZSlider.value : 0f
            );
        }
    }
}


