using UnityEngine;

public class PromptManager : MonoBehaviour
{
    /// <summary>
    /// 动态生成发送给大脑（LLM）的具身智能语义Prompt
    /// </summary>
    public string GeneratePhysicsEnginePrompt(
        float satiety,
        NpcPersonality personality,
        string currentTimeStr,
        string serializedRadarJson,
        string serializedMemoryJson,
        string leftHandItem,
        string rightHandItem,
        string currentGoal = "无")
    {
        string leftItemStr = string.IsNullOrEmpty(leftHandItem) ? "空无一物" : leftHandItem;
        string rightItemStr = string.IsNullOrEmpty(rightHandItem) ? "空无一物" : rightHandItem;

        // 🌟 核心机制语义化：根据性格动态注入底层的“因果物理规则”与“本能权重”
        // 规则文本和数值阈值都集中在 PersonalityProtocolConfig，这里只管拼装
        string personalityRules = PersonalityProtocolConfig.GetRuleText(personality);

        string prompt =
            $"# 具身智能物理沙盒任务控制台\n" +
            $"系统时间: {currentTimeStr} | 饱食度: {satiety:F1}/100 | 性格本能: {personality}\n\n" +
            $"🌟 左手持物: {leftItemStr}\n" +
            $"🌟 右手持物: {rightItemStr}\n" +
            // 🌟【核心修改】：强行让大模型意识到它已经安排过这个任务了！
            $"🎯 身体当前正在推进执行的宏观战略 (Goal): {currentGoal}\n" +
            $"⚠️ 指挥官核心因果链原则：\n" +
            $"- 当你看到当前主要目标不是‘无’时，说明你的身体正在完美执行你上一轮下达的命令。\n" +
            $"- 你千万不要由于失忆，再次下达相同意图的初始动作（比如身体已经拿着木棍走向狼，你不要再下达‘去捡木棍’）！\n" +
            $"- 你应当保持时空连续性，在返回的 `plan_steps` 中，无缝规划在此战略【完成之后】或者【中途需要突变调整】时的下一步任务序列！\n\n" +

            "## 1. 原子物理原语（必须严格遵守）\n" +
            "- APPLY_FORCE: 必须提供 arg_x 和 arg_z！（arg_x >0=右, <0=左 | arg_z >0=前, <0=后，建议力度 2.0~4.0）\n" +
            "- APPROACH: 靠近某个物体（推荐使用，AI只需给出 target_id）\n" +
            "- MOVE_DIRECTION: 朝指定方向移动一段距离（需 arg_x、arg_z、strength）\n" +
            "- GRAB / RELEASE / USE_ITEM: 必须包含 \"hand\" 字段（\"Left\" 或 \"Right\"）！\n" +
            "- GRAB: 除了 hand，还必须提供 \"target_id\"，明确指出要抓取的具体物体，不要因为觉得目标\"很明显\"就省略！\n" +
            "- EXPLORE: 交给身体自主漫步探索（会自己避开障碍物、尽量不重复走过的地方），不需要提供任何参数。" +
            "见下文第4节，没有明确目标时用这个，不要自己规划移动坐标。\n\n" +

            "## 2. 当前感知\n" +
            $"{serializedRadarJson}\n\n" +

            "## 2.5 近期记忆（不在当前视野内，凭记忆推测的位置，仅供参考）\n" +
            "- 这些是你之前亲眼见过、但现在已经不在视野范围/角度内的物体，位置是根据你现在的实际坐标重新换算过的。\n" +
            "- confidence_note 会提示这份记忆有多新鲜；食物/武器这类不会自己移动的物体，记忆通常仍然准确；\n" +
            "  敌人这类会动的物体，记忆越旧就越可能已经不在原地了，请自行判断要不要依赖这份信息去行动。\n" +
            $"{serializedMemoryJson}\n\n" +

            "## 3. 核心行为与性格本能准则\n" +
            "- 你是大脑，只负责输出原子原语和行动计划。小脑只会忠实按顺序执行你下发的原子原语。\n" +
            "- 移动必须通过物理方式实现（APPROACH / MOVE_DIRECTION / APPLY_FORCE）。\n" +
            "- 抓取和使用物品依赖实际接近（由执行器处理）。\n" +
            "- 🌟双手独立：可以右手持武器，左手做其他事。\n" +
            "- 🌟两段式进食：必须先 GRAB 食物到手中，再 USE_ITEM 吃掉。\n" +
            personalityRules + "\n\n" + // 🌟 注入非干预性的性格底层约束规则

            "## 4. 多步规划能力（核心机制）\n" +
            "- 你拥有优秀的战略规划能力。当你有明确的具体目标时（比如去拿某个东西、吃东西、攻击某个敌人），" +
            "**请制定包含至少2个步骤的连贯行动计划**（plan_steps），让小脑可以连续执行，不要一次只规划一步。\n" +
            "- 你需要根据你的常识判断需要多少个步骤，把一个大目标分解成需要的步数。\n" +
            "- 明确目标优秀示例：\n" +
            "  - APPROACH Stick → GRAB（Right） → APPROACH Wolf → USE_ITEM（Right）\n" +
            "  - MOVE_DIRECTION（向前） → APPROACH Fruit → GRAB（Left） → USE_ITEM（Left）\n" +
            "  - APPLY_FORCE 微调位置 → GRAB\n" +
            "- plan_steps 中每一项必须包含 description、arrival_op、hand（target_id / arg_x / arg_z / strength 根据 op 类型提供）。\n\n" +
            "- **【没有明确目标时——探索/巡逻/找东西】**：不要自己规划移动坐标去模拟"
                + "\"随便走走\"，具体路线交给身体自主处理。`plan_steps` 只需要包含【唯一一个】步骤：" +
            "`{ \"description\": \"...\", \"arrival_op\": \"EXPLORE\" }`。你的身体会自己找路、避开障碍物、" +
            "尽量不重复走过的地方，不需要也不应该给出任何移动坐标参数。\n" +
            "- 【战略惊醒锚点】：只要 plan_steps 用的是 EXPLORE（没有明确目标、正在探索/巡逻/找东西），" +
            "就必须在 JSON 的 `interrupt_anchor_type` 字段中填入你这次探索的核心欲望目标类型" +
            "（可选值：'Food'、'Enemy'、'Weapon'）。一旦身体在漫步途中雷达首次扫到该类型，" +
            "小脑会立刻掐断漫步、强制交还控制权给你重新决策。如果是去抓取眼前特定物品等短程确定性连招（用的是 APPROACH/GRAB 等具体原语），" +
            "请务必将其设为 'None'。\n" +

            "## 5. 绝对限制 JSON 响应格式\n" +
            "必须严格返回标准的 JSON 格式块，不要包含任何 markdown 解释。\n" +
            "格式如下：\n" +
            "{\n" +
            "  \"monologue\": \"思考过程（中文）...\",\n" +
            "  \"goal\": \"短期目标描述\",\n" +
            "  \"interrupt_anchor_type\": \"Food\",\n" + 
            "  \"plan_steps\": [\n" +
            "    { \"description\": \"靠近木棍\", \"arrival_op\": \"APPROACH\", \"target_id\": \"Stick\", \"strength\": 1.0 },\n" +
            "    { \"description\": \"用右手抓取木棍\", \"arrival_op\": \"GRAB\", \"hand\": \"Right\", \"target_id\": \"Stick\" },\n" +
            "    { \"description\": \"向狼的方向移动\", \"arrival_op\": \"MOVE_DIRECTION\", \"arg_x\": -2.5, \"arg_z\": 1.0, \"strength\": 1.2 }\n" +
            "  ]\n" +
            "}";

        return prompt;
    }
}