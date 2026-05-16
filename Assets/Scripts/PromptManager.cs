using UnityEngine;

public class PromptManager : MonoBehaviour
{
    /// <summary>
    /// 高层主接口：聚合各类细化规则，生成最终送入大模型的完整剧本提示词
    /// </summary>
    public string GenerateNpcPrompt(float satiety, string personality, string currentTimeStr, string environmentReport, string allowedActionsJson, bool hasFood, bool hasWeapon, bool hasEnemy)
    {
        // 分类 1：细化生成【性格本能提示词】
        string personalityPrompt = GetPersonalityPrompt(personality);

        // 分类 2：细化生成【世界本能与硬性硬判法则】（饱食度反转映射适配）
        string worldRules = GetWorldRules(satiety);

        // 分类 3：细化生成【实时生存危机提示】
        string survivalHints = GetSurvivalHints(hasFood, hasWeapon, hasEnemy);

        // 核心组合：拼装最终全景 Prompt
        string promptText = $"# 角色身份设定\n" +
                            $"你是一个在蛮荒时代挣扎求生的原始人。\n" +
                            $"【当前世界时间】：{currentTimeStr}\n" + // 👈 注入时间维度！
                            $"当前你的饱食度是 {satiety:F1}（满分100，饱食度降为0表示你将彻底饿死）。\n\n" +
                            $"# 【性格本能驱使】\n" +
                            $"{personalityPrompt}\n\n" +
                            $"# 【感知层】雷达实时物理环境报告\n" +
                            $"{environmentReport}\n\n" +
                            $"# 【常识层】原始人世界法则与底层逻辑\n" +
                            $"{worldRules}\n\n" +
                            $"# 【直觉层】当前生存本能推演提示\n" +
                            $"{survivalHints}\n\n" +
                            $"# 【动作层】当前生存条件下的合法执行约束\n" +
                            $"请基于当前世界时间、你的饱食度程度和周边环境，本能地做出最有利于你长远自主生存的决策。\n" +
                            $"请严格只回复如下格式的纯 JSON 字符串，其中 action 字段**必须且只能**从当前可用的合法动作列表 {allowedActionsJson} 中选择一个，绝对不能选择列表以外的动作：\n" +
                            $"{{\"monologue\":\"符合你性格特征、当前世界时间与饱食度环境下的内心生存算盘推演\",\"action\":\"执行的动作\"}}";

        // 🟢 【色彩日志】一字不差保留原有调试打印面板
        Debug.Log($"<color=#3399FF>[2. 模块化高级提示词组装工厂 (Prompt)]</color>\n--------------------------------------------------\n{promptText}\n--------------------------------------------------");

        return promptText;
    }

    /// <summary>
    /// 细化细分 - 性格本能控制台文本映射
    /// </summary>
    private string GetPersonalityPrompt(string personality)
    {
        if (personality == "RISK_TAKER")
        {
            return "- 你的性格：你是个极端的亡命之徒，对饥饿的耐受力极低。当你极度饥饿时，狂暴的食欲会冲垮你的理智，让你忽视一切潜伏的死伤危险，优先去寻找食物！";
        }
        else
        {
            return "- 你的性格：你是个极度谨慎、恐惧死亡的懦夫。只要周围有危险生物或不确定性，无论多饿你都会把保护自己、寻找防身武器或逃跑放在第一优先级。";
        }
    }

    /// <summary>
    /// 分类 2：世界基础规则常识库（完美镜像原有 95 以上死线逻辑 -> 变为饱食度 5 以下濒死判定）
    /// </summary>
    private string GetWorldRules(float satiety)
    {
        string rules = "";

        // 完美保留并等效转换原有的极度濒死死亡判定逻辑
        if (satiety <= 5f)
        {
            rules += "- 绝对死亡判定：你当前的饱食度已经低至极其危险的危险红线 " + satiety.ToString("F1") + "！你的身体机能已经彻底崩溃，【你在当前这一秒、这个回合不立刻进食就会原地暴毙死亡】，没有任何悬念！你没有任何时间去捡武器或做多余的防守，吃下食物是你活下去唯一的希望！\n";
        }
        else
        {
            rules += "- 世界基本常识：在这个弱肉强open的荒野，空手对抗恶狼（Enemy）是极其愚蠢的行为。\n" +
                     "- 武器法则：地上的粗糙木棍（Weapon）是极佳的自卫工具，装备武器可以极大提升在危机中的生存概率。\n" +
                     "- 饱食法则：吃下食物（Food）可以立刻完全恢复你所有的饱食度，从而保证原始人不会在严寒中饿死。\n";
        }

        rules += "- 恶狼威胁：冲过去拿食物会被恶狼攻击，你有 50% 的几率重伤。\n" +
                 "- 战斗法则：空手无法战胜恶狼，如果你由于过度饥饿没有力气捡武器，直接战斗就是送死。";
        return rules;
    }

    /// <summary>
    /// 分类 3：基于当前雷达重合状态细化的动态生存直觉
    /// </summary>
    private string GetSurvivalHints(bool hasFood, bool hasWeapon, bool hasEnemy)
    {
        if (hasEnemy)
        {
            string contextHint = "【警报】周围发现了极具威胁的恶狼！";
            if (hasFood)
            {
                contextHint += "如果你赤手空拳直接越过狼去拿食物，有极大几率会被恶狼拦截并撕碎并咬死！";
            }
            if (hasWeapon)
            {
                contextHint += "强烈建议你利用本能，先拿地上的木棍作为武器来保护自己。";
            }
            return contextHint;
        }
        else
        {
            return "【安全】当前雷达侦测圈内没有发现直接物理威胁，周围环境相对安全，可以优先考虑补充体能。";
        }
    }
}