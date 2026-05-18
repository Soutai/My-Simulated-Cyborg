// DataTransferObjects.cs
// 这是一个纯粹的数据结构文件，放在项目的 Scripts/DTO 目录下

using System.Collections.Generic;

namespace EmbodiedAI.DTO
{
    [System.Serializable]
    public class PrimitiveCommand
    {
        public string op;        // APPLY_FORCE, GRAB, RELEASE, USE_ITEM
        public float arg_x;
        public float arg_z;
        public string target_id;

        // 🌟【双手新增】指定本次物理原语作用于哪只手: "Left" 或 "Right"
        // 规定：小脑移动时默认不依赖手；但 GRAB, RELEASE, USE_ITEM 必须指定手
        public string hand;
    }

    [System.Serializable]
    public class AIPhysicsDecision
    {
        public string monologue;
        public List<PrimitiveCommand> primitive_commands;
        public string goal;
        public string goal_target_id;

        // 🌟【大小脑协作升级】小脑护送肉身抵达后，指定由哪只手去执行临门一脚
        public PrimitiveCommand goal_arrival_command;
    }

    // Google API 规范需要的底层请求/响应外壳
    [System.Serializable]
    public class GeminiRequest
    {
        public RequestContent[] contents;
        [System.Serializable] public class RequestContent { public RequestPart[] parts; }
        [System.Serializable] public class RequestPart { public string text; }
    }

    [System.Serializable]
    public class GeminiResponse
    {
        public Candidate[] candidates;
        [System.Serializable] public class Candidate { public Content content; }
        [System.Serializable] public class Content { public Part[] parts; }
        [System.Serializable] public class Part { public string text; }
    }
}