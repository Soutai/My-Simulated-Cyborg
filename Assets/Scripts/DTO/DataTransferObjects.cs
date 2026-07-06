using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmbodiedAI.DTO
{
    // ⚠️ PlanStep / AIPhysicsDecision 的字段形状会被大模型按 JSON 原样解析（JsonUtility.FromJson）。
    // 改这里的字段（增删/改名）时，务必同步更新 PromptManager.GeneratePhysicsEnginePrompt 里
    // "## 5. 绝对限制 JSON 响应格式" 那段手写的 JSON 示例，否则大模型不会知道新字段的存在。
    //
    // 🌟 这既是大模型输出的 JSON 契约，也是执行器（CharacterActuator）直接消费的命令结构——
    // 不再额外维护一份 PrimitiveCommand 做字段对字段的手工转换。
    [Serializable]
    public class PlanStep
    {
        public string description;
        public string arrival_op;            // "APPROACH", "MOVE_DIRECTION", "APPLY_FORCE", "GRAB", "RELEASE", "USE_ITEM"
        public string hand = "";             // Left / Right （双手机制）
        public string target_id = "";
        public float arg_x = 0f;
        public float arg_z = 0f;
        public float strength = 1f;          // 用于 MOVE_DIRECTION / APPROACH 的强度 (0.5~2.0)
    }

    [Serializable]
    public class AIPhysicsDecision
    {
        public string monologue;
        public string goal = "无"; // 大脑当前认定的宏观战略目标（如：打狼、觅食）
        public List<PlanStep> plan_steps = new List<PlanStep>(); // 为该目标规划的多步原子动作序列
        public string interrupt_anchor_type = "None"; // 可选值: "Food", "Enemy", "Weapon", "None"
    }

    // ====================== Gemini API 请求/响应结构 ======================
    [Serializable]
    public class GeminiRequest
    {
        public RequestContent[] contents;

        [Serializable]
        public class RequestContent
        {
            public RequestPart[] parts;
        }

        [Serializable]
        public class RequestPart
        {
            public string text;
        }
    }

    [Serializable]
    public class GeminiResponse
    {
        public Candidate[] candidates;
        // 🌟 请求被安全策略拦截时，candidates 通常为空，原因会出现在这里而不是 candidates 里
        public PromptFeedback promptFeedback;

        [Serializable]
        public class Candidate
        {
            public Content content;
        }

        [Serializable]
        public class Content
        {
            public Part[] parts;
        }

        [Serializable]
        public class Part
        {
            public string text;
        }

        [Serializable]
        public class PromptFeedback
        {
            public string blockReason;
        }
    }
}