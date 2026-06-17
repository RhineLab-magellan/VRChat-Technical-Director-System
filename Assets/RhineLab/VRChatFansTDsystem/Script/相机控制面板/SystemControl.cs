
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;


namespace RhineLab.VRChatFansTDsystem
{
    /// <summary>
    /// 导播控制面板 — 纯本地 UI，不参与网络同步。
    ///
    /// 核心职责：
    ///   1. 多系统管理：维护 VRCTDCamera[] 直接引用
    ///   2. 参数下发：将 UI 操作通过直接引用调用 VRCTDCamera._StartChanger() 等
    ///   3. 满帧率中心判定：统一判定哪些系统需要满帧率运行
    ///   4. 缩略图循环：自驱动循环，按配置帧率轮询渲染非直播相机
    ///
    /// 满帧率判定公式（每个 Camera 独立计算）：
    ///   needFullFrame[i] = _fullFrameRate[i] || cameras[i].CurrentSubsystem.NeedRuning
    ///
    /// 变量组织（Plane 脚本 = 区域 1+4）：
    ///   区域 1: 组件注册 — VRCTDCamera[] / CameraOutputSystem
    ///   区域 4: UI 依赖 — Toggle / Button
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SystemControl : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — Inspector 直接引用
        // ==========================================

        /// <summary>所有相机系统数组</summary>
        public VRCTDCamera[] cameras;

        /// <summary>显示输出系统引用</summary>
        public CameraOutputSystem outputSystem;

        [Header("缩略图配置")]
        /// <summary>缩略图刷新帧率（次/秒），默认 1f</summary>
        public float thumbnailRefreshRate = 1f;

        /// <summary>缩略图功能开关 Toggle（可选）</summary>
        public Toggle thumbnailToggle;

        [Header("调试")]
        public bool debugMode;

        // ---- 运行时私有状态 ----

        /// <summary>满帧率标志数组（长度 = cameras.Length）</summary>
        private bool[] _fullFrameRate;

        /// <summary>换算后的缩略图延迟秒数</summary>
        private float _thumbnailDelay;

        /// <summary>缩略图循环是否正在运行</summary>
        private bool _isThumbnailActive;

        /// <summary>初始化就绪标志</summary>
        private bool _initialized;

        /// <summary>循环代数计数器（用于取消延迟事件 — 重检时递增）</summary>
        private int _loopGeneration;

        /// <summary>最近一次调度时记录的代数（_ThumbnailLoop_Continue 中比对）</summary>
        private int _scheduledGeneration;

        // ==========================================
        // Unity 生命周期
        // ==========================================

