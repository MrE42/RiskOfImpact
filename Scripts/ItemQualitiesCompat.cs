using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using R2API;
using RoR2;
using RoR2.ExpansionManagement;
using UnityEngine;

using ItemQualities;

namespace RiskOfImpact
{
    internal static class ItemQualitiesCompat
    {
        private const string QualityPluginGuid = "com.Gorakh.ItemQualities";

        private static bool _qualityInstalled;

        // Base + variants
        private static ItemDef _baseMug;
        internal static ItemDef Mug_Uncommon;
        internal static ItemDef Mug_Rare;
        internal static ItemDef Mug_Epic;
        internal static ItemDef Mug_Legendary;

        // Our injected group instance
        private static ItemQualityGroup _mugQualityGroup;

        internal static void Init()
        {
            _qualityInstalled = Chainloader.PluginInfos.ContainsKey(QualityPluginGuid);
            if (!_qualityInstalled)
                return;

            // Delay the catalog rebuild injection until RoR2 is loaded
            RoR2Application.onLoad += OnRoR2Loaded;

            RiskOfImpactMain.LogInfo("[ItemQualitiesCompat] ItemQualities detected. Mug quality variants will be registered.");
        }

        /// <summary>
        /// Call this during your item registration (right after you create the base Mug ItemDef),
        /// before/after ItemAPI.Add(baseMug). This clones + registers the quality variants.
        /// </summary>
        internal static void RegisterMugVariantsIfNeeded(ItemDef baseMug, ItemDisplayRuleDict mugDisplayRules, ExpansionDef requiredExpansion)
        {
            if (!_qualityInstalled)
                return;

            if (!baseMug)
            {
                RiskOfImpactMain.LogWarning("[ItemQualitiesCompat] RegisterMugVariantsIfNeeded called with null baseMug.");
                return;
            }

            // Prevent double registration
            if (_baseMug)
                return;

            _baseMug = baseMug;

            // IMPORTANT:
            // These tokens should be your own tokens (or reuse base tokens with suffixes).
            // I’m using placeholders that match your existing pattern.
            Mug_Uncommon = CloneMugVariant(baseMug, "Uncommon", "DD_NAME_QUALITY_UNCOMMON", "DD_PICKUP_QUALITY_UNCOMMON", "DD_DESC_QUALITY_UNCOMMON", requiredExpansion);
            Mug_Rare     = CloneMugVariant(baseMug, "Rare",     "DD_NAME_QUALITY_RARE",     "DD_PICKUP_QUALITY_RARE",     "DD_DESC_QUALITY_RARE",     requiredExpansion);
            Mug_Epic     = CloneMugVariant(baseMug, "Epic",     "DD_NAME_QUALITY_EPIC",     "DD_PICKUP_QUALITY_EPIC",     "DD_DESC_QUALITY_EPIC",     requiredExpansion);
            Mug_Legendary= CloneMugVariant(baseMug, "Legendary","DD_NAME_QUALITY_LEGENDARY","DD_PICKUP_QUALITY_LEGENDARY","DD_DESC_QUALITY_LEGENDARY",requiredExpansion);

            // Register variants with ItemAPI so they exist as real items
            ItemAPI.Add(new CustomItem(Mug_Uncommon, mugDisplayRules));
            ItemAPI.Add(new CustomItem(Mug_Rare, mugDisplayRules));
            ItemAPI.Add(new CustomItem(Mug_Epic, mugDisplayRules));
            ItemAPI.Add(new CustomItem(Mug_Legendary, mugDisplayRules));

            RiskOfImpactMain.LogInfo("[ItemQualitiesCompat] Registered Mug quality variants.");
        }

        internal static int GetMugTotalCount(Inventory inv)
        {
            if (!inv || !_baseMug)
                return 0;

            int total = inv.GetItemCount(_baseMug);

            if (_qualityInstalled)
            {
                if (Mug_Uncommon)  total += inv.GetItemCount(Mug_Uncommon);
                if (Mug_Rare)      total += inv.GetItemCount(Mug_Rare);
                if (Mug_Epic)      total += inv.GetItemCount(Mug_Epic);
                if (Mug_Legendary) total += inv.GetItemCount(Mug_Legendary);
            }

            return total;
        }

        private static ItemDef CloneMugVariant(ItemDef baseMug, string suffix, string nameToken, string pickupToken, string descToken, ExpansionDef requiredExpansion)
        {
            ItemDef clone = ScriptableObject.Instantiate(baseMug);
            clone.name = $"{baseMug.name}_Quality_{suffix}";
            clone.nameToken = nameToken;
            clone.pickupToken = pickupToken;
            clone.descriptionToken = descToken;

            // Keep same tier/tags; still “the Mug”, just a different item index for Quality.
            clone.requiredExpansion = requiredExpansion ? requiredExpansion : baseMug.requiredExpansion;

            return clone;
        }

        private static void OnRoR2Loaded()
        {
            if (!_qualityInstalled)
                return;

            // Only inject if we successfully registered variants
            if (!_baseMug || !Mug_Uncommon || !Mug_Rare || !Mug_Epic || !Mug_Legendary)
            {
                RiskOfImpactMain.LogWarning("[ItemQualitiesCompat] Mug or variants missing; skipping Quality group injection.");
                return;
            }

            RoR2Application.instance.StartCoroutine(InjectMugQualityGroupAndRebuildCatalog());
        }

