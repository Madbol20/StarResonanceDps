using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDpsAnalysis.Core.Data.Models
{
    /// <summary>
    /// 战斗日志
    /// </summary>
    /// <remarks>
    /// Changed from struct to class for better performance with collections.
    /// Large structs (>16 bytes) cause excessive copying when used in List/Dictionary.
    /// </remarks>
    public sealed class BattleLog
    {
        /// <summary>
        /// 包ID
        /// </summary>
        public long PacketID { get; internal set; }
        /// <summary>
        /// 时间戳 (Ticks)
        /// </summary>
        public long TimeTicks { get; internal set; }
        /// <summary>
        /// 技能ID
        /// </summary>
        public long SkillID { get; internal set; }
        /// <summary>
        /// 释放对象UUID (发出者)
        /// </summary>
        public long AttackerUuid { get; internal set; }
        /// <summary>
        /// 目标对象UUID (目标者)
        /// </summary>
        public long TargetUuid { get; internal set; }
        /// <summary>
        /// 具体数值 (伤害)
        /// </summary>
        public long Value { get; internal set; }
        /// <summary>
        /// 数值元素类型
        /// </summary>
        public int ValueElementType { get; internal set; }
        /// <summary>
        /// 伤害来源类型
        /// </summary>
        public int DamageSourceType { get; internal set; }

        /// <summary>
        /// 释放对象 (发出者) 是否为玩家
        /// </summary>
        public bool IsAttackerPlayer { get; internal set; }
        /// <summary>
        /// 目标对象 (目标者) 是否为玩家
        /// </summary>
        public bool IsTargetPlayer { get; internal set; }
        /// <summary>
        /// 具体数值是否为幸运一击
        /// </summary>
        public bool IsLucky { get; internal set; }
        /// <summary>
        /// 具体数值是否为暴击
        /// </summary>
        public bool IsCritical { get; internal set; }
        /// <summary>
        /// 具体数值是否为治疗
        /// </summary>
        public bool IsHeal { get; internal set; }
        /// <summary>
        /// 具体数值是否为闪避
        /// </summary>
        public bool IsMiss { get; internal set; }
        /// <summary>
        /// 目标对象 (目标者) 是否阵亡
        /// </summary>
        public bool IsDead { get; internal set; }
    }
}
