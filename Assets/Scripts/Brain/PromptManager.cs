using UnityEngine;

public class PromptManager : MonoBehaviour
{
    // 🌟【精准修复版】：既保留了双手独立持物，又完美还原了底层物理推力的坐标控制参数！
    public string GeneratePhysicsEnginePrompt(
        float satiety,
        string personality,
        string currentTimeStr,
        string serializedRadarJson,
        string leftHandItem,
        string rightHandItem,
        string currentGoal = "无")
    {
        string leftItemStr = string.IsNullOrEmpty(leftHandItem) ? "空无一物" : leftHandItem;
        string rightItemStr = string.IsNullOrEmpty(rightHandItem) ? "空无一物" : rightHandItem;

        string prompt =
            $"# 具身智能物理沙盒任务控制台\n" +
            $"系统时间: {currentTimeStr} | 饱食度: {satiety:F1}/100 | 性格: {personality}\n\n" +
            $"🌟 左手持物: {leftItemStr}\n" +
            $"🌟 右手持物: {rightItemStr}\n" +
            $"🎯 当前主要目标: {currentGoal}\n\n" +

            "## 1. 原子物理原语（必须严格遵守）\n" +
            "- APPLY_FORCE: 必须提供 arg_x 和 arg_z！（arg_x >0=右, <0=左 | arg_z >0=前, <0=后，建议力度 2.0~4.0）\n" +
            "- GRAB / RELEASE / USE_ITEM: 必须包含 \"hand\" 字段（\"Left\" 或 \"Right\"）！\n\n" +

            "## 2. 当前感知\n" + serializedRadarJson + "\n\n" +

            "## 3. 核心行为准则\n" +
            "- 你是大脑，只负责输出原子原语和行动计划。\n" +
            "- 小脑只会忠实按顺序执行你下发的原子原语，不会自主导航。\n" +
            "- 移动必须通过 APPLY_FORCE 实现。\n" +
            "- 抓取和使用物品依赖实际接近（由执行器处理）。\n" +
            "- 🌟双手独立：可以右手持武器，左手做其他事。\n" +
            "- 🌟两段式进食：必须先 GRAB 食物到手中，再 USE_ITEM 吃掉。\n\n" +

            "## 4.5 多步规划能力（核心机制）\n" +
            "- 你拥有优秀的战略规划能力。**请每次思考时都制定2~4步连贯的行动计划**（plan_steps），让小脑可以连续执行。\n" +
            "- 每个步骤必须是原子原语（APPLY_FORCE / GRAB / RELEASE / USE_ITEM）。\n" +
            "- 对于移动步骤，请明确给出 APPLY_FORCE 的力度（建议总和 2.5~4.0），让单次移动有明显距离。\n" +
            "- 优秀示例：\n" +
            "  - 先 GRAB 木棍 → 再 USE_ITEM 攻击狼\n" +
            "  - GRAB 食物 → USE_ITEM 吃掉\n" +
            "  - APPLY_FORCE 向前移动 → APPLY_FORCE 向右探索\n" +
            "- plan_steps 中每一项必须包含 description、arrival_op、hand（target_id 可选）。\n\n" +

            "## 5. 绝对限制 JSON 响应格式\n" +
            "必须严格返回标准的 JSON 格式块，不要包含任何 markdown 解释。\n" +
            "格式如下：\n" +
            "{\n" +
            "  \"monologue\": \"思考过程（中文）...\",\n" +
            "  \"primitive_commands\": [],\n" +
            "  \"goal\": \"短期目标描述\",\n" +
            "  \"plan_steps\": [\n" +
            "    { \"description\": \"向右前方移动\", \"arrival_op\": \"APPLY_FORCE\", \"hand\": \"\" },\n" +
            "    { \"description\": \"拾取木棍\", \"arrival_op\": \"GRAB\", \"hand\": \"Right\", \"target_id\": \"Stick\" }\n" +
            "  ]\n" +
            "}\n";

        return prompt;
    }
}