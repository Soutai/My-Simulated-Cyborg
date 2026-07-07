using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 具身智能物理沙盒 - 感知/记忆调试面板。
///
/// 只做一件事：定期把 PerceptionRadar 当前看到的东西 + SpatialMemoryStore 记住的东西，
/// 整理成人类可读的文本，显示在 Canvas 上绑定的 Text 组件里——不用一直盯着 Console 日志找。
///
/// 纯展示层，不产生任何行为逻辑；数据全部来自 PerceptionRadar.GetVisibleObjects() 和
/// SpatialMemoryStore.AllRecords，跟 Prompt 实际用的是同一份感知/记忆数据，
/// 面板上看到什么，大脑（下次思考时）大致就能看到什么。
/// </summary>
[RequireComponent(typeof(PerceptionRadar))]
[RequireComponent(typeof(SpatialMemoryStore))]
public class PerceptionHudDisplay : MonoBehaviour
{
    [Header("UI 绑定")]
    [Tooltip("拖入 Canvas 下面用来显示感知/记忆信息的 Text 组件")]
    public Text displayText;

    [Tooltip("刷新间隔（秒）——不需要每帧都重新整理字符串，省得产生多余的 GC")]
    public float refreshInterval = 0.3f;

    private PerceptionRadar radar;
    private SpatialMemoryStore memoryStore;
    private float timer = 0f;

    void Awake()
    {
        radar = GetComponent<PerceptionRadar>();
        memoryStore = GetComponent<SpatialMemoryStore>();
    }

    void Update()
    {
        if (displayText == null) return;

        timer += Time.deltaTime;
        if (timer < refreshInterval) return;
        timer = 0f;

        displayText.text = BuildDisplayText();
    }

    private string BuildDisplayText()
    {
        var sb = new StringBuilder();

        sb.AppendLine("== 当前感知 ==");
        AppendVisibleObjects(sb);

        sb.AppendLine();
        sb.AppendLine("== 近期记忆 ==");
        AppendMemoryRecords(sb);

        return sb.ToString();
    }

    private void AppendVisibleObjects(StringBuilder sb)
    {
        var visible = radar.GetVisibleObjects();
        if (visible.Count == 0)
        {
            sb.AppendLine("(视野内空无一物)");
            return;
        }

        foreach (var semanticObj in visible)
        {
            float distance = Vector3.Distance(transform.position, semanticObj.transform.position);
            sb.AppendLine($"· {semanticObj.gameObject.name} [{semanticObj.semanticType}] {distance:F1}m");
        }
    }

    private void AppendMemoryRecords(StringBuilder sb)
    {
        bool any = false;

        foreach (var record in memoryStore.AllRecords)
        {
            // 当前还看得见的东西已经在"当前感知"里列过了，记忆列表里不重复显示，跟发给大脑的规则一致
            if (radar.IsCurrentlyVisible(record.LastKnownPosition)) continue;

            float distance = Vector3.Distance(transform.position, record.LastKnownPosition);
            sb.AppendLine($"· {record.EntityId} [{record.SemanticType}] {distance:F1}m，{record.AgeSeconds:F0}秒前");
            any = true;
        }

        if (!any) sb.AppendLine("(暂无记忆)");
    }
}
