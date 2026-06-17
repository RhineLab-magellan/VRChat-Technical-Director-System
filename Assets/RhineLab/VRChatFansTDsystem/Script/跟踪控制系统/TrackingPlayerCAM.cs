
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

namespace RhineLab.VRChatFansTDsystem
{

    /// <summary>
    /// 玩家跟踪执行系统 — Manual 同步 + [NetworkCallable] 带参网络事件。
    /// 跟踪指定玩家的 TrackingData（Head/LeftHand/RightHand/Origin），
    /// 支持相对/绝对模式、旋转锁定、偏移配置。
    ///
    /// 同步机制：
    ///   - _trackingID：[UdonSynced] Manual 同步（Late Joiner 可见）
    ///   - 其余 5 参数：[NetworkCallable] 带参网络事件（一 shot，Late Joiner 不可见）
    ///   - ScheduleSerialization：1 秒延迟合并快速连续修改，减少网络拥堵
    ///
    /// 变量组织（CAM 脚本 = 区域 1+2+3）：
    ///   区域 1: 组件注册 — trackingTransform / trackingIndicator / trackingPlane
    ///   区域 2: 同步变量 — [UdonSynced] _trackingID（Manual）
    ///   区域 3: 可设置属性 — 模式/偏移/锁定配置 + TempWrite 预览变量
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class TrackingPlayerCAM : UdonSharpBehaviour
    {
        // ==========================================
        // 区域 1: 组件注册 — Inspector 直接引用
        // ==========================================

        /// <summary>跟踪的 Transform（通常为 TrackingTarget）</summary>
        public Transform trackingTransform;

        /// <summary>跟踪指示器 Renderer（可选，通过 appearance 控制显示/隐藏）</summary>
        public MeshRenderer trackingIndicator;

        /// <summary>跟踪控制面板引用（可选，用于 UI 回调）</summary>
        public TrackingPlayerPlane trackingPlane;

        // ==========================================
        // 区域 2: 同步变量 — Manual 同步
        // ==========================================

        /// <summary>
        /// 跟踪类型（Manual 同步，Late Joiner 可见）。
        /// 0=Head, 1=LeftHand, 2=RightHand, 3=Origin，其他=关闭跟踪。
        /// </summary>
        [UdonSynced] private int _trackingID;

        // ==========================================
        // 区域 3: 可设置属性 — Inspector 配置项
        // ==========================================

        [Header("跟踪配置")]
        /// <summary>位置偏移（绝对模式下叠加到 TrackingData.position）</summary>
        public Vector3 positionOffset;

        /// <summary>相对模式（true=完全跟随 TrackingData，false=位置+偏移/旋转归零）</summary>
        public bool useRelativePosition;

        /// <summary>旋转反转（相对模式下，true=Quaternion.Inverse(data.rotation)）</summary>
        public bool rotationOffset;

        /// <summary>各轴旋转锁定 [X, Y, Z]，true=锁定该轴（角度归零）</summary>
        public bool[] rotationLock = new bool[3];

        /// <summary>跟踪指示器显示</summary>
        public bool appearance;

        [Header("调试")]
        public bool debugMode;

        // ---- TempWrite 预览变量（TempWrite=true 时写入此组，仅本地生效） ----

        private int _trackingIDPreview;
        private Vector3 _positionOffsetPreview;
        private bool _useRelativePositionPreview;
        private bool _rotationOffsetPreview;
        private bool[] _rotationLockPreview = new bool[3];
        private bool _appearancePreview;

        // ---- 运行时私有状态 ----

        /// <summary>是否正在主动跟踪</summary>
        private bool _trackingActive;

        /// <summary>当前跟踪的玩家引用</summary>
        private VRCPlayerApi _trackingPlayer;

        /// <summary>同步合并延迟标记</summary>
        private bool _syncPending;

        /// <summary>是否已完成首次同步（首次同步全量发送所有参数，后续仅发送变更值）</summary>
        private bool _firstFullSync = true;

        // ---- 上次已发送值（用于增量同步比对） ----
        private int _lastSentTrackingID;
        private Vector3 _lastSentPositionOffset;
        private bool _lastSentUseRelativePosition;
        private bool _lastSentRotationOffset;
        private bool[] _lastSentRotationLock = new bool[3];
        private bool _lastSentAppearance;

        /// <summary>初始化就绪标志</summary>
        private bool _initialized;

        /// <summary>Plane 是否正在监听本 CAM（由 Plane 在切换 cameraIndex 时动态控制）</summary>
        public bool isPlaneListening;

        /// <summary>网络事件合并延迟（秒）</summary>
        private const float SYNC_DELAY = 1f;

        // ==========================================
        // 公共属性 — 供 TrackingPlayerPlane 读取
        // ==========================================

        public int TrackingID { get { return _trackingID; } }
        public bool TrackingActive { get { return _trackingActive; } }

        // ==========================================
        // Unity 生命周期
        // ==========================================

        void Start()
        {
            // 初始化旋转锁定数组
            if (rotationLock == null || rotationLock.Length < 3)
            {
                rotationLock = new bool[3];
            }
            if (_rotationLockPreview == null || _rotationLockPreview.Length < 3)
            {
                _rotationLockPreview = new bool[3];
            }

            // 初始化预览变量为当前工作值
            _trackingIDPreview = _trackingID;
            _positionOffsetPreview = positionOffset;
            _useRelativePositionPreview = useRelativePosition;
            _rotationOffsetPreview = rotationOffset;
            for (int i = 0; i < 3; i++) _rotationLockPreview[i] = rotationLock[i];
            _appearancePreview = appearance;

            _initialized = true;

            // 应用初始同步状态
            OnDeserialization();
        }

        void Update()
        {
            if (!_trackingActive || _trackingPlayer == null) return;
            if (trackingTransform == null) return;

            // 边界检查 trackingID
            if (_trackingID < 0 || _trackingID > 3)
            {
                _trackingActive = false;
                return;
            }

            // 获取跟踪数据
            VRCPlayerApi.TrackingData data = _trackingPlayer.GetTrackingData(
                (VRCPlayerApi.TrackingDataType)_trackingID
            );

            // 父节点世界旋转（用于世界→本地空间转换）
            Transform parentTx = trackingTransform.parent;
            Quaternion parentWorldRot = (parentTx != null) ? parentTx.rotation : Quaternion.identity;

            if (useRelativePosition)
            {
                // 相对模式：世界 TrackingData 转换为本地空间（相对父点位 A 的偏移）
                if (parentTx != null)
                {
                    trackingTransform.localPosition = parentTx.InverseTransformPoint(data.position);
                }
                else
                {
                    trackingTransform.localPosition = data.position;
                }

                if (rotationOffset)
                {
                    // 旋转反转
                    Quaternion worldRot = Quaternion.Inverse(data.rotation);
                    trackingTransform.localRotation = Quaternion.Inverse(parentWorldRot) * worldRot;
                }
                else
                {
                    // 应用旋转锁定
                    Vector3 euler = data.rotation.eulerAngles;
                    if (rotationLock[0]) euler.x = 0f;
                    if (rotationLock[1]) euler.y = 0f;
                    if (rotationLock[2]) euler.z = 0f;
                    Quaternion worldRot = Quaternion.Euler(euler);
                    trackingTransform.localRotation = Quaternion.Inverse(parentWorldRot) * worldRot;
                }
            }
            else
            {
                // 绝对模式：本地偏移 + 旋转归零（相对父点位 A）
                trackingTransform.localPosition = positionOffset;
                trackingTransform.localRotation = Quaternion.identity;
            }
        }

        // ==========================================
        // 公共方法 — TrackingPlayerPlane 调用接口
        // ==========================================

        /// <summary>
        /// 设置跟踪目标玩家（由 TrackingPlayerPlane 快速选名调用）。
        /// 有效目标 → SetOwner → 所有权转移 → 触发跟踪。
        /// </summary>
        /// <param name="playerID">目标玩家的 VRCPlayerApi.playerId</param>
        public void SetTrackingTarget(int playerID)
        {
            VRCPlayerApi target = VRCPlayerApi.GetPlayerById(playerID);
            if (target == null || !target.IsValid())
            {
                if (debugMode) Debug.LogWarning(string.Format(
                    "[TrackingPlayerCAM] SetTrackingTarget — playerID {0} 无效，保持当前目标", playerID
                ));
                return;
            }

            // 所有权转移：将跟踪对象的所有权交给目标玩家
            Networking.SetOwner(target, gameObject);

            _trackingPlayer = target;
            _trackingActive = true;

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[TrackingPlayerCAM] SetTrackingTarget → {0} (ID: {1}), 所有权已转移",
                    target.displayName, target.playerId
                ));
            }

            // 同步 trackingID
            _ScheduleSync();
        }

        /// <summary>
        /// TempWrite 模式：将预览变量应用到工作变量（由 Plane "应用" 按钮调用）。
        /// </summary>
        public void _ApplyPreview()
        {
            if (!Networking.IsOwner(gameObject)) return;

            _trackingID = _trackingIDPreview;
            positionOffset = _positionOffsetPreview;
            useRelativePosition = _useRelativePositionPreview;
            rotationOffset = _rotationOffsetPreview;
            for (int i = 0; i < 3; i++) rotationLock[i] = _rotationLockPreview[i];
            appearance = _appearancePreview;

            _trackingActive = (_trackingID >= 0 && _trackingID <= 3);

            // 同步到其他客户端
            _ScheduleSync();

            if (debugMode) Debug.Log("[TrackingPlayerCAM] _ApplyPreview — 预览已应用并触发同步");
        }

        /// <summary>
        /// TempWrite 模式：放弃预览，恢复为当前工作值。
        /// </summary>
        public void _CancelPreview()
        {
            _trackingIDPreview = _trackingID;
            _positionOffsetPreview = positionOffset;
            _useRelativePositionPreview = useRelativePosition;
            _rotationOffsetPreview = rotationOffset;
            for (int i = 0; i < 3; i++) _rotationLockPreview[i] = rotationLock[i];
            _appearancePreview = appearance;

            // 通知面板刷新 UI（仅当 Plane 正在监听本 CAM）
            if (isPlaneListening && trackingPlane != null)
            {
                trackingPlane._RefreshUI();
            }

            if (debugMode) Debug.Log("[TrackingPlayerCAM] _CancelPreview — 预览已取消，恢复工作值");
        }

        // ==========================================
        // TempWrite 写入方法 — 由 Plane 在 TempWrite=true 时调用
        // 写入预览变量，仅本地生效
        // ==========================================

        public void _SetTrackingIDPreview(int id)
        {
            _trackingIDPreview = Mathf.Clamp(id, 0, 3);
        }

        public void _SetPositionOffsetPreview(Vector3 offset)
        {
            _positionOffsetPreview = offset;
        }

        public void _SetUseRelativePositionPreview(bool value)
        {
            _useRelativePositionPreview = value;
        }

        public void _SetRotationOffsetPreview(bool value)
        {
            _rotationOffsetPreview = value;
        }

        public void _SetRotationLockPreview(int axis, bool locked)
        {
            if (axis >= 0 && axis < 3)
            {
                _rotationLockPreview[axis] = locked;
            }
        }

        public void _SetAppearancePreview(bool value)
        {
            _appearancePreview = value;
        }

        // ==========================================
        // [NetworkCallable] 方法 — 由 _DoSync 通过网络事件调用
        // 所有客户端（含发送者）执行，参数由网络携带
        // ==========================================

        [NetworkCallable]
        public void SetTrackingDataType(int trackingID)
        {
            _trackingID = trackingID;
            _trackingActive = (_trackingID >= 0 && _trackingID <= 3);
        }

        [NetworkCallable]
        public void SetPositionOffset(Vector3 offset)
        {
            positionOffset = offset;
        }

        [NetworkCallable]
        public void SetUseRelativePosition(bool value)
        {
            useRelativePosition = value;
        }

        [NetworkCallable]
        public void SetRotationOffset(bool value)
        {
            rotationOffset = value;
        }

        [NetworkCallable]
        public void SetRotationLock(int axis, bool locked)
        {
            if (axis >= 0 && axis < 3)
            {
                rotationLock[axis] = locked;
            }
        }

        [NetworkCallable]
        public void SetAppearance(bool value)
        {
            appearance = value;
            if (trackingIndicator != null)
            {
                trackingIndicator.enabled = value;
            }
        }

        // ==========================================
        // 同步调度 — 1 秒延迟合并
        // ==========================================

        /// <summary>
        /// 调度延迟同步。若已有待处理的同步则跳过（合并），
        /// 否则启动 1 秒延迟后执行 _DoSync。
        /// </summary>
        private void _ScheduleSync()
        {
            if (!_syncPending)
            {
                _syncPending = true;
                SendCustomEventDelayedSeconds("_DoSync", SYNC_DELAY);
            }
        }

        /// <summary>
        /// 执行延迟同步 — 将所有工作变量通过 NetworkCallable 事件发送到所有客户端。
        /// 由 _ScheduleSync 的延迟事件触发。
        /// </summary>
        public void _DoSync()
        {
            _syncPending = false;
            if (!Networking.IsOwner(gameObject)) return;

            IUdonEventReceiver receiver = (IUdonEventReceiver)this;
            bool anySent = false;

            // 首次同步全量发送；后续仅发送变更值
            if (_firstFullSync || _trackingID != _lastSentTrackingID)
            {
                NetworkCalling.SendCustomNetworkEvent(receiver, NetworkEventTarget.All, "SetTrackingDataType", _trackingID);
                _lastSentTrackingID = _trackingID;
                anySent = true;
            }
            if (_firstFullSync || positionOffset != _lastSentPositionOffset)
            {
                NetworkCalling.SendCustomNetworkEvent(receiver, NetworkEventTarget.All, "SetPositionOffset", positionOffset);
                _lastSentPositionOffset = positionOffset;
                anySent = true;
            }
            if (_firstFullSync || useRelativePosition != _lastSentUseRelativePosition)
            {
                NetworkCalling.SendCustomNetworkEvent(receiver, NetworkEventTarget.All, "SetUseRelativePosition", useRelativePosition);
                _lastSentUseRelativePosition = useRelativePosition;
                anySent = true;
            }
            if (_firstFullSync || rotationOffset != _lastSentRotationOffset)
            {
                NetworkCalling.SendCustomNetworkEvent(receiver, NetworkEventTarget.All, "SetRotationOffset", rotationOffset);
                _lastSentRotationOffset = rotationOffset;
                anySent = true;
            }
            if (_firstFullSync || appearance != _lastSentAppearance)
            {
                NetworkCalling.SendCustomNetworkEvent(receiver, NetworkEventTarget.All, "SetAppearance", appearance);
                _lastSentAppearance = appearance;
                anySent = true;
            }

            // rotationLock 逐轴比较，仅发送变更的轴
            for (int i = 0; i < 3; i++)
            {
                if (_firstFullSync || rotationLock[i] != _lastSentRotationLock[i])
                {
                    NetworkCalling.SendCustomNetworkEvent(receiver, NetworkEventTarget.All, "SetRotationLock", i, rotationLock[i]);
                    _lastSentRotationLock[i] = rotationLock[i];
                    anySent = true;
                }
            }

            _firstFullSync = false;

            // Manual 同步：有变更才序列化
            if (anySent)
            {
                RequestSerialization();
            }

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[TrackingPlayerCAM] _DoSync — trackingID={0}, relative={1}, rotOffset={2}, appearance={3}, anySent={4}",
                    _trackingID, useRelativePosition, rotationOffset, appearance, anySent
                ));
            }
        }

        // ==========================================
        // VRChat 事件
        // ==========================================

        /// <summary>
        /// 同步变量反序列化回调。
        /// Late Joiner 收到 _trackingID 的初始/更新值。
        /// </summary>
        public override void OnDeserialization()
        {
            if (!_initialized) return;

            // 从 [UdonSynced] _trackingID 恢复跟踪状态
            _trackingActive = (_trackingID >= 0 && _trackingID <= 3);

            // 非 Owner 更新面板 UI（仅当 Plane 正在监听本 CAM）
            if (!Networking.IsOwner(gameObject) && isPlaneListening && trackingPlane != null)
            {
                trackingPlane._RefreshUI();
            }
        }

        /// <summary>
        /// 所有权转移回调。
        /// 新 Owner 成为跟踪目标（玩家跟踪自身的 TrackingData）。
        /// </summary>
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player != null)
            {
                _trackingPlayer = player;
                _trackingActive = (_trackingID >= 0 && _trackingID <= 3);
            }

            // 新 Owner 刷新面板（仅当 Plane 正在监听本 CAM）
            if (Networking.IsOwner(gameObject) && isPlaneListening && trackingPlane != null)
            {
                trackingPlane._RefreshUI();
            }

            if (debugMode)
            {
                Debug.Log(string.Format(
                    "[TrackingPlayerCAM] 所有权转移 → {0} (ID: {1}), trackingActive={2}",
                    player != null ? player.displayName : "null",
                    player != null ? player.playerId : -1,
                    _trackingActive
                ));
            }
        }
    }

}
