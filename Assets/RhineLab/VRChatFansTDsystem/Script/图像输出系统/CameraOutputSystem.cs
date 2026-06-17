
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace RhineLab.VRChatFansTDsystem
{

    /// <summary>
    /// 显示输出管理系统 — Manual 网络同步 + Animator 动画过渡。
    /// 合并原 DisPlayerM + FastCameraChanger，升级为动画驱动画面切换。
    ///
    /// 核心功能：
    ///   1. 双纹理预览：_MainTex = 当前投屏，_SubTex = 切换目标
    ///   2. 动画过渡：挂载 Animator 驱动 Shader _Lerp 实现渐变/切换
    ///   3. 批量初始化：启动时设置 RenderTexture 到多个静态显示器
    ///   4. 通知 SystemControl：切换完成时触发满帧率重检
    ///
    /// 配套 Shader：Custom/TextureLerp（_MainTex + _SubTex + _Lerp）
    ///
    /// 变量组织（独立系统 = 区域 1+2+3）：
    ///   区域 1: 组件注册 — Material / RenderTexture[] / Animator / MeshRenderer[] / SystemControl
    ///   区域 2: 同步变量 — [UdonSynced] currentTexIndex / nextTexIndex / animationIndex（Manual）
    ///   区域 3: 可设置属性 — 内部状态
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class CameraOutputSystem : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — Inspector 直接引用
        // ==========================================

        [Header("输出材质")]
        /// <summary>使用 Custom/TextureLerp 的输出材质</summary>
        public Material outputMaterial;

        [Header("纹理源")]
        /// <summary>各 VRCTDCamera 的 RenderTexture</summary>
        public RenderTexture[] cameraTextures;

        /// <summary>TV 画面源（可选）</summary>
        public RenderTexture[] tvTextures;

        [Header("动画")]
        /// <summary>驱动切换动画的 Animator</summary>
        public Animator transitionAnimator;

        [Header("显示器")]
        /// <summary>批量静态显示器</summary>
        public MeshRenderer[] staticDisplays;

        /// <summary>TV 显示器（可选）</summary>
        public MeshRenderer[] tvDisplays;

        [Header("系统引用")]
        /// <summary>SystemControl 引用 — 切换完成时通知</summary>
        public SystemControl systemControl;

        [Header("调试")]
        public bool debugMode;

        // ==========================================
        // 区域 2: 同步变量 — Manual 同步
        // ==========================================

        /// <summary>当前正在显示的 RenderTexture 索引（_MainTex）</summary>
        [UdonSynced] private int _currentTexIndex;

        /// <summary>准备切换到的 RenderTexture 索引（_SubTex）</summary>
        [UdonSynced] private int _nextTexIndex;

        /// <summary>渐变动画类型（0=无动画/初始化/已重置，1+=具体动画样式）</summary>
        [UdonSynced] private int _animationIndex;

        // ==========================================
        // 区域 3: 可设置属性 — 运行时状态
        // ==========================================

        /// <summary>等待动画完成的待切换索引（防快速连续切换丢失目标）</summary>
        private int _pendingNextIndex;

        /// <summary>是否有待处理的切换</summary>
        private bool _hasPendingSwitch;

        /// <summary>是否正在动画过渡中</summary>
        private bool _isTransitioning;

        /// <summary>初始化就绪标志</summary>
        private bool _initialized;

        /// <summary>待处理动画类型</summary>
        private int _pendingAnimationType;

        // ==========================================
        // 公共属性 — 供 SystemControl 读取
        // ==========================================

        /// <summary>当前显示的系统索引</summary>
        public int CurrentTexIndex { get { return _currentTexIndex; } }

        /// <summary>是否正在过渡中</summary>
        public bool IsTransitioning { get { return _isTransitioning; } }

        // ==========================================
        // Unity 生命周期
        // ==========================================

        void Start()
        {
            // 批量初始化静态显示器
            _InitStaticDisplays();

            _initialized = true;

            // 应用初始同步状态
            OnDeserialization();

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[CameraOutputSystem] Start 完成 — cameraTextures: {0}, currentTexIndex: {1}",
                    cameraTextures != null ? cameraTextures.Length : 0,
                    _currentTexIndex
                ));
            }
        }

        // ==========================================
        // VRChat 网络同步回调
        // ==========================================

        /// <summary>
        /// 同步变量反序列化回调。
        /// Late Joiner / 非 Owner 在此恢复显示状态。
        /// </summary>
        public override void OnDeserialization()
        {
            if (!_initialized) return;

            if (_animationIndex == 0)
            {
                // 无动画 / 已重置 — 直接设置 _MainTex
                _isTransitioning = false;
                if (outputMaterial != null
                    && _IsValidIndex(_currentTexIndex))
                {
                    outputMaterial.SetTexture("_MainTex", _GetTexture(_currentTexIndex));
                }
            }
            else
            {
                // 正在动画中 — 设置双纹理并触发本地 Animator 播放
                _isTransitioning = true;
                if (outputMaterial != null)
                {
                    if (_IsValidIndex(_currentTexIndex))
                        outputMaterial.SetTexture("_MainTex", _GetTexture(_currentTexIndex));
                    if (_IsValidIndex(_nextTexIndex))
                        outputMaterial.SetTexture("_SubTex", _GetTexture(_nextTexIndex));
                }

                if (transitionAnimator != null)
                {
                    transitionAnimator.SetInteger("TransitionType", _animationIndex);
                    transitionAnimator.SetBool("StartTransition", true);
                }
            }
        }

        // ==========================================
        // 公共方法 — 切换接口
        // ==========================================

        /// <summary>
        /// 发起画面切换（由 UI Dropdown / 按钮调用）。
        /// 若当前正在过渡中，将目标存入 _pendingNextIndex，
        /// 等待当前动画完成后自动发起下一次切换。
        /// </summary>
        /// <param name="newIndex">目标 RenderTexture 索引</param>
        /// <param name="animationType">渐变动画类型（1+）</param>
        public void _SwitchTo(int newIndex, int animationType)
        {
            // 边界检查（正数索引→cameraTextures，负数索引→tvTextures）
            if (!_IsValidIndex(newIndex))
            {
                if (debugMode) Debug.LogWarning(string.Format(
                    "[CameraOutputSystem] _SwitchTo — newIndex {0} 越界（cameraTextures长度={1}, tvTextures长度={2}）",
                    newIndex,
                    cameraTextures != null ? cameraTextures.Length : 0,
                    tvTextures != null ? tvTextures.Length : 0
                ));
                return;
            }

            // 相同索引 — 无操作（过渡中检查 _nextTexIndex，非过渡检查 _currentTexIndex）
            int compareIndex = _isTransitioning ? _nextTexIndex : _currentTexIndex;
            if (newIndex == compareIndex)
            {
                if (debugMode) Debug.Log(string.Format(
                    "[CameraOutputSystem] _SwitchTo — 目标索引 {0} 与当前{1}相同，跳过",
                    newIndex, _isTransitioning ? "过渡目标" : "显示"
                ));
                return;
            }

            if (_isTransitioning)
            {
                // 正在过渡中 — 排队等待
                _pendingNextIndex = newIndex;
                _pendingAnimationType = animationType;
                _hasPendingSwitch = true;

                if (debugMode) Debug.Log(string.Format(
                    "[CameraOutputSystem] _SwitchTo — 过渡中，已排队: pendingIndex={0}, animType={1}",
                    newIndex, animationType
                ));
                return;
            }

            // 立即执行切换
            _ExecuteSwitch(newIndex, animationType);
        }

        // ==========================================
        // Animation Event 回调
        // ==========================================

        /// <summary>
        /// 动画完成回调 — 由 Animator 的 Animation Event 在动画片段末尾调用。
        /// 交换 Main/Sub 纹理 → 重置 Animator → 通知 SystemControl → 处理排队切换。
        ///
        /// Owner：更新 [UdonSynced] 状态 + RequestSerialization + 通知 SystemControl + 处理排队。
        /// 非 Owner：仅做本地显示更新和 Animator 重置，等待 Owner 的 OnDeserialization 同步最终状态。
        /// </summary>
        public void OnTransitionComplete()
        {
            if (!_isTransitioning) return;

            _isTransitioning = false;

            if (Networking.IsOwner(gameObject))
            {
                // ======== Owner：更新同步状态 + 序列化 ========
                _currentTexIndex = _nextTexIndex;
                _animationIndex = 0;

                // 设置 _MainTex 为当前系统纹理
                if (outputMaterial != null && _IsValidIndex(_currentTexIndex))
                {
                    outputMaterial.SetTexture("_MainTex", _GetTexture(_currentTexIndex));
                }

                // 重置 Animator
                if (transitionAnimator != null)
                {
                    transitionAnimator.SetInteger("TransitionType", 0);
                    transitionAnimator.SetBool("StartTransition", false);
                }

                // 通知 SystemControl → 触发满帧率重检
                if (systemControl != null)
                {
                    systemControl._DisplayChanged(_currentTexIndex);
                }

                RequestSerialization();

                if (debugMode)
                {
                    Debug.Log(string.Format(
                        "[CameraOutputSystem] OnTransitionComplete — 当前显示索引: {0}",
                        _currentTexIndex
                    ));
                }

                // 处理排队切换
                if (_hasPendingSwitch)
                {
                    _hasPendingSwitch = false;
                    int pendingIdx = _pendingNextIndex;
                    int pendingAnim = _pendingAnimationType;
                    _pendingNextIndex = 0;
                    _pendingAnimationType = 0;

                    if (debugMode) Debug.Log(string.Format(
                        "[CameraOutputSystem] 执行排队切换 → index={0}, animType={1}",
                        pendingIdx, pendingAnim
                    ));

                    _ExecuteSwitch(pendingIdx, pendingAnim);
                }
            }
            else
            {
                // ======== 非 Owner：仅本地更新，不等下次反序列化 ========
                // 本地更新 _MainTex 为切换目标纹理
                if (outputMaterial != null && _IsValidIndex(_nextTexIndex))
                {
                    outputMaterial.SetTexture("_MainTex", _GetTexture(_nextTexIndex));
                }

                // 重置本地 Animator
                if (transitionAnimator != null)
                {
                    transitionAnimator.SetInteger("TransitionType", 0);
                    transitionAnimator.SetBool("StartTransition", false);
                }

                // 排队切换由 Owner 的 OnTransitionComplete 处理，
                // 非 Owner 在下次 OnDeserialization 中收到 Owner 发起的新切换。
                _hasPendingSwitch = false;
            }
        }

        // ==========================================
        // 内部方法 — 纹理索引解析
        // ==========================================

        /// <summary>
        /// 根据索引从对应数组中获取 RenderTexture。
        /// 索引语义：>= 0 → cameraTextures[index]，< 0 → tvTextures[-index-1]。
        /// </summary>
        private RenderTexture _GetTexture(int index)
        {
            if (index >= 0)
            {
                if (cameraTextures != null && index < cameraTextures.Length)
                    return cameraTextures[index];
            }
            else
            {
                int tvIndex = -index - 1;
                if (tvTextures != null && tvIndex >= 0 && tvIndex < tvTextures.Length)
                    return tvTextures[tvIndex];
            }
            return null;
        }

        /// <summary>
        /// 验证索引是否在 cameraTextures 或 tvTextures 的有效范围内。
        /// </summary>
        private bool _IsValidIndex(int index)
        {
            if (index >= 0)
                return cameraTextures != null && index < cameraTextures.Length;
            else
            {
                int tvIndex = -index - 1;
                return tvTextures != null && tvIndex >= 0 && tvIndex < tvTextures.Length;
            }
        }

        // ==========================================
        // 内部方法 — 切换执行
        // ==========================================

        /// <summary>
        /// 执行实际切换逻辑。
        /// 设置双纹理 → 配置 Animator → 启动动画 → RequestSerialization。
        /// </summary>
        private void _ExecuteSwitch(int newIndex, int animationType)
        {
            // 所有权检查与获取（在通过所有校验后才获取，避免无效操作触发不必要的所有权转移）
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            _nextTexIndex = newIndex;
            _animationIndex = animationType;
            _isTransitioning = true;

            // 设置双纹理
            if (outputMaterial != null)
            {
                if (_IsValidIndex(_currentTexIndex))
                    outputMaterial.SetTexture("_MainTex", _GetTexture(_currentTexIndex));
                if (_IsValidIndex(_nextTexIndex))
                    outputMaterial.SetTexture("_SubTex", _GetTexture(_nextTexIndex));
            }

            // 配置并触发 Animator
            if (transitionAnimator != null)
            {
                transitionAnimator.SetInteger("TransitionType", animationType);
                transitionAnimator.SetBool("StartTransition", true);
            }

            // Manual 同步
            RequestSerialization();

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[CameraOutputSystem] _ExecuteSwitch — current={0} → next={1}, animType={2}",
                    _currentTexIndex, _nextTexIndex, animationType
                ));
            }
        }

        /// <summary>
        /// 批量初始化静态显示器 — 将所有 staticDisplays 和 tvDisplays 的
        /// _MainTex 设置为 outputMaterial 的主纹理。
        /// </summary>
        private void _InitStaticDisplays()
        {
            if (outputMaterial == null) return;

            Texture mainTex = outputMaterial.GetTexture("_MainTex");
            if (mainTex == null) return;

            // 静态显示器
            if (staticDisplays != null)
            {
                for (int i = 0; i < staticDisplays.Length; i++)
                {
                    if (staticDisplays[i] != null)
                    {
                        staticDisplays[i].material.SetTexture("_MainTex", mainTex);
                    }
                }
            }

            // TV 显示器
            if (tvDisplays != null)
            {
                for (int i = 0; i < tvDisplays.Length; i++)
                {
                    if (tvDisplays[i] != null)
                    {
                        tvDisplays[i].material.SetTexture("_MainTex", mainTex);
                    }
                }
            }

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[CameraOutputSystem] _InitStaticDisplays — static:{0}, tv:{1}",
                    staticDisplays != null ? staticDisplays.Length : 0,
                    tvDisplays != null ? tvDisplays.Length : 0
                ));
            }
        }

        // ==========================================
        // VRChat 事件
        // ==========================================

        /// <summary>
        /// 所有权转移回调。
        /// </summary>
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[CameraOutputSystem] 所有权转移 → {0} (ID: {1})",
                    player != null ? player.displayName : "null",
                    player != null ? player.playerId : -1
                ));
            }
        }
    }

}
