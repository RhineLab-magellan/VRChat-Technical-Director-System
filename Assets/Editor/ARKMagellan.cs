using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// 麦哲伦工具箱 —— 编辑器工具集
/// </summary>
public static class ARKMagellan
{
    /// <summary>
    /// 扫描全场景所有 UI 可交互组件（Selectable），将其导航 (Navigation) 全部设为 None。
    /// </summary>
    [MenuItem("麦哲伦工具箱/UI 导航全部设为 None", false, 0)]
    public static void SetAllUINavigationToNone()
    {
        Selectable[] allSelectables = Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None);
        int count = 0;

        foreach (Selectable sel in allSelectables)
        {
            Navigation nav = sel.navigation;
            if (nav.mode != Navigation.Mode.None)
            {
                Undo.RecordObject(sel, "设置 UI 导航为 None");
                nav.mode = Navigation.Mode.None;
                sel.navigation = nav;
                count++;
            }
        }

        EditorUtility.DisplayDialog(
            "麦哲伦工具箱",
            $"扫描完成！共找到 {allSelectables.Length} 个 UI 组件，已修改 {count} 个。",
            "确定"
        );

        Debug.Log($"[麦哲伦工具箱] 共处理 {allSelectables.Length} 个 Selectable，已将 {count} 个组件的导航设为 None。");
    }
}