        private static IEnumerator InjectMugQualityGroupAndRebuildCatalog()
        {
            // extra safety: wait one frame
            yield return null;

            // Build ItemQualityGroup asset instance
            _mugQualityGroup = ScriptableObject.CreateInstance<ItemQualityGroup>();
            _mugQualityGroup.name = "igRiskOfImpact_CheerfulMug";

            // Set private serialized fields on ItemQualityGroup
            SetInstanceField(typeof(ItemQualityGroup), _mugQualityGroup, "_uncommonItem", Mug_Uncommon);
            SetInstanceField(typeof(ItemQualityGroup), _mugQualityGroup, "_rareItem", Mug_Rare);
            SetInstanceField(typeof(ItemQualityGroup), _mugQualityGroup, "_epicItem", Mug_Epic);
            SetInstanceField(typeof(ItemQualityGroup), _mugQualityGroup, "_legendaryItem", Mug_Legendary);

            // Read internal static fields from ItemQualitiesContent (AllQualityTiers / AllGroups / etc.)
            Type qtContainerType = typeof(ItemQualitiesContent.QualityTiers);
            Type igContainerType = typeof(ItemQualitiesContent.ItemQualityGroups);
            Type egContainerType = typeof(ItemQualitiesContent.EquipmentQualityGroups);
            Type bgContainerType = typeof(ItemQualitiesContent.BuffQualityGroups);

            object allQualityTiers = GetStaticField(qtContainerType, "AllQualityTiers");
            object allItemGroups   = GetStaticField(igContainerType, "AllGroups");
            object allEquipGroups  = GetStaticField(egContainerType, "AllGroups");
            object allBuffGroups   = GetStaticField(bgContainerType, "AllGroups");

            if (allQualityTiers == null || allItemGroups == null || allEquipGroups == null || allBuffGroups == null)
            {
                RiskOfImpactMain.LogWarning("[ItemQualitiesCompat] Failed reading ItemQualitiesContent internal AllGroups fields.");
                yield break;
            }

            // Merge ItemQualityGroups list with our new group
            var mergedItemGroups = new List<ItemQualityGroup>();
            foreach (var g in (IEnumerable)allItemGroups)
                mergedItemGroups.Add((ItemQualityGroup)g);
            mergedItemGroups.Add(_mugQualityGroup);

            // Invoke private: QualityCatalog.SetQualityGroups(...)
            MethodInfo setQualityGroups = typeof(QualityCatalog).GetMethod("SetQualityGroups", BindingFlags.NonPublic | BindingFlags.Static);
            if (setQualityGroups == null)
            {
                RiskOfImpactMain.LogWarning("[ItemQualitiesCompat] Could not find QualityCatalog.SetQualityGroups (private).");
                yield break;
            }

            IEnumerator rebuildEnum = (IEnumerator)setQualityGroups.Invoke(null, new object[]
            {
                allQualityTiers,
                mergedItemGroups,  // List<T> implements IReadOnlyCollection<T>
                allEquipGroups,
                allBuffGroups
            });

            while (rebuildEnum.MoveNext())
                yield return rebuildEnum.Current;

            // Patch base item mapping (Quality relies on BaseItemReference for addressables; our item isn't addressable)
            PatchBaseItemMapping();

            RiskOfImpactMain.LogInfo("[ItemQualitiesCompat] Injected Mug ItemQualityGroup and rebuilt QualityCatalog.");
        }

        private static void PatchBaseItemMapping()
        {
            if (!_mugQualityGroup || !_baseMug)
                return;

            // public ItemIndex BaseItemIndex
            FieldInfo baseItemIndexField = typeof(ItemQualityGroup).GetField("BaseItemIndex", BindingFlags.Public | BindingFlags.Instance);
            baseItemIndexField?.SetValue(_mugQualityGroup, _baseMug.itemIndex);

            // public ItemQualityGroupIndex GroupIndex (assigned during rebuild)
            FieldInfo groupIndexField = typeof(ItemQualityGroup).GetField("GroupIndex", BindingFlags.Public | BindingFlags.Instance);
            object groupIndexValue = groupIndexField?.GetValue(_mugQualityGroup);
            if (groupIndexValue == null)
            {
                RiskOfImpactMain.LogWarning("[ItemQualitiesCompat] GroupIndex was null after rebuild; cannot patch base item mapping.");
                return;
            }

            // private static ItemQualityGroupIndex[] _itemIndexToQualityGroupIndex
            FieldInfo mapGroupIdxField = typeof(QualityCatalog).GetField("_itemIndexToQualityGroupIndex", BindingFlags.NonPublic | BindingFlags.Static);
            Array groupMapArray = mapGroupIdxField?.GetValue(null) as Array;

            // private static QualityTier[] _itemIndexToQuality
            FieldInfo mapQualityField = typeof(QualityCatalog).GetField("_itemIndexToQuality", BindingFlags.NonPublic | BindingFlags.Static);
            Array qualityMapArray = mapQualityField?.GetValue(null) as Array;

            if (groupMapArray == null || qualityMapArray == null)
            {
                RiskOfImpactMain.LogWarning("[ItemQualitiesCompat] Could not access QualityCatalog internal mapping arrays.");
                return;
            }

            int baseIdx = (int)_baseMug.itemIndex;
            if (baseIdx < 0 || baseIdx >= groupMapArray.Length || baseIdx >= qualityMapArray.Length)
            {
                RiskOfImpactMain.LogWarning("[ItemQualitiesCompat] Base mug item index out of range for mapping arrays.");
                return;
            }

            groupMapArray.SetValue(groupIndexValue, baseIdx);
            qualityMapArray.SetValue(QualityTier.None, baseIdx);
        }

        private static void SetInstanceField(Type type, object instance, string fieldName, object value)
        {
            FieldInfo f = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null)
            {
                RiskOfImpactMain.LogWarning($"[ItemQualitiesCompat] Missing field {type.FullName}.{fieldName}");
                return;
            }
            f.SetValue(instance, value);
        }

        private static object GetStaticField(Type type, string fieldName)
        {
            FieldInfo f = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            return f?.GetValue(null);
        }
    }
}
