using UnityEngine;
using System.Collections.Generic;

public class PerceptionRadar : MonoBehaviour
{
    [Header("雷达设置")]
    public float perceptionRadius = 8f;

    // 外部可读的感知状态
    public bool HasFoodInSight { get; private set; }
    public bool HasEnemyInSight { get; private set; }
    public bool HasWeaponInSight { get; private set; }

    // 扫描环境，返回给大脑需要的自然语言报告
    public string ScanEnvironment()
    {
        // 每次扫描前重置布尔状态
        HasFoodInSight = false;
        HasEnemyInSight = false;
        HasWeaponInSight = false;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, perceptionRadius);
        if (hitColliders.Length == 0) return "四周一片死寂，视野内没有发现任何植物、野兽或工具。";

        List<string> detectedObjects = new List<string>();
        foreach (var col in hitColliders)
        {
            if (col.gameObject == this.gameObject) continue;

            float distance = Vector3.Distance(transform.position, col.transform.position);
            distance = Mathf.Round(distance * 10f) / 10f; // 留一位小数

            if (col.CompareTag("Food"))
            {
                detectedObjects.Add($"在距离你 {distance} 米处，有一个【看起来能果腹的果子（Food）】。");
                HasFoodInSight = true;
            }
            else if (col.CompareTag("Enemy"))
            {
                detectedObjects.Add($"在距离你 {distance} 米处，有一只【龇牙咧嘴、正在低吼的恶狼（Enemy）】，极度危险。");
                HasEnemyInSight = true;
            }
            else if (col.CompareTag("Weapon"))
            {
                detectedObjects.Add($"在距离你 {distance} 米处，躺着一根【沉重的粗木棍（Weapon）】，可以用来防身打狼。");
                HasWeaponInSight = true;
            }
        }

        if (detectedObjects.Count == 0) return "周围有一些乱石杂草，但没有可以吃的东西、可用的武器，也没有危险。";
        return string.Join("\n", detectedObjects.ToArray());
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, perceptionRadius);
    }
}