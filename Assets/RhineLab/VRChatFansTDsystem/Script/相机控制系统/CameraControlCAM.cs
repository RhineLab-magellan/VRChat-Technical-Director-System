
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace RhineLab.VRChatFansTDsystem
{

    /// <summary>
    /// 相机位姿微调执行系统 — Manual 网络同步。
    /// 6 轴微调（localPosition XYZ + localRotation XYZ），
    /// 采用简单渐进式插值（Vector3.MoveTowards / Quaternion.RotateTowards）。
    ///
    /// 插值方案：
    ///   position = Vector3.MoveTowards(current, _targetPosition, speed * deltaTime)
    ///   rotation = Quaternion.RotateTowards(current, _targetRotation, speed * deltaTime)
    ///   到达判定：|current - target| < 0.01（位置距离 + 旋转角度）
    ///
    /// 变量组织（CAM 脚本 = 区域 1+2+3）：
    ///   区域 1: 组件注册 — controlTransform / controlPlane
    ///   区域 2: 同步变量 — [UdonSynced] _targetPosition / _targetRotation（Manual）
    ///   区域 3: 可设置属性 — speed / 阈值 / 运行时状态
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class CameraControlCAM : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — Inspector 直接引用
        // ==========================================

        /// <summary>控制的 Transform（通常为 CameraTransform）</summary>
        public Transform controlTransform;

        /// <summary>位姿控制面板引用（可选，用于同步 UI 显示）</summary>
        public CameraControlPlane controlPlane;

        // ==========================================
        // 区域 2: 同步变量 — Manual 同步
        // ==========================================

        /// <summary>目标本地位置（Manual 同步）</summary>
        [UdonSynced] private Vector3 _targetPosition;

        /// <summary>目标本地旋转（Manual 同步）</summary>
        [UdonSynced] private Quaternion _targetRotation;

        // ==========================================
        // 区域 3: 可设置属性 — Inspector 配置项
        // ==========================================

        [Header("插值配置")]
        /// <summary>移动速度（由 CameraControlPlane 速度 Slider 动态调整）</summary>
        public float speed = 1f;

        [Header("调试")]
        public bool debugMode;

        // ---- 运行时私有状态 ----

        /// <summary>
        /// 到达判定阈值。
        /// 同时适用于位置（距离差）和旋转（角度差，通过 Quaternion.Angle 判定）。
        /// </summary>
        private const float ARRIVE_THRESHOLD = 0.01f;

        /// <summary>是否正在插值移动中</summary>
        private bool _isMoving;

        /// <summary>初始化就绪标志</summary>
        private bool _initialized;

        /// <summary>Plane 是否正在监听本 CAM（由 Plane 在切换 cameraIndex 时控制）</summary>
        public bool isPlaneListening;

        /// <summary>上次 RequestSerialization 时间（用于 0.5s 冷却）</summary>
        private float _lastSerializationTime;

        /// <summary>是否有待刷新的序列化（冷却期间积累了新目标）</summary>
        private bool _pendingSerialization;

        /// <summary>RequestSerialization 最小间隔（秒）</summary>
        private const float SERIALIZATION_COOLDOWN = 0.5f;

        // ==========================================
        // 公共属性 — 供 CameraControlPlane 读取
        // ==========================================

        /// <summary>当前目标位置</summary>
        public Vector3 TargetPosition { get { return _targetPosition; } }

        /// <summary>当前目标旋转（Euler 角，供面板显示）</summary>
        public Vector3 TargetRotationEuler { get { return _targetRotation.eulerAngles; } }

        // ==========================================
        // Unity 生命周期
        // ==========================================

        void Start()
        {
            // 以当前 Transform 的实际位姿作为初始目标
            if (controlTransform != null)
            {
                _targetPosition = controlTransform.localPosition;
                _targetRotation = controlTransform.localRotation;
            }

            _initialized = true;

            // 应用初始同步状态
            OnDeserialization();
        }

        void Update()
        {
            if (controlTransform == null) return;
            if (!_isMoving) return;

            Vector3 currentPos = controlTransform.localPosition;
            Quaternion currentRot = controlTransform.localRotation;

            // 简单渐进式插值
            float step = speed * Time.deltaTime;
            Vector3 newPos = Vector3.MoveTowards(currentPos, _targetPosition, step);
            Quaternion newRot = Quaternion.RotateTowards(currentRot, _targetRotation, step);

            controlTransform.localPosition = newPos;
            controlTransform.localRotation = newRot;

            // 到达判定
            float posDiff = Vector3.Distance(newPos, _targetPosition);
            float rotDiff = Quaternion.Angle(newRot, _targetRotation);

            if (posDiff < ARRIVE_THRESHOLD && rotDiff < ARRIVE_THRESHOLD)
            {
                // 精确设为目标值，消除微小误差
                controlTransform.localPosition = _targetPosition;
                controlTransform.localRotation = _targetRotation;
                _isMoving = false;

                if (debugMode)
                {
                    Debug.Log("[CameraControlCAM] 已到达目标位姿，停止插值");
                }
            }
        }

        // ==========================================
        // 公共方法 — CameraControlPlane 调用接口
        // ==========================================

        /// <summary>
        /// 设置目标位姿（由 CameraControlPlane 调用）。
        /// 获取所有权 → 写入同步变量 → RequestSerialization → 启动本地插值。
        /// </summary>
        /// <param name="position">目标本地位置</param>
        /// <param name="rotation">目标本地旋转</param>
        public void SetTargetPose(Vector3 position, Quaternion rotation)
        {
            // 所有权检查与获取
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            _targetPosition = position;
            _targetRotation = rotation;
            _isMoving = true;

            // Manual 同步：0.5s 冷却防抖动 — 拖拽 Slider 时避免每帧序列化
            _TrySerialize();

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[CameraControlCAM] SetTargetPose — pos: {0}, rot: {1}",
                    _targetPosition, _targetRotation.eulerAngles
                ));
            }
        }

        /// <summary>
        /// 设置移动速度（由 CameraControlPlane 速度 Slider 调用）。
        /// </summary>
        /// <param name="s">新的速度值</param>
        public void SetSpeed(float s)
        {
            speed = Mathf.Max(0.01f, s);
        }

        /// <summary>
        /// 立即停止插值，以当前位置作为目标（由 Plane 暂停按钮调用）。
        /// </summary>
        public void StopMovement()
        {
            if (!Networking.IsOwner(gameObject)) return;

            if (controlTransform != null)
            {
                _targetPosition = controlTransform.localPosition;
                _targetRotation = controlTransform.localRotation;
            }
            _isMoving = false;
            _TrySerialize();
        }

        /// <summary>
        /// 将目标位姿重置为当前 Transform 的位姿（由 Plane 重置按钮调用）。
        /// </summary>
        public void ResetToCurrent()
        {
            if (!Networking.IsOwner(gameObject)) return;

            if (controlTransform != null)
            {
                _targetPosition = controlTransform.localPosition;
                _targetRotation = controlTransform.localRotation;
            }
            _isMoving = false;

            // 同步更新面板 Slider 显示（仅当 Plane 正在监听本 CAM）
            if (isPlaneListening && controlPlane != null)
            {
                controlPlane._RefreshAllSliders(_targetPosition, _targetRotation.eulerAngles);
            }

            _TrySerialize();

            if (debugMode)
            {
                Debug.Log("[CameraControlCAM] ResetToCurrent — 目标已重置为当前位姿");
            }
        }

        // ==========================================
        // 内部方法 — 序列化冷却
        // ==========================================

        /// <summary>
        /// 带 0.5s 冷却的序列化。
        /// 冷却期间调用仅标记 _pendingSerialization，冷却结束后由 _FlushPendingSerialization 发送最新值。
        /// 确保拖拽 Slider 时不会每帧 RequestSerialization，同时保证最终值不丢失。
        /// </summary>
        private void _TrySerialize()
        {
            float elapsed = Time.time - _lastSerializationTime;
            if (elapsed >= SERIALIZATION_COOLDOWN)
            {
                // 冷却已过 → 立即发送
                RequestSerialization();
                _lastSerializationTime = Time.time;
                _pendingSerialization = false;
            }
            else if (!_pendingSerialization)
            {
                // 冷却中，首次标记待刷新 → 调度延迟发送
                _pendingSerialization = true;
                float delay = SERIALIZATION_COOLDOWN - elapsed;
                SendCustomEventDelayedSeconds("_FlushPendingSerialization", delay);
            }
            // else: 冷却中且已标记 → 什么都不做（延迟回调会发送最新值）
        }

        /// <summary>
        /// 冷却结束回调 — 发送冷却期间积累的最新目标位姿。
        /// </summary>
        public void _FlushPendingSerialization()
        {
            if (_pendingSerialization && Networking.IsOwner(gameObject))
            {
                RequestSerialization();
                _lastSerializationTime = Time.time;
                _pendingSerialization = false;

                if (debugMode)
                {
                    Debug.Log("[CameraControlCAM] _FlushPendingSerialization — 延迟序列化已发送");
                }
            }
        }

        // ==========================================
        // VRChat 事件
        // ==========================================

        /// <summary>
        /// 同步变量反序列化回调。
        /// 非 Owner 在此收到 Owner 设定的目标位姿 → 启动本地插值。
        ///
        /// _isMoving 强制设为 true 的设计意图：
        /// 每次收到新同步目标时，强制状态机经过 Update() 插值逻辑，
        /// 由 Update() 内部的到达判定（posDiff + rotDiff < ARRIVE_THRESHOLD）
        /// 自然决定是启动插值还是立即到达。这保证了非 Owner 与 Owner
        /// 的目标位姿最终一致，即使 Owner 发送的是"停止"（目标=当前位置）。
        /// </summary>
        public override void OnDeserialization()
        {
            if (!_initialized) return;

            // Owner：Start() 末尾触发，已在 Start 中设置初始目标
            // Non-Owner：收到新的目标位姿 → 强制启动插值检查
            if (!Networking.IsOwner(gameObject))
            {
                _isMoving = true;

                // 同步更新面板 Slider（非 Owner 也可以看到目标值，仅当 Plane 正在监听本 CAM）
                if (isPlaneListening && controlPlane != null)
                {
                    controlPlane._RefreshAllSliders(_targetPosition, _targetRotation.eulerAngles);
                }
            }
        }

        /// <summary>
        /// 所有权转移回调。
        /// 新 Owner 以当前实际位姿作为目标，避免突变。
        /// </summary>
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (Networking.IsOwner(gameObject))
            {
                // 新 Owner — 以当前 Transform 位姿为基准
                if (controlTransform != null)
                {
                    _targetPosition = controlTransform.localPosition;
                    _targetRotation = controlTransform.localRotation;
                }
                _isMoving = false;
            }

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[CameraControlCAM] 所有权转移 → {0} (ID: {1})",
                    player != null ? player.displayName : "null",
                    player != null ? player.playerId : -1
                ));
            }
        }
    }

}
