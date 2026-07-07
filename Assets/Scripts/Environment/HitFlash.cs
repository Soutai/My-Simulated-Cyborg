using System.Collections;
using UnityEngine;

/// <summary>
/// 通用受击视觉反馈：闪一下指定颜色，再恢复到"闪烁前那一刻"的颜色。
/// 不缓存固定的"原始颜色"，而是每次闪烁开始时现取——这样即使物体身上还有别的系统
/// 也在管颜色（比如 NPCAttributes 的濒死变黑），这个组件也不会跟它打架，闪完只会
/// 老老实实地把颜色还给闪烁发生前的那个状态。
/// 任何东西受到伤害/扣血时调用一次 Flash() 即可，不需要关心自己身上颜色逻辑的细节。
/// </summary>
public class HitFlash : MonoBehaviour
{
    [Tooltip("受击时闪烁的颜色")]
    public Color flashColor = Color.red;
    [Tooltip("闪烁总时长（秒）")]
    public float flashDuration = 0.15f;

    /// <summary>
    /// 闪烁进行中标志，供外部系统（比如 NPCAttributes 自己的颜色状态机）判断"现在要不要暂时让路"。
    /// </summary>
    public bool IsFlashing { get; private set; }

    private Renderer targetRenderer;
    private Coroutine flashRoutine;

    void Awake()
    {
        targetRenderer = GetComponentInChildren<Renderer>();
    }

    public void Flash()
    {
        if (targetRenderer == null) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        IsFlashing = true;
        Color colorBeforeFlash = targetRenderer.material.color;

        targetRenderer.material.color = flashColor;
        yield return new WaitForSeconds(flashDuration);

        targetRenderer.material.color = colorBeforeFlash;
        IsFlashing = false;
        flashRoutine = null;
    }
}
