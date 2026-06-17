
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace RhineLab.VRChatFansTDsystem
{

    /// <summary>
    /// 点位子系统基类 — 纯本地执行（BehaviourSyncMode.None）。
    /// 定义四标志配置与生命周期方法，由 VRCTDCamera 管理和调用。
    ///
    /// 四标志协同逻辑：
    ///   Start():
    ///     [NeedCallBack] → 遍历 Targets → 获取点位附 Udon → SetProgramVariable("Managerudon", 自身UdonBehaviour)
    ///   
    ///   _ChangerTarget():
    ///     停用全部 Target → 激活 Targets[CameraTrackingTarget]
    ///     [SpecialSignal] → SetProgramVariable("VoidObjectActive", ...) → SendCustomEvent("OnEnable")
    ///     [FOVDefectUse]  → GetProgramVariable("FOV") → _camera.fovControlCAM.ApplyDefectFOV(fov)
    ///
    /// 变量组织（无后缀 = 区域 1+2+3，无 UI 依赖）：
    ///   区域 1: 组件注册 — 通过 _camera 引用间接获取
    ///   区域 2: 同步变量 — 无（None 同步模式）
    ///   区域 3: 可设置属性 — Inspector 配置项（四标志）+ 运行时状态
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SubControlSystem : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — 通过 VRCTDCamera 间接引用
        // ==========================================

        /// <summary>所属的 VRCTDCamera 引用（由 _SetCamera 注入）</summary>
        private VRCTDCamera _camera;

        // ==========================================
        // 区域 3: 可设置属性 — Inspector 配置项
        // ==========================================

        [Header("点位配置")]
        /// <summary>子系统管理的目标点位数组（每个元素为带 UdonBehaviour 的点位 GameObject）</summary>
        public GameObject[] Targets;

        [Header("四标志")]
        /// <summary>子系统是否需要 Camera 持续渲染（满帧率），true=禁止缩略图。由 SystemControl 缩略图循环读取。</summary>
        public bool NeedRuning;

        /// <summary>切换点位时是否通知点位附 Udon（SendCustomEvent("OnEnable") + SetProgramVariable）</summary>
        public bool SpecialSignal;

        /// <summary>初始化时是否将自身 UdonBehaviour 注册到每个点位的 "Managerudon" 变量</summary>
        public bool NeedCallBack;

        /// <summary>切换点位时是否从点位读取预设 FOV 并推送到 FovControlCAM</summary>
        public bool FOVDefectUse;

        [Header("调试")]
        /// <summary>调试模式开关（控制日志输出）</summary>
        public bool debugMode;

        // ---- 运行时私有状态 ----

        /// <summary>
        /// 缓动速度值。
        /// 点位附 Udon 在运行时通过 Managerudon.SetProgramVariable("SlarpV", newValue) 写入，
        /// 由子系统的 Update() 或其他缓动逻辑读取使用。
        /// </summary>
        public float SlarpV;

        // ==========================================
        // Unity 生命周期
        // ==========================================

        void Start()
        {
            // NeedCallBack: 初始化时将自身 UdonBehaviour 引用注册到每个点位附 Udon
            // 点位获得 Managerudon 引用后，可通过 SetProgramVariable("SlarpV", ...) 回调
            if (NeedCallBack && Targets != null && Targets.Length > 0)
            {
                UdonBehaviour myUdon = _GetOwnUdon();
                if (myUdon == null)
                {
                    if (debugMode) Debug.LogWarning("[SubControlSystem] Start — 无法获取自身 UdonBehaviour，跳过 NeedCallBack 注册");
                    return;
                }

                for (int i = 0; i < Targets.Length; i++)
                {
                    if (Targets[i] == null) continue;

                    UdonBehaviour pointUdon = _GetPointUdon(Targets[i]);
                    if (pointUdon == null)
                    {
                        if (debugMode) Debug.LogWarning(string.Format(
                            "[SubControlSystem] Start — Target[{0}] 上未找到 UdonBehaviour，跳过 Managerudon 注册", i
                        ));
                        continue;
                    }

                    pointUdon.SetProgramVariable("Managerudon", myUdon);

                    if (debugMode)
                    {
                        Debug.Log(string.Format(
                            "[SubControlSystem] Start — 已将 Managerudon 注册到 Target[{0}]: {1}",
                            i, Targets[i].name
                        ));
                    }
                }
            }
        }

        // ==========================================
        // 公共方法 — VRCTDCamera 调用接口
        // ==========================================

        /// <summary>
        /// 由 VRCTDCamera._InitializeSubsystems() 调用，注入相机引用。
        /// 子系统通过 _camera 获取所有所需组件（Camera、Transform、RenderTexture 等），
        /// 不再通过 transform.parent.Find(...) 查找。
        /// </summary>
        public void _SetCamera(VRCTDCamera camera)
        {
            _camera = camera;
        }

        /// <summary>
        /// 点位切换 — 由 VRCTDCamera._CallCamera() 调用。
        ///
        /// 执行流程：
        ///   1. 停用所有 Targets
        ///   2. 激活 Targets[CameraTrackingTarget]（内部二次边界校验）
        ///   3. [SpecialSignal] → 通知点位附 Udon
        ///   4. [FOVDefectUse]  → 读取点位预设 FOV → 推送到 FovControlCAM
        /// </summary>
        public void _ChangerTarget()
        {
            if (_camera == null)
            {
                if (debugMode) Debug.LogWarning("[SubControlSystem] _ChangerTarget — _camera 为 null，跳过");
                return;
            }
            if (Targets == null || Targets.Length == 0)
            {
                if (debugMode) Debug.LogWarning("[SubControlSystem] _ChangerTarget — Targets 数组为空，跳过");
                return;
            }

            int targetIndex = _camera.CameraTrackingTarget;

            // 防御性边界检查（二次校验，VRCTDCamera._ApplyBoundaryChecks 已做第一次）
            if (targetIndex < 0 || targetIndex > Targets.Length - 1)
            {
                Debug.LogWarning(string.Format(
                    "[SubControlSystem] _ChangerTarget — 锚点索引越界 ({0}/{1})，操作取消",
                    targetIndex, Targets.Length
                ));
                return;
            }

            // ---- 1. 停用所有点位 ----
            _DeactivateAllTargets();

            // ---- 2. 激活目标点位 ----
            GameObject activeTarget = Targets[targetIndex];
            if (activeTarget != null)
            {
                activeTarget.SetActive(true);

                if (debugMode)
                {
                    Debug.Log(string.Format(
                        "[SubControlSystem] _ChangerTarget — 激活 Target[{0}]: {1}",
                        targetIndex, activeTarget.name
                    ));
                }
            }

            // ---- 3. 获取激活点位的 UdonBehaviour ----
            UdonBehaviour pointUdon = _GetPointUdon(activeTarget);

            // ---- 4. SpecialSignal: 通知点位附 Udon ----
            if (SpecialSignal)
            {
                if (pointUdon != null)
                {
                    // 传递当前激活状态
                    bool voidActive = _camera.VoidObjectActive;
                    pointUdon.SetProgramVariable("VoidObjectActive", voidActive);

                    // 触发点位的 OnEnable 自定义事件
                    pointUdon.SendCustomEvent("OnEnable");

                    if (debugMode)
                    {
                        Debug.Log(string.Format(
                            "[SubControlSystem] _ChangerTarget — SpecialSignal: VoidObjectActive={0}, 已发送 OnEnable",
                            voidActive
                        ));
                    }
                }
                else if (debugMode)
                {
                    Debug.LogWarning(string.Format(
                        "[SubControlSystem] _ChangerTarget — SpecialSignal=true 但 Target[{0}] 上未找到 UdonBehaviour",
                        targetIndex
                    ));
                }
            }

            // ---- 5. FOVDefectUse: 从点位读取预设 FOV 并推送到 FovControlCAM ----
            if (FOVDefectUse)
            {
                if (pointUdon != null && _camera.fovControlCAM != null)
                {
                    // GetProgramVariable 返回 object，强制转换为 float
                    float defectFOV = (float)pointUdon.GetProgramVariable("FOV");
                    _camera.fovControlCAM.ApplyDefectFOV(defectFOV);

                    if (debugMode)
                    {
                        Debug.Log(string.Format(
                            "[SubControlSystem] _ChangerTarget — FOVDefectUse: 从点位读取 FOV={0}，已推送到 FovControlCAM",
                            defectFOV
                        ));
                    }
                }
                else if (debugMode)
                {
                    if (pointUdon == null)
                        Debug.LogWarning(string.Format(
                            "[SubControlSystem] _ChangerTarget — FOVDefectUse=true 但 Target[{0}] 上未找到 UdonBehaviour",
                            targetIndex
                        ));
                    if (_camera.fovControlCAM == null)
                        Debug.LogWarning("[SubControlSystem] _ChangerTarget — FOVDefectUse=true 但 _camera.fovControlCAM 为 null");
                }
            }
        }

        // ==========================================
        // 内部方法 — 辅助
        // ==========================================

        /// <summary>
        /// 从目标 GameObject 上获取点位附 Udon（UdonBehaviour 组件）。
        /// 
        /// 注意：UdonSharp 禁用泛型 GetComponent&lt;T&gt;()，须使用非泛型版本。
        /// 若点位附 Udon 在子节点上，需将 Targets 元素指向该子节点。
        /// </summary>
        /// <param name="target">目标 GameObject（null 安全）</param>
        /// <returns>UdonBehaviour 引用，未找到返回 null</returns>
        private UdonBehaviour _GetPointUdon(GameObject target)
        {
            if (target == null) return null;
            return (UdonBehaviour)target.GetComponent(typeof(UdonBehaviour));
        }

        /// <summary>
        /// 获取自身 GameObject 上的 UdonBehaviour 组件。
        /// 用于 SetProgramVariable("Managerudon", myUdon) 注册回调。
        /// </summary>
        private UdonBehaviour _GetOwnUdon()
        {
            return (UdonBehaviour)GetComponent(typeof(UdonBehaviour));
        }

        /// <summary>
        /// 停用所有 Targets 中的 GameObject。
        /// </summary>
        private void _DeactivateAllTargets()
        {
            if (Targets == null) return;

            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i] != null)
                {
                    Targets[i].SetActive(false);
                }
            }
        }
    }

}
