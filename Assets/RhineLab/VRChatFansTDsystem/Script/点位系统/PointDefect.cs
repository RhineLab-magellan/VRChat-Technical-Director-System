
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// 点位附Udon 标准模板 — 与 SubControlSystem 的通信规范。
/// 
/// 通信协议（设计文档 §3.5）：
///   子系统 → 点位:
///     SetProgramVariable("Managerudon", 子系统Udon)   ← NeedCallBack, 子系统 Start()
///     SetProgramVariable("VoidObjectActive", bool)    ← SpecialSignal, _ChangerTarget()
///     GetProgramVariable("FOV") → float               ← FOVDefectUse, _ChangerTarget()
///     SendCustomEvent("_OnPointActivated")            ← SpecialSignal, _ChangerTarget()
///
///   点位 → 子系统:
///     Managerudon.SetProgramVariable("SlarpV", float) ← 点位 UI 变更时回调
///
/// 生命周期：
///   Unity OnEnable()  → 仅设置 _isActive = true（由 SetActive 触发，不做业务逻辑）
///   _OnPointActivated() → 子系统显式激活回调（此时 VoidObjectActive 已写入），调用 _OnPointEnabled() 钩子
///   Unity OnDisable() → 设置 _isActive = false，调用 _OnPointDisabled() 钩子（幂等保护）
///
/// 变量组织（无后缀 = 区域 1+3，无 UI 依赖）：
///   区域 1: 组件注册 — （通过 Managerudon 引用间接获取）
///   区域 3: 可设置属性 — FOV 配置 / 运行时状态
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PointDefect : UdonSharpBehaviour
{
    // ==========================================
    // 区域 1: 组件注册 — 通过 Managerudon 间接引用
    // ==========================================

    // （无 Inspector 直接引用，所有外部引用通过 Managerudon 进行）

    // ==========================================
    // 区域 3: 可设置属性 — 子系统通信变量
    // ==========================================

    /// <summary>
    /// 子系统 UdonBehaviour 引用。
    /// 由 SubControlSystem.Start() 在 NeedCallBack=true 时通过 SetProgramVariable 写入。
    /// 点位通过此引用回调子系统（如 SetProgramVariable("SlarpV", value)）。
    /// </summary>
    [HideInInspector]
    public UdonBehaviour Managerudon;

    /// <summary>
    /// 关联对象激活状态。
    /// 由 SubControlSystem._ChangerTarget() 在 SpecialSignal=true 时通过 SetProgramVariable 写入。
    /// 点位 _OnPointActivated() 中可读取此值判断当前激活上下文。
    /// </summary>
    [HideInInspector]
    public bool VoidObjectActive;

    [Header("预设 FOV")]
    /// <summary>
    /// 预设 FOV 值（度）。
    /// 须为 public — SubControlSystem 通过 GetProgramVariable 读取（仅能访问 public 变量）；
    /// 同时在 Unity Editor Inspector 中可直接配置默认值。
    /// 若点位不需要预设 FOV，保持默认值即可。
    /// </summary>
    public float FOV = 60f;

    [Header("调试")]
    public bool debugMode;

    // ---- 运行时私有状态 ----

    /// <summary>点位是否已激活</summary>
    private bool _isActive;

    // ==========================================
    // Unity 生命周期
    // ==========================================

    void Start()
    {
        if (debugMode)
        {
            Debug.Log(string.Format(
                "[PointDefect] Start — FOV={0}", FOV
            ));
        }
    }

    /// <summary>
    /// Unity 生命周期 — GameObject 激活时由引擎调用。
    /// 仅设置内部标志，不做业务逻辑（业务逻辑由 _OnPointActivated 处理）。
    /// </summary>
    void OnEnable()
    {
        _isActive = true;
    }

    /// <summary>
    /// Unity 生命周期 — GameObject 禁用时由引擎调用。
    /// 幂等保护：已非活跃时跳过，避免重复清理。
    /// </summary>
    void OnDisable()
    {
        if (!_isActive) return;
        _isActive = false;
        _OnPointDisabled();
    }

    // ==========================================
    // 子系统通信 — 接收事件
    // ==========================================

    /// <summary>
    /// 点位激活回调 — 由 SubControlSystem._ChangerTarget() 在 SpecialSignal=true 时
    /// 通过 SendCustomEvent("_OnPointActivated") 调用。
    /// 此时 VoidObjectActive 已被 SetProgramVariable 写入，可直接读取。
    /// 
    /// 与 Unity OnEnable() 的区别：
    ///   - OnEnable() 在每次 SetActive(true) 时触发（包括先停用再激活的场景），不做业务逻辑
    ///   - _OnPointActivated() 仅在子系统显式激活点位时触发，此时通信变量已就绪
    /// </summary>
    public void _OnPointActivated()
    {
        _isActive = true;

        if (debugMode)
        {
            Debug.Log(string.Format(
                "[PointDefect] _OnPointActivated — VoidObjectActive={0}", VoidObjectActive
            ));
        }

        // 调用派生逻辑钩子
        _OnPointEnabled();
    }

    /// <summary>
    /// 点位激活逻辑钩子 — 在此方法中实现自定义激活行为。
    /// 作为模板方法，直接修改此方法体即可。
    /// </summary>
    private void _OnPointEnabled()
    {
        // 在此实现自定义激活逻辑（如启动动画、启用组件等）
    }

    /// <summary>
    /// 点位禁用逻辑钩子 — 在此方法中实现自定义清理行为。
    /// 作为模板方法，直接修改此方法体即可。
    /// </summary>
    private void _OnPointDisabled()
    {
        // 在此实现自定义清理逻辑（如停止动画、禁用组件等）
    }

    // ==========================================
    // 回调子系统 — 辅助方法
    // ==========================================

    /// <summary>
    /// 向子系统写入 SlarpV（缓动速度）。
    /// 由点位 UI（如 Slider OnValueChanged）调用，通过 Managerudon 引用回调子系统。
    /// </summary>
    public void _SetSlarpV(float value)
    {
        if (Managerudon != null)
        {
            Managerudon.SetProgramVariable("SlarpV", value);
        }
    }

    /// <summary>
    /// 向子系统发送自定义事件（通用回调通道）。
    /// </summary>
    public void _SendEventToSubsystem(string eventName)
    {
        if (Managerudon != null)
        {
            Managerudon.SendCustomEvent(eventName);
        }
    }
}

