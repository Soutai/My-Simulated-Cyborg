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

            "## 3. 核心行为准则（大小脑协作与控制流托管）\n" +
            "- 你是大脑，负责制定持久目标（goal）和当下的即时物理原语操作。\n" +
            "- 除非出现更高优先级危险（狼极近、饱食度很低），否则要坚持当前目标。\n" +
            "- 【重要：目标动作托管机制】当你指定了一个需要移动贴近的长期目标物体（goal_target_id）时，你必须在 `goal_arrival_command` 字段中，" +
            "明确指定当肉身被小脑成功护送到该目标物体身边时，应该由小脑代为触发的“临门一脚”原子物理原语。小脑本身没有常识，完全听从你的原语托管。\n" +
            "- 【语言限制】输出 JSON 时，其中的 'goal' 字段和 'monologue' 字段的内容必须完全使用【简体中文】。\n\n" +

            "## 4. 托管示例决策参考\n" +
            "- 如果你想去捡起木棍：\n" +
            "  \"goal\": \"获取最近的武器以自卫\", \"goal_target_id\": \"Stick_1\", \"goal_arrival_command\": { \"op\": \"GRAB\", \"target_id\": \"Stick_1\" }\n" +
            "- 如果你想去吃某个水果：\n" +
            "  \"goal\": \"进食以恢复饱食度\", \"goal_target_id\": \"Fruit_3\", \"goal_arrival_command\": { \"op\": \"USE_ITEM\" }\n" +
            "- 如果你想拿着武器去打狼：\n" +
            "  \"goal\": \"驱赶附近的狼以确保安全\", \"goal_target_id\": \"Wolf_1\", \"goal_arrival_command\": { \"op\": \"USE_ITEM\" }\n" +
            "- 如果你当前只是在避险或者没有长期目标（小脑不需要导航）：\n" +
            "  \"goal\": \"无\", \"goal_target_id\": \"\", \"goal_arrival_command\": null\n\n" +

            "## 5. 绝对限制 JSON 响应格式\n" +
            "必须严格返回标准的 JSON 格式块，不要包含任何 markdown 解释。格式如下：\n" +
            "{\n" +
            "  \"monologue\": \"思考过程（中文）...\",\n" +
            "  \"primitive_commands\": [\n" +
            "    { \"op\": \"APPLY_FORCE\", \"arg_x\": 0.5, \"arg_z\": -1.2 }\n" +
            "  ],\n" +
            "  \"goal\": \"当前或新目标描述（中文）\",\n" +
            "  \"goal_target_id\": \"目标物体的 unique_id，没有则填空字符串\",\n" +
            "  \"goal_arrival_command\": {\n" +
            "    \"op\": \"到达目标时执行的操作码(GRAB/USE_ITEM/RELEASE等)，若无目标则整个对象填 null\",\n" +
            "    \"arg_x\": 0,\n" +
            "    \"arg_z\": 0,\n" +
            "    \"target_id\": \"对应操作的目标ID（如GRAB时需要）\"\n" +
            "  }\n" +
            "}";

        return prompt;
    }
}