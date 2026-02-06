using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using BepInEx.Bootstrap;
using LookingGlass;
using LookingGlass.ItemStatsNameSpace;
using RoR2;

namespace RiskOfImpact
{
    /// <summary>
    /// LookingGlass integration for Combo Star.
    /// Shows:
    ///  - damage per Combo stack
    ///  - max Combo stacks
    ///  - damage at max stacks
    /// </summary>
    public static class LookingGlassCompat
    {
        public static void Init()
        {
            // Extra safety in case Init() is called without LG installed.
            if (!Chainloader.PluginInfos.ContainsKey("droppod.lookingglass"))
            {
                RiskOfImpactMain.LogInfo("[ComboStar LG] LookingGlass not detected, skipping compat.");
                return;
            }

            // Run after RoR2 finishes loading catalogs so ItemDefinitions exists.
            RoR2Application.onLoad += RegisterItemStatsSafe;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void RegisterItemStatsSafe()
        {
            try
            {
                RegisterComboStarItemStats();
                RegisterMugItemStats();
                RegisterRedshifterItemStats();
                RegisterBioticShellItemStats();
                RegisterRiskyDiceAfflictionItemStats();
            }
            catch (Exception e)
            {
                RiskOfImpactMain.LogError("[RiskOfImpact LG] Failed to register stats: " + e);
            }
        }
        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void RegisterMugItemStats()
        {
            ItemDef mugItem = RiskOfImpactContent.GetMugItemDef();
            if (!mugItem)
            {
                RiskOfImpactMain.LogError("[Mug LG] Cheerful Mug ItemDef missing, aborting.");
                return;
            }

            Dictionary<int, ItemStatsDef> dict = ItemDefinitions.allItemDefinitions;
            if (dict == null)
            {
                RiskOfImpactMain.LogError("[Mug LG] ItemDefinitions.allItemDefinitions is null.");
                return;
            }

            if (!dict.TryGetValue((int)mugItem.itemIndex, out ItemStatsDef def))
            {
                def = new ItemStatsDef();
                dict[(int)mugItem.itemIndex] = def;
            }

            def.descriptions.Clear();
            def.valueTypes.Clear();
            def.measurementUnits.Clear();

            // 1) Total bonus
            def.descriptions.Add("Current bonus: ");
            def.valueTypes.Add(ItemStatsDef.ValueType.Utility);
            def.measurementUnits.Add(ItemStatsDef.MeasurementUnits.Percentage);

            // 69% per stack for both move and attack speed
            def.calculateValuesNew = (float luck, int itemCount, float procChance) =>
            {
                if (itemCount <= 0)
                    return new List<float> { 0f, 0f, 0f };

                float perStackPercent = 69f;         // 69% per stack
                float totalPercent = perStackPercent * itemCount;

                return new List<float>(1)
                {
                    totalPercent   / 100f,   // total move speed
                };
            };

            RiskOfImpactMain.LogInfo("[Mug LG] Registered Cheerful Mug item stats with LookingGlass.");
        }



        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void RegisterComboStarItemStats()
        {
            ItemDef comboItem = RiskOfImpactContent.GetComboStarItemDef();
            if (!comboItem)
            {
                RiskOfImpactMain.LogError("[ComboStar LG] Combo Star ItemDef missing, aborting.");
                return;
            }

            Dictionary<int, ItemStatsDef> dict = ItemDefinitions.allItemDefinitions;
            if (dict == null)
            {
                RiskOfImpactMain.LogError("[ComboStar LG] ItemDefinitions.allItemDefinitions is null.");
                return;
            }

            if (!dict.TryGetValue((int)comboItem.itemIndex, out ItemStatsDef def))
            {
                def = new ItemStatsDef();
                dict[(int)comboItem.itemIndex] = def;
            }

            def.descriptions.Clear();
            def.valueTypes.Clear();
            def.measurementUnits.Clear();

            // 1) Damage per Combo stack (percent)
            def.descriptions.Add("Damage per stack: ");
            def.valueTypes.Add(ItemStatsDef.ValueType.Damage);
            def.measurementUnits.Add(ItemStatsDef.MeasurementUnits.Percentage);

            // 2) Max Combo stacks (buff stacks)
            def.descriptions.Add("Max stacks: ");
            def.valueTypes.Add(ItemStatsDef.ValueType.Stack);
            def.measurementUnits.Add(ItemStatsDef.MeasurementUnits.Number);

            // 3) Damage at max stacks (percent)
            def.descriptions.Add("Damage at max stacks: ");
            def.valueTypes.Add(ItemStatsDef.ValueType.Damage);
            def.measurementUnits.Add(ItemStatsDef.MeasurementUnits.Percentage);
            
            // 4) Crit at max stacks (percent)
            def.descriptions.Add("Crit at max stacks: ");
            def.valueTypes.Add(ItemStatsDef.ValueType.Damage);
            def.measurementUnits.Add(ItemStatsDef.MeasurementUnits.Percentage);

            // Damage formula:
            //   per-stack damage (%) = 3 + 0.5 * (itemCount - 1)
            //   max stacks           = BaseMaxStacks + ExtraStacksPerItem * (itemCount - 1)
            //   damage at max (%)    = perStackDamage * maxStacks
            //   crit at max (%)      = 5 + 2.5 * (itemCount - 1)
            def.calculateValuesNew = (float luck, int itemCount, float procChance) =>
            {
                if (itemCount <= 0)
                    return new List<float> { 0f, 0f, 0f };

                float perStackDamage = 3f + 0.5f * (itemCount - 1);
                float maxStacks = ComboStarHooks.BaseMaxStacks +
                                  ComboStarHooks.ExtraStacksPerItem * (itemCount - 1);
                float damageAtMax = perStackDamage * maxStacks;
                float crit = 5f + ((itemCount - 1) * 2.5f);

                return new List<float>(3)
                {
                    perStackDamage / 100f, // "Damage per stack"
                    maxStacks,      // "Max stacks"
                    damageAtMax / 100f,      // "Damage at max stacks"
                    crit / 100f     // Crit at max
                };
            };

            RiskOfImpactMain.LogInfo("[ComboStar LG] Registered Combo Star item stats with LookingGlass.");
        }
        
        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void RegisterRedshifterItemStats()
        {
            ItemDef redshifterItemDef = RiskOfImpactContent.GetRedshifterItemDef();
            if (!redshifterItemDef)
            {
                RiskOfImpactMain.LogError("[Redshifter LG] Redshifter ItemDef missing, aborting.");
                return;
            }

            Dictionary<int, ItemStatsDef> dict = ItemDefinitions.allItemDefinitions;
            if (dict == null)
            {
                RiskOfImpactMain.LogError("[Redshifter LG] ItemDefinitions.allItemDefinitions is null.");
                return;
            }

            if (!dict.TryGetValue((int)redshifterItemDef.itemIndex, out ItemStatsDef def))
            {
                def = new ItemStatsDef();
                dict[(int)redshifterItemDef.itemIndex] = def;
            }

            def.descriptions.Clear();
            def.valueTypes.Clear();
            def.measurementUnits.Clear();
            

            // Range
            def.descriptions.Add("Range Increase: ");
            def.valueTypes.Add(ItemStatsDef.ValueType.Utility);
            def.measurementUnits.Add(ItemStatsDef.MeasurementUnits.Percentage);
            
            
            def.calculateValuesNew = (float luck, int itemCount, float procChance) =>
            {
                if (itemCount <= 0)
                    return new List<float> { 0f };

                float increase = 0.5f * itemCount;

                return new List<float>(3)
                {
                    increase, // "50% per stack"
                };
            };

            RiskOfImpactMain.LogInfo("[Redshifter LG] Registered Redshifter item stats with LookingGlass.");
        }
        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void RegisterBioticShellItemStats()
        {
            ItemDef bioticShell = RiskOfImpactContent.GetBioticShellItemDef();
            if (!bioticShell)
            {
                RiskOfImpactMain.LogError("[BioticShell LG] Biotic Shell ItemDef missing, aborting.");
                return;
            }

            Dictionary<int, ItemStatsDef> dict = ItemDefinitions.allItemDefinitions;
            if (dict == null)
            {
                RiskOfImpactMain.LogError("[BioticShell LG] ItemDefinitions.allItemDefinitions is null.");
                return;
            }

            if (!dict.TryGetValue((int)bioticShell.itemIndex, out ItemStatsDef def))
            {
                def = new ItemStatsDef();
                dict[(int)bioticShell.itemIndex] = def;
            }

            def.descriptions.Clear();
            def.valueTypes.Clear();
            def.measurementUnits.Clear();

            // Show the *slowdown fraction* f(x) = 0.12x / (0.12x + 1)
            def.descriptions.Add("Barrier decay slowed by: ");
            def.valueTypes.Add(ItemStatsDef.ValueType.Utility);
            def.measurementUnits.Add(ItemStatsDef.MeasurementUnits.Percentage);

            def.calculateValuesNew = (float luck, int itemCount, float procChance) =>
            {
                if (itemCount <= 0)
                    return new List<float> { 0f };

                const float k = 0.12f;
                float x = itemCount;

                float slowFraction = (k * x) / (k * x + 1f); // hyperbolic, approaches 1

                return new List<float>(1)
                {
                    slowFraction
                };
            };

            RiskOfImpactMain.LogInfo("[BioticShell LG] Registered Biotic Shell item stats with LookingGlass.");
        }
        
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void RegisterRiskyDiceAfflictionItemStats()
        {
            ItemDef affliction = RiskOfImpactContent.GetRiskyDiceAfflictionItemDef();
            if (!affliction)
            {
                RiskOfImpactMain.LogError("[RiskyDice LG] Affliction ItemDef missing, aborting.");
                return;
            }

            Dictionary<int, ItemStatsDef> dict = ItemDefinitions.allItemDefinitions;
            if (dict == null)
            {
                RiskOfImpactMain.LogError("[RiskyDice LG] ItemDefinitions.allItemDefinitions is null.");
                return;
            }

            if (!dict.TryGetValue((int)affliction.itemIndex, out ItemStatsDef def))
            {
                def = new ItemStatsDef();
                dict[(int)affliction.itemIndex] = def;
            }

            def.descriptions.Clear();
            def.valueTypes.Clear();
            def.measurementUnits.Clear();

            // Total multiplicative stat debuff (damage/movespeed/attackspeed/regen)
            def.descriptions.Add("Debuff: ");
            def.valueTypes.Add(ItemStatsDef.ValueType.Utility);
            def.measurementUnits.Add(ItemStatsDef.MeasurementUnits.Percentage);
            
            def.descriptions.Add("Curse: ");
            def.valueTypes.Add(ItemStatsDef.ValueType.Utility);
            def.measurementUnits.Add(ItemStatsDef.MeasurementUnits.Percentage);


            def.calculateValuesNew = (float luck, int itemCount, float procChance) =>
            { 
                if (itemCount <= 0)
                    return new List<float>(2) { 0f , 0f };

                // MUST MATCH ApplyMisfortuneAffliction()
                const float strength = 0.4f;                 // relative to tonic
                float perStackStatPenalty = 0.05f * strength; // 5% * strength
                float cursePerStack = 0.1f * strength;

                float multiplier = Mathf.Pow(1f - perStackStatPenalty, itemCount);
                float debuffFraction = 1f - multiplier;
                float curseTotal = cursePerStack * itemCount;

                return new List<float>(2) { debuffFraction, curseTotal };
            };

            RiskOfImpactMain.LogInfo("[RiskyDice LG] Registered Risky Dice affliction debuff with LookingGlass.");
        }

    }
}
