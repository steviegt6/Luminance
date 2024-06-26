﻿using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Luminance.Common.Utilities
{
    public static partial class Utilities
    {
        /// <summary>
        ///     Defines a given <see cref="NPC"/>'s HP based on the current difficulty mode.
        /// </summary>
        /// <param name="npc">The NPC to set the HP for.</param>
        /// <param name="normalModeHP">HP value for normal mode</param>
        /// <param name="expertModeHP">HP value for expert mode</param>
        /// <param name="masterModeHP">HP value for master mode</param>
        public static void SetLifeMaxByMode(this NPC npc, int normalModeHP, int expertModeHP, int masterModeHP)
        {
            npc.lifeMax = normalModeHP;
            if (Main.expertMode)
                npc.lifeMax = expertModeHP;
            if (Main.masterMode)
                npc.lifeMax = masterModeHP;
        }

        /// <summary>
        ///     Excludes a given <see cref="NPC"/> from the bestiary completely.
        /// </summary>
        /// <param name="npc">The NPC to apply the bestiary deletion to.</param>
        public static void ExcludeFromBestiary(this ModNPC npc)
        {
            NPCID.Sets.NPCBestiaryDrawModifiers value = new()
            {
                Hide = true
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(npc.Type, value);
        }

        /// <summary>
        ///     A simple utility that gracefully gets a <see cref="NPC"/>'s <see cref="NPC.ModNPC"/> instance as a specific type without having to do clunky casting.
        /// </summary>
        /// <remarks>
        ///     In the case of casting errors, this will create a log message that informs the user of the failed cast and fall back on a dummy instance.
        /// </remarks>
        /// <typeparam name="TNPC">The ModNPC type to convert to.</typeparam>
        /// <param name="n">The NPC to access the ModNPC from.</param>
        public static TNPC As<TNPC>(this NPC n) where TNPC : ModNPC
        {
            if (n.ModNPC is TNPC castedNPC)
                return castedNPC;

            bool vanillaNPC = n.ModNPC is null;
            Mod mod = ModContent.GetInstance<Luminance>();
            if (vanillaNPC)
                mod.Logger.Warn($"A vanilla NPC of ID {n.type} was erroneously casted to a mod NPC of type {nameof(TNPC)}.");
            else
                mod.Logger.Warn($"A NPC of type {n.ModNPC.Name} was erroneously casted to a mod NPC of type {nameof(TNPC)}.");

            return ModContent.GetInstance<TNPC>();
        }

        private static bool? BossIsActiveThisFrame;

        internal static void UpdateBossCache() => BossIsActiveThisFrame = null;

        /// <summary>
        ///     Checks if any bosses are present this frame.
        /// </summary>
        public static bool AnyBosses()
        {
            if (BossIsActiveThisFrame.HasValue)
                return BossIsActiveThisFrame.Value;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                bool isEaterOfWorlds = npc.type == NPCID.EaterofWorldsBody || npc.type == NPCID.EaterofWorldsHead || npc.type == NPCID.EaterofWorldsTail;
                if (npc.boss || isEaterOfWorlds)
                {
                    BossIsActiveThisFrame = true;
                    return BossIsActiveThisFrame.Value;
                }
            }
            
            BossIsActiveThisFrame = false;
            return BossIsActiveThisFrame.Value;
        }
    }
}
