using UnityEngine;

public class PromptManager : MonoBehaviour
{
    public string GeneratePhysicsEnginePrompt(
        float satiety,
        string personality,
        string currentTimeStr,
        string serializedRadarJson,
        string currentGrabbedItem,
        string currentGoal = "无")
    {
        string prompt =
            $"# 具身智能物理沙盒任务控制台\n" +
            $"系统时间: {currentTimeStr} | 饱食度: {satiety:F1}/100 | 性格: {personality}\n\n" +
            $"🌟 当前持物: {currentGrabbedItem}\n" +
            $"🎯 当前主要目标: {currentGoal}\n\n" +

            "## 1. 原子物理原语（必须严格遵守坐标系）\n" +
            "- APPLY_FORCE: arg_x >0=右, <0=左 | arg_z >0=前, <0=后 (力度建议-4.0~4.0)\n" +
            "- GRAB / RELEASE / USE_ITEM\n\n" +

            "## 2. 当前感知\n" + serializedRadarJson + "\n\n" +

            "## 3. 核心行为准则（大小脑协作）\n" +
            "- 你是大脑，负责制定持久目标和短期动作。\n" +
            "- 除非出现更高优先级危险（狼极近、饱食度很低），否则要坚持当前目标。\n" +
            "- 【语言限制】输出 JSON 时，其中的 'goal' 字段和 'monologue' 字段的内容必须完全使用【简体中文】。\n" +
            "- 输出JSON时必须包含 'goal' 字段，描述你接下来想达成的持久目标。\n" +
            "- 如果目标已完成或需要变更，请在 goal 中明确写出新目标。\n\n" +

            "请严格输出以下JSON格式：\n" +
            "{\n" +
            "  \"monologue\": \"思考过程...\",\n" +
            "  \"primitive_commands\": [ ... ],\n" +
            "  \"goal\": \"Obtain the nearest Weapon to defend myself\",\n" +
            "  \"goal_target_id\": \"Stick\"\n" +
            "}";

        return prompt;
    }
}