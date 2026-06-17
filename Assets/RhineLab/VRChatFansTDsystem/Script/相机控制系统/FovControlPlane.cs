
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace RhineLab.VRChatFansTDsystem
{

    /// <summary>
    /// FOV 控制面板 — 纯本地 UI（BehaviourSyncMode.None）。
    /// 从 SystemControl 获取 CAM 数组，通过 cameraIndex 切换当前控制的机位。
    /// 接收 CAM 的状态更新以同步 Slider/Toggle 显示。
    ///
    /// 变量组织（Plane 脚本 = 区域 1+4）：
    ///   区域 1: 组件注册 — SystemControl / fovCAMs[]
    ///   区域 4: UI 依赖 — Slider / Toggle / 机位按钮
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FovControlPlane : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — Inspector 直接引用
        // ==========================================

        /// <summary>导播主面板（用于获取 VRCTDCamera[] 并提取 FovControlCAM[]）</summary>
        public SystemControl systemControl;

        // ---- 运行时构建（Start 中从 SystemControl 初始化） ----

        /// <summary>FOV 执行系统数组（对应 systemControl.cameras[i].fovControlCAM）</summary>
        private FovControlCAM[] fovCAMs;

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

        [Header("FOV 控件")]
        /// <summary>FOV 值 Slider（范围需与 FovControlCAM.minFOV/maxFOV 一致）</summary>
        public Slider fovSlider;

        /// <summary>自动/手动模式 Toggle</summary>
        public Toggle autoModeToggle;

        /// <summary>当前 FOV 值文本显示（可选）</summary>
        public Text fovValueText;

        // ---- 运行时私有状态 ----

        /// <summary>当前选中的 CAM 索引（[0, fovCAMs.Length-1]，越界拒绝操作）</summary>
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
                fovCAMs = new FovControlCAM[count];
                for (int i = 0; i < count; i++)
                {
                    VRCTDCamera vcam = systemControl.cameras[i];
                    fovCAMs[i] = vcam != null ? vcam.fovControlCAM : null;
                    if (fovCAMs[i] != null)
                    {
                        fovCAMs[i].fovPlane = this;   // 注入回引
                    }
                }
            }

            // 2. 应用初始 cameraIndex（默认 0）→ 刷新 UI + 设置 isPlaneListening 标志
            _ApplyCameraIndex();
        }

        // ==========================================
        // UI 事件回调 — 由 Inspector OnValueChanged / OnClick 绑定
        // ==========================================

        /// <summary>机位 Up 按钮（cameraIndex++，拒绝越界）</summary>
        public void _OnCameraUpClicked()
        {
            if (fovCAMs == null || fovCAMs.Length == 0) return;
            if (cameraIndex >= fovCAMs.Length - 1) return;
            cameraIndex++;
            _ApplyCameraIndex();
        }

        /// <summary>机位 Down 按钮（cameraIndex--，拒绝越界）</summary>
        public void _OnCameraDownClicked()
        {
            if (cameraIndex <= 0) return;
            cameraIndex--;
            _ApplyCameraIndex();
        }

        /// <summary>
        /// FOV Slider 值变更回调。
        /// 在 Inspector 中将 FOV Slider 的 OnValueChanged 绑定到此方法。
        /// </summary>
        public void _OnFovSliderChanged()
        {
            if (fovSlider == null) return;
            FovControlCAM current = _GetCurrentCAM();
            if (current == null) return;

            float value = fovSlider.value;
            current.SetTargetFOV(value);
            _UpdateFovText(value);
        }

        /// <summary>
        /// 自动模式 Toggle 变更回调。
        /// 在 Inspector 中将 Toggle 的 OnValueChanged 绑定到此方法。
        /// </summary>
        public void _OnAutoModeToggleChanged()
        {
            if (autoModeToggle == null) return;
            FovControlCAM current = _GetCurrentCAM();
            if (current == null) return;

            bool isAuto = autoModeToggle.isOn;
            current.SetMode(isAuto);

            // 自动模式下 Slider 仅用于显示当前 FOV，不可手动拖动
            if (fovSlider != null)
            {
                fovSlider.interactable = !isAuto;
            }
        }

        // ==========================================
        // 公共方法 — 由 FovControlCAM 回调
        // ==========================================

        /// <summary>
        /// 由 FovControlCAM 调用，同步更新 Slider/Toggle 显示值。
        /// 不会触发 Slider 的 OnValueChanged，避免循环调用。
        /// 仅当 CAM 的 isPlaneListening=true 时才会被 CAM 调用，无需额外身份过滤。
        /// </summary>
        /// <param name="value">新的 FOV 值</param>
        public void _RefreshSlider(float value)
        {
            if (fovSlider != null)
            {
                fovSlider.SetValueWithoutNotify(value);
            }
            _UpdateFovText(value);

            // 同步 Toggle 状态
            FovControlCAM current = _GetCurrentCAM();
            if (autoModeToggle != null && current != null)
            {
                autoModeToggle.SetIsOnWithoutNotify(current.autoMode);
                if (fovSlider != null)
                {
                    fovSlider.interactable = !current.autoMode;
                }
            }
        }

        // ==========================================
        // 内部方法
        // ==========================================

        /// <summary>获取当前 cameraIndex 对应的 CAM（含空值防护）</summary>
        private FovControlCAM _GetCurrentCAM()
        {
            if (fovCAMs == null || fovCAMs.Length == 0) return null;
            if (cameraIndex < 0 || cameraIndex >= fovCAMs.Length) return null;
            return fovCAMs[cameraIndex];
        }

        /// <summary>
        /// 应用 cameraIndex：边界检查 → 设置 isPlaneListening 标志 → 刷新 UI。
        /// 仅当前 CAM 的 isPlaneListening=true，其余为 false。
        /// </summary>
        private void _ApplyCameraIndex()
        {
            if (fovCAMs == null || fovCAMs.Length == 0) return;

            // 边界检查（拒绝越界）
            if (cameraIndex < 0) cameraIndex = 0;
            if (cameraIndex >= fovCAMs.Length) cameraIndex = fovCAMs.Length - 1;

            // 设置监听标志：当前 CAM=true，其余=false
            for (int i = 0; i < fovCAMs.Length; i++)
            {
                if (fovCAMs[i] != null)
                {
                    fovCAMs[i].isPlaneListening = (i == cameraIndex);
                }
            }

            // 从当前 CAM 读取状态并刷新 UI
            FovControlCAM current = fovCAMs[cameraIndex];
            if (current != null)
            {
                if (fovSlider != null)
                {
                    fovSlider.SetValueWithoutNotify(current.CurrentFOV);
                }
                if (autoModeToggle != null)
                {
                    autoModeToggle.SetIsOnWithoutNotify(current.autoMode);
                    if (fovSlider != null)
                    {
                        fovSlider.interactable = !current.autoMode;
                    }
                }
                _UpdateFovText(current.CurrentFOV);
            }

            // 更新机位文本
            if (cameraIndexText != null)
            {
                cameraIndexText.text = string.Format("{0}/{1}", cameraIndex + 1, fovCAMs.Length);
            }
        }

        /// <summary>
        /// 更新 FOV 数值文本显示（格式: "60.0°"）。
        /// </summary>
        private void _UpdateFovText(float value)
        {
            if (fovValueText != null)
            {
                fovValueText.text = string.Format("{0:F1}°", value);
            }
        }
    }

}
