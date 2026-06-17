
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace RhineLab.VRChatFansTDsystem
{

    /// <summary>
    /// 跟踪控制面板 + 玩家名快速选择 — 纯本地 UI（BehaviourSyncMode.None）。
    /// 从 SystemControl 获取 CAM 数组，通过 cameraIndex 切换当前控制的机位。
    /// 集成原 PlayerTrackingControl + QuickNameChoose。
    ///
    /// 功能：
    ///   - 跟踪类型选择（Head/LeftHand/RightHand/Origin）
    ///   - 位置偏移 XYZ Slider
    ///   - 相对/绝对模式 Toggle、旋转反转 Toggle、各轴旋转锁定 Toggle
    ///   - 跟踪指示器显示 Toggle
    ///   - TempWrite 模式：预览编辑 → 应用/取消
    ///   - 玩家名快速选择：动态刷新在线玩家列表 → 一键选为跟踪目标
    ///
    /// 变量组织（Plane 脚本 = 区域 1+4）：
    ///   区域 1: 组件注册 — SystemControl / trackingCAMs[]
    ///   区域 4: UI 依赖 — Slider / Toggle / Dropdown / Button / Text
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TrackingPlayerPlane : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — Inspector 直接引用
        // ==========================================

        /// <summary>导播主面板（用于获取 VRCTDCamera[] 并提取 TrackingPlayerCAM[]）</summary>
        public SystemControl systemControl;

        // ---- 运行时构建（Start 中从 SystemControl 初始化） ----

        /// <summary>玩家跟踪执行系统数组（对应 systemControl.cameras[i].trackingPlayerCAM）</summary>
        private TrackingPlayerCAM[] trackingCAMs;

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

        [Header("跟踪类型")]
        /// <summary>跟踪类型 Dropdown（0=Head, 1=LeftHand, 2=RightHand, 3=Origin）</summary>
        public Dropdown trackingTypeDropdown;

        [Header("位置偏移 Slider（XYZ）")]
        public Slider offsetXSlider;
        public Slider offsetYSlider;
        public Slider offsetZSlider;

        [Header("模式 Toggle")]
        /// <summary>相对/绝对模式 Toggle</summary>
        public Toggle relativeModeToggle;
        /// <summary>旋转反转 Toggle</summary>
        public Toggle rotationOffsetToggle;

        [Header("旋转锁定 Toggle（XYZ 轴）")]
        public Toggle lockXToggle;
        public Toggle lockYToggle;
        public Toggle lockZToggle;

        [Header("指示器")]
        /// <summary>跟踪指示器显示 Toggle</summary>
        public Toggle indicatorToggle;

        [Header("TempWrite 模式")]
        /// <summary>TempWrite 开关 Toggle</summary>
        public Toggle tempWriteToggle;
        /// <summary>应用预览按钮</summary>
        public Button applyPreviewButton;
        /// <summary>取消预览按钮</summary>
        public Button cancelPreviewButton;

        [Header("玩家名快速选择")]
        /// <summary>刷新玩家列表按钮</summary>
        public Button refreshPlayersButton;
        /// <summary>玩家名按钮容器（按钮放在此 Transform 下）</summary>
        public Transform playerButtonContainer;
        /// <summary>玩家名按钮预制体（需带 Text 子节点和 Button 组件）</summary>
        public GameObject playerButtonPrefab;
        /// <summary>最大同时显示玩家按钮数</summary>
        public int maxPlayerButtons = 80;

        // ---- 运行时私有状态 ----

        /// <summary>当前选中的 CAM 索引（[0, trackingCAMs.Length-1]，越界拒绝操作）</summary>
        private int cameraIndex;

        /// <summary>在线玩家引用缓存</summary>
        private VRCPlayerApi[] _onlinePlayers;

        /// <summary>玩家名按钮缓存</summary>
        private GameObject[] _playerButtons;

        /// <summary>是否正在 TempWrite 模式</summary>
        private bool _tempWriteMode;

        // ==========================================
        // Unity 生命周期
        // ==========================================

        void Start()
        {
            // 初始化玩家按钮数组
            _playerButtons = new GameObject[0];

            // 1. 从 SystemControl 构建 CAM 数组并注入引用
            if (systemControl != null && systemControl.cameras != null)
            {
                int count = systemControl.cameras.Length;
                trackingCAMs = new TrackingPlayerCAM[count];
                for (int i = 0; i < count; i++)
                {
                    VRCTDCamera vcam = systemControl.cameras[i];
                    trackingCAMs[i] = vcam != null ? vcam.trackingPlayerCAM : null;
                    if (trackingCAMs[i] != null)
                    {
                        trackingCAMs[i].trackingPlane = this;   // 注入回引
                    }
                }
            }

            // 2. 应用初始 cameraIndex（默认 0）→ 刷新 UI + 设置 isPlaneListening 标志
            _ApplyCameraIndex();
        }

        // ==========================================
        // UI 事件回调 — Inspector OnValueChanged / OnClick 绑定
        // ==========================================

        /// <summary>机位 Up 按钮（cameraIndex++，拒绝越界）</summary>
        public void _OnCameraUpClicked()
        {
            if (trackingCAMs == null || trackingCAMs.Length == 0) return;
            if (cameraIndex >= trackingCAMs.Length - 1) return;
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

        /// <summary>跟踪类型 Dropdown 变更</summary>
        public void _OnTrackingTypeChanged()
        {
            if (trackingTypeDropdown == null) return;
            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current == null) return;
            int id = trackingTypeDropdown.value;
            _WriteTrackingID(id, current);
        }

        /// <summary>任意偏移 Slider 变更</summary>
        public void _OnOffsetSliderChanged()
        {
            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current == null) return;
            Vector3 offset = new Vector3(
                offsetXSlider != null ? offsetXSlider.value : 0f,
                offsetYSlider != null ? offsetYSlider.value : 0f,
                offsetZSlider != null ? offsetZSlider.value : 0f
            );
            _WritePositionOffset(offset, current);
        }

        /// <summary>相对模式 Toggle 变更</summary>
        public void _OnRelativeModeChanged()
        {
            if (relativeModeToggle == null) return;
            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current == null) return;
            _WriteUseRelativePosition(relativeModeToggle.isOn, current);
        }

        /// <summary>旋转反转 Toggle 变更</summary>
        public void _OnRotationOffsetChanged()
        {
            if (rotationOffsetToggle == null) return;
            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current == null) return;
            _WriteRotationOffset(rotationOffsetToggle.isOn, current);
        }

        /// <summary>任意旋转锁定 Toggle 变更</summary>
        public void _OnRotationLockChanged()
        {
            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current == null) return;
            if (lockXToggle != null) _WriteRotationLock(0, lockXToggle.isOn, current);
            if (lockYToggle != null) _WriteRotationLock(1, lockYToggle.isOn, current);
            if (lockZToggle != null) _WriteRotationLock(2, lockZToggle.isOn, current);
        }

        /// <summary>指示器 Toggle 变更</summary>
        public void _OnIndicatorToggleChanged()
        {
            if (indicatorToggle == null) return;
            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current == null) return;
            _WriteAppearance(indicatorToggle.isOn, current);
        }

        /// <summary>TempWrite Toggle 变更</summary>
        public void _OnTempWriteToggleChanged()
        {
            if (tempWriteToggle == null) return;
            _tempWriteMode = tempWriteToggle.isOn;

            if (applyPreviewButton != null) applyPreviewButton.interactable = _tempWriteMode;
            if (cancelPreviewButton != null) cancelPreviewButton.interactable = _tempWriteMode;

            if (!_tempWriteMode)
            {
                TrackingPlayerCAM current = _GetCurrentCAM();
                if (current != null) current._CancelPreview();
            }
        }

        /// <summary>应用预览按钮</summary>
        public void _OnApplyPreviewClicked()
        {
            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current != null) current._ApplyPreview();
            if (tempWriteToggle != null) tempWriteToggle.isOn = false;
            _tempWriteMode = false;
        }

        /// <summary>取消预览按钮</summary>
        public void _OnCancelPreviewClicked()
        {
            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current != null) current._CancelPreview();
            if (tempWriteToggle != null) tempWriteToggle.isOn = false;
            _tempWriteMode = false;
        }

        /// <summary>刷新玩家列表按钮</summary>
        public void _OnRefreshPlayersClicked()
        {
            Display();
        }

        // ==========================================
        // 公共方法 — 由 TrackingPlayerCAM 回调
        // ==========================================

        /// <summary>
        /// 由 TrackingPlayerCAM 调用，同步更新所有 UI 控件显示。
        /// 使用 SetXxxWithoutNotify 避免触发 OnValueChanged 循环。
        /// 仅当 CAM 的 isPlaneListening=true 时才会被 CAM 调用，无需额外身份过滤。
        /// </summary>
        public void _RefreshUI()
        {
            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current == null) return;

            // 跟踪类型 Dropdown
            if (trackingTypeDropdown != null)
                trackingTypeDropdown.SetValueWithoutNotify(current.TrackingID);

            // 偏移 Slider
            Vector3 offset = current.positionOffset;
            if (offsetXSlider != null) offsetXSlider.SetValueWithoutNotify(offset.x);
            if (offsetYSlider != null) offsetYSlider.SetValueWithoutNotify(offset.y);
            if (offsetZSlider != null) offsetZSlider.SetValueWithoutNotify(offset.z);

            // 模式 Toggle
            if (relativeModeToggle != null) relativeModeToggle.SetIsOnWithoutNotify(current.useRelativePosition);
            if (rotationOffsetToggle != null) rotationOffsetToggle.SetIsOnWithoutNotify(current.rotationOffset);

            // 旋转锁定 Toggle
            if (lockXToggle != null) lockXToggle.SetIsOnWithoutNotify(current.rotationLock[0]);
            if (lockYToggle != null) lockYToggle.SetIsOnWithoutNotify(current.rotationLock[1]);
            if (lockZToggle != null) lockZToggle.SetIsOnWithoutNotify(current.rotationLock[2]);

            // 指示器 Toggle
            if (indicatorToggle != null) indicatorToggle.SetIsOnWithoutNotify(current.appearance);
        }

        // ==========================================
        // 玩家名快速选择
        // ==========================================

        /// <summary>
        /// 刷新在线玩家列表并生成选择按钮。
        /// 由刷新按钮 OnClick 或 TrackingPlayerPlane 初始化时调用。
        /// </summary>
        public void Display()
        {
            VRCPlayerApi[] allPlayers = new VRCPlayerApi[maxPlayerButtons];
            VRCPlayerApi.GetPlayers(allPlayers);

            int playerCount = 0;
            for (int i = 0; i < allPlayers.Length; i++)
            {
                if (allPlayers[i] != null && allPlayers[i].IsValid())
                {
                    playerCount++;
                }
                else
                {
                    break;
                }
            }

            _onlinePlayers = new VRCPlayerApi[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                _onlinePlayers[i] = allPlayers[i];
            }

            _ClearPlayerButtons();

            if (playerButtonPrefab != null && playerButtonContainer != null)
            {
                _playerButtons = new GameObject[playerCount];
                for (int i = 0; i < playerCount; i++)
                {
                    GameObject btn = Object.Instantiate(playerButtonPrefab);
                    btn.transform.SetParent(playerButtonContainer, false);
                    btn.name = string.Format("PlayerBtn_{0}", i);

                    Text btnText = btn.GetComponentInChildren<Text>();
                    if (btnText != null)
                    {
                        btnText.text = string.Format("#{0} {1}", i, _onlinePlayers[i].displayName);
                    }

                    UdonBehaviour btnUdon = (UdonBehaviour)btn.GetComponent(typeof(UdonBehaviour));
                    if (btnUdon != null)
                    {
                        btnUdon.SetProgramVariable("targetPlane", this);
                        btnUdon.SetProgramVariable("playerIndex", i);
                    }

                    _playerButtons[i] = btn;
                }
            }
        }

        /// <summary>
        /// 玩家按钮点击回调 — 选择该玩家为跟踪目标。
        /// 由玩家按钮的 UdonBehaviour 通过 SendCustomEvent 调用。
        /// </summary>
        /// <param name="index">玩家在 _onlinePlayers 中的索引</param>
        public void SetToken(int index)
        {
            if (_onlinePlayers == null || index < 0 || index >= _onlinePlayers.Length) return;

            TrackingPlayerCAM current = _GetCurrentCAM();
            if (current == null) return;

            VRCPlayerApi target = _onlinePlayers[index];
            if (target == null || !target.IsValid()) return;

            current.SetTrackingTarget(target.playerId);
        }

        // ==========================================
        // 内部方法
        // ==========================================

        /// <summary>获取当前 cameraIndex 对应的 CAM（含空值防护）</summary>
        private TrackingPlayerCAM _GetCurrentCAM()
        {
            if (trackingCAMs == null || trackingCAMs.Length == 0) return null;
            if (cameraIndex < 0 || cameraIndex >= trackingCAMs.Length) return null;
            return trackingCAMs[cameraIndex];
        }

        /// <summary>
        /// 应用 cameraIndex：边界检查 → 设置 isPlaneListening 标志 → 刷新 UI。
        /// 仅当前 CAM 的 isPlaneListening=true，其余为 false。
        /// </summary>
        private void _ApplyCameraIndex()
        {
            if (trackingCAMs == null || trackingCAMs.Length == 0) return;

            // 边界检查（拒绝越界）
            if (cameraIndex < 0) cameraIndex = 0;
            if (cameraIndex >= trackingCAMs.Length) cameraIndex = trackingCAMs.Length - 1;

            // 设置监听标志：当前 CAM=true，其余=false
            for (int i = 0; i < trackingCAMs.Length; i++)
            {
                if (trackingCAMs[i] != null)
                {
                    trackingCAMs[i].isPlaneListening = (i == cameraIndex);
                }
            }

            // 从当前 CAM 刷新所有 UI
            _RefreshUI();

            // 更新机位文本
            if (cameraIndexText != null)
            {
                cameraIndexText.text = string.Format("{0}/{1}", cameraIndex + 1, trackingCAMs.Length);
            }
        }

        /// <summary>写入跟踪类型（TempWrite 感知）</summary>
        private void _WriteTrackingID(int id, TrackingPlayerCAM cam)
        {
            if (_tempWriteMode)
                cam._SetTrackingIDPreview(id);
            else
                cam.SetTrackingDataType(id);
        }

        /// <summary>写入位置偏移（TempWrite 感知）</summary>
        private void _WritePositionOffset(Vector3 offset, TrackingPlayerCAM cam)
        {
            if (_tempWriteMode)
                cam._SetPositionOffsetPreview(offset);
            else
                cam.SetPositionOffset(offset);
        }

        /// <summary>写入相对模式（TempWrite 感知）</summary>
        private void _WriteUseRelativePosition(bool value, TrackingPlayerCAM cam)
        {
            if (_tempWriteMode)
                cam._SetUseRelativePositionPreview(value);
            else
                cam.SetUseRelativePosition(value);
        }

        /// <summary>写入旋转反转（TempWrite 感知）</summary>
        private void _WriteRotationOffset(bool value, TrackingPlayerCAM cam)
        {
            if (_tempWriteMode)
                cam._SetRotationOffsetPreview(value);
            else
                cam.SetRotationOffset(value);
        }

        /// <summary>写入旋转锁定（TempWrite 感知）</summary>
        private void _WriteRotationLock(int axis, bool locked, TrackingPlayerCAM cam)
        {
            if (_tempWriteMode)
                cam._SetRotationLockPreview(axis, locked);
            else
                cam.SetRotationLock(axis, locked);
        }

        /// <summary>写入指示器显示（TempWrite 感知）</summary>
        private void _WriteAppearance(bool value, TrackingPlayerCAM cam)
        {
            if (_tempWriteMode)
                cam._SetAppearancePreview(value);
            else
                cam.SetAppearance(value);
        }

        /// <summary>清理旧玩家按钮</summary>
        private void _ClearPlayerButtons()
        {
            if (_playerButtons == null) return;
            for (int i = 0; i < _playerButtons.Length; i++)
            {
                if (_playerButtons[i] != null)
                {
                    Destroy(_playerButtons[i]);
                }
            }
            _playerButtons = new GameObject[0];
        }
    }
}