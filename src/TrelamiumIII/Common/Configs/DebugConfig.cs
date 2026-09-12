using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader.Config;

namespace TrelamiumIII.Common.Configs
{
    internal class DebugConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;
        internal static DebugConfig Instance => ModContent.GetInstance<DebugConfig>();

        [Header("DebugMode")]
        [DefaultValue(false)]
        public bool DebugMode { get; set; } = false;

    }
}
