using UnityEngine;

/// <summary>
/// 具身智能物理沙盒 - 听觉本能反射。
///
/// 只做一件事：感知半径内、视野扇形之外，如果有正在移动的物体（静止的东西不发出声音），
/// 身体真正静止时就本能地转头朝那个方向看去——不联网、不经过大脑，大脑全程不会知道
/// "转头"这件事发生过，它只会在下一次视觉扫描时，发现这东西自然而然地出现在了视野里。
///
/// 这是"决策交给大脑、细小操作下放给反射"这条原则的具体体现：转头张望不构成一次战略决策，
/// 不需要大模型批准，纯粹的刺激-反应。
/// </summary>
[RequireComponent(typeof(CharacterActuator))]
[RequireComponent(typeof(PerceptionRadar))]
public class HearingReflex : MonoBehaviour
{
    private CharacterActuator actuator;
    private PerceptionRadar radar;

    [Header("听觉转向")]
    [Tooltip("移动速度超过此值的物体才会被视为发出了声音，静止的东西不发声")]
    public float noiseVelocityThreshold = 0.5f;

    private readonly Collider[] hearingBuffer = new Collider[32];

    void Awake()
    {
        actuator = GetComponent<CharacterActuator>();
        radar = GetComponent<PerceptionRadar>();
    }

    void FixedUpdate()
    {
        // 只有身体真正静止时才会本能转向——只要还在移动（不管是大脑指挥的还是本能逃跑），
        // 朝向已经由移动方向决定了，不需要也不应该被这里覆盖
        if (!actuator.IsNearlyStationary) return;

        Vector3? soundDirection = FindLoudestUnseenSoundDirection();
        if (soundDirection.HasValue)
        {
            actuator.TurnFacingTowards(soundDirection.Value);
        }
    }

    /// <summary>
    /// 在视野扇形之外找一个"最响"的动静：越近、动得越快就越容易被注意到。
    /// 已经在视野内的东西不需要"转头去看"，本来就看得见。
    /// </summary>
    private Vector3? FindLoudestUnseenSoundDirection()
    {
        if (radar == null) return null;

        GameObject loudestSource = null;
        float loudestScore = 0f;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radar.perceptionRadius, hearingBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            var col = hearingBuffer[i];
            if (col.gameObject == this.gameObject) continue;
            if (col.gameObject == actuator.LeftHandObject || col.gameObject == actuator.RightHandObject) continue;

            SemanticObject semanticObj = col.GetComponent<SemanticObject>();
            if (semanticObj == null) continue;

            // 已经在视野扇形内的东西看得见，不需要靠"听"再转头确认
            if (radar.IsWithinVisionCone(col.transform.position)) continue;

            Rigidbody targetRb = col.GetComponent<Rigidbody>();
            float speed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;
            if (speed < noiseVelocityThreshold) continue; // 静止的东西不发出声音

            float distance = Mathf.Max(Vector3.Distance(transform.position, col.transform.position), 0.5f);
            float loudness = speed / distance; // 越近、动得越快 = 越容易被注意到

            if (loudness > loudestScore)
            {
                loudestScore = loudness;
                loudestSource = col.gameObject;
            }
        }

        if (loudestSource == null) return null;

        Vector3 dir = loudestSource.transform.position - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : (Vector3?)null;
    }
}