        void Start()
        {
            // 初始化满帧率数组
            if (cameras != null)
            {
                _fullFrameRate = new bool[cameras.Length];
            }

            // 计算缩略图延迟（避免除零）
            _thumbnailDelay = 1f / Mathf.Max(0.1f, thumbnailRefreshRate);

            _initialized = true;

            // 延迟启动缩略图循环（给予其他模块 Start + OnDeserialization 完成时间）
            // 使用 1 帧延迟确保所有 CAM 模块的 Start 已执行完毕
            SendCustomEventDelayedFrames("_DelayedStart", 2);

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[SystemControl] Start 完成 — cameras: {0}, thumbnailDelay: {1:F2}s, 等待延迟启动",
                    cameras != null ? cameras.Length : 0,
                    _thumbnailDelay
                ));
            }
        }

        /// <summary>
        /// 延迟启动 — 在所有 CAM 模块 Start 完成后启动缩略图循环。
        /// </summary>
        public void _DelayedStart()
        {
            StartThumbnailLoop();
        }

        // ==========================================
        // 公共方法 — CameraOutputSystem 回调
        // ==========================================

        /// <summary>
        /// 由 CameraOutputSystem 在切换发起时调用（可选，提前通知）。
        /// </summary>
        public void _DisplayChanging(int nextTexIndex)
        {
            if (debugMode) Debug.Log(string.Format(
                "[SystemControl] _DisplayChanging — 即将切换到 index={0}", nextTexIndex
            ));
        }

        /// <summary>
        /// 由 CameraOutputSystem.OnTransitionComplete() 调用。
        /// 更新满帧率标志 → 触发缩略图重检。
        /// </summary>
        /// <param name="currentTexIndex">当前显示的系统索引（即 CameraOutputSystem.currentTexIndex）。
        ///   >= 0 → cameraTextures 索引；< 0 → TV 画面源（全部 Camera 退出满帧率）。</param>
        public void _DisplayChanged(int currentTexIndex)
        {
            // 更新满帧率标志：只有当前被直播输出的 Camera 系统需要满帧率；
            // TV 画面源（currentTexIndex < 0）时全部 Camera 退出满帧率
            if (_fullFrameRate != null && cameras != null)
            {
                for (int i = 0; i < _fullFrameRate.Length; i++)
                {
                    _fullFrameRate[i] = (currentTexIndex >= 0 && i == currentTexIndex);
                }
            }

            // 触发缩略图重检
            _RequestThumbnailRecheck();

            if (debugMode) Debug.Log(string.Format(
                "[SystemControl] _DisplayChanged — 当前显示系统: index={0}", currentTexIndex
            ));
        }

        // ==========================================
        // 缩略图循环控制
        // ==========================================

        /// <summary>
        /// 缩略图循环入口（首次启动或恢复）。
        /// 由 _DelayedStart() 或外部（如 UI 按钮）调用。
        /// </summary>
        public void StartThumbnailLoop()
        {
            if (_isThumbnailActive) return;

            _isThumbnailActive = true;
            _ThumbnailLoop();

            if (debugMode) Debug.Log("[SystemControl] StartThumbnailLoop — 缩略图循环已启动");
        }

        /// <summary>
        /// 停止缩略图循环。
        /// 由 UI Toggle 关闭时调用。
        /// </summary>
        public void StopThumbnailLoop()
        {
            _isThumbnailActive = false;
            _loopGeneration++; // 使任何待处理的延迟回调失效

            // 关闭所有 Camera（由外部决定是否需要全部关闭）
            if (debugMode) Debug.Log("[SystemControl] StopThumbnailLoop — 缩略图循环已停止");
        }

        /// <summary>
        /// 缩略图重检统一入口。
        /// 递增循环代数使当前待处理的延迟回调失效 → 立即执行一次判定。
        ///
        /// 触发源：
        ///   - CameraOutputSystem 切换完成 → _DisplayChanged → _RequestThumbnailRecheck
        ///   - 用户手动切换缩略图开关 → _RequestThumbnailRecheck
        /// </summary>
        public void _RequestThumbnailRecheck()
        {
            if (!_isThumbnailActive) return;

            // 递增代数：使当前待处理的 _ThumbnailLoop_Continue 延迟回调失效
            _loopGeneration++;

            if (debugMode) Debug.Log(string.Format(
                "[SystemControl] _RequestThumbnailRecheck — gen={0}", _loopGeneration
            ));

            // 立即执行一轮判定
            _ThumbnailLoop();
        }

        /// <summary>
        /// 缩略图循环主逻辑（每轮执行）。
        ///
        /// 流程：
        ///   1. 遍历所有 Camera → 判定满帧率条件 → SetFullFrameRate
        ///   2. 非满帧率 Camera → _RefreshRenderFrame（渲染一帧缩略图）
        ///   3. 自驱动：调度下一轮延迟回调
        /// </summary>
        public void _ThumbnailLoop()
        {
            if (!_isThumbnailActive) return;
            if (cameras == null || cameras.Length == 0) return;

            int myGeneration = _loopGeneration;

            // ======== 1. 判定满帧率并应用到各 Camera ========
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null) continue;

                // 判定公式：被输出系统选中 OR 当前子系统要求满帧率
                bool needFullFrame = false;
                if (_fullFrameRate != null && i < _fullFrameRate.Length)
                {
                    needFullFrame = _fullFrameRate[i];
                }
                if (!needFullFrame && cameras[i].CurrentSubsystem != null)
                {
                    needFullFrame = cameras[i].CurrentSubsystem.NeedRuning;
                }

                cameras[i].SetFullFrameRate(needFullFrame);
            }

            // ======== 2. 非满帧率 Camera 渲染一帧缩略图 ========
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null) continue;

                // 跳过满帧率 Camera（已在持续渲染）
                bool isFullFrame = false;
                if (_fullFrameRate != null && i < _fullFrameRate.Length)
                {
                    isFullFrame = _fullFrameRate[i];
                }
                if (!isFullFrame && cameras[i].CurrentSubsystem != null)
                {
                    isFullFrame = cameras[i].CurrentSubsystem.NeedRuning;
                }
                if (isFullFrame) continue;

                cameras[i]._RefreshRenderFrame();
            }

            // ======== 3. 调度下一轮（自驱动） ========
            _scheduledGeneration = myGeneration;
            SendCustomEventDelayedSeconds("_ThumbnailLoop_Continue", _thumbnailDelay);
        }

        /// <summary>
        /// 缩略图循环延迟回调。
        /// 检查循环代数是否匹配（不匹配 = 被 _RequestThumbnailRecheck 取消）。
        /// 匹配则继续下一轮。
        /// </summary>
        public void _ThumbnailLoop_Continue()
        {
            // 代数不匹配：本轮已被重检请求取消，跳过
            if (_loopGeneration != _scheduledGeneration) return;
            if (!_isThumbnailActive) return;

            _ThumbnailLoop();
        }

        // ==========================================
        // UI 事件回调 — Inspector OnValueChanged 绑定
        // ==========================================

        /// <summary>
        /// 缩略图开关 Toggle 变更回调。
        /// </summary>
        public void _OnThumbnailToggleChanged()
        {
            if (thumbnailToggle == null) return;

            if (thumbnailToggle.isOn)
            {
                StartThumbnailLoop();
            }
            else
            {
                StopThumbnailLoop();
            }
        }

        // ==========================================
        // 公共方法 — 参数下发到 VRCTDCamera
        // （由 UI 控件在 Inspector 中绑定调用）
        // ==========================================

        /// <summary>
        /// 向指定 Camera 下发切换参数。
        /// </summary>
        /// <param name="cameraIndex">VRCTDCamera 在 cameras[] 中的索引</param>
        /// <param name="voidNameID">子系统索引（0=关闭）</param>
        /// <param name="trackingTarget">锚点索引</param>
        /// <param name="playerID">目标玩家 ID</param>
        /// <param name="slarp">缓动开关</param>
        /// <param name="voidObjectActive">关联对象激活</param>
        public void _DispatchStartChanger(int cameraIndex, int voidNameID, int trackingTarget, int playerID, bool slarp, bool voidObjectActive)
        {
            if (cameras == null || cameraIndex < 0 || cameraIndex >= cameras.Length) return;
            if (cameras[cameraIndex] == null) return;

            cameras[cameraIndex]._StartChanger(voidNameID, trackingTarget, playerID, slarp, voidObjectActive);

            if (debugMode) Debug.Log(string.Format(
                "[SystemControl] _DispatchStartChanger → Camera[{0}]: voidNameID={1}, trackingTarget={2}, playerID={3}",
                cameraIndex, voidNameID, trackingTarget, playerID
            ));
        }

        /// <summary>
        /// 刷新单个 Camera 的缩略图（手动触发）。
        /// </summary>
        public void _RefreshSingleThumbnail(int cameraIndex)
        {
            if (cameras == null || cameraIndex < 0 || cameraIndex >= cameras.Length) return;
            if (cameras[cameraIndex] == null) return;

            cameras[cameraIndex]._RefreshRenderFrame();
        }
    }
}
