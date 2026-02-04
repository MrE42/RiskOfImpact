using RoR2;
using UnityEngine.Networking;

namespace RiskOfImpact
{
    internal static class StartItemTester
    {
        // Call this from RiskOfImpactMain.Awake()
        internal static void Init()
        {
            Run.onRunStartGlobal += OnRunStart;
        }

        private static void OnRunStart(Run run)
        {
            // Only give items on the server so it syncs correctly
            if (!NetworkServer.active) return;

            // How many copies of each item you want to start with 
            const int redshifterStacks   = 0;
            const int comboStarStacks    = 1;
            const int cheerfulMugStacks  = 0;
            const int bioticShellStacks  = 0;
            const int a = 0;
            const int b = 0;
            const int c = 0;
            const int d = 0;
            const int e = 0;

            foreach (var pcmc in PlayerCharacterMasterController.instances)
            {
                var master = pcmc?.master;
                var inv    = master?.inventory;
                if (!inv) continue;

                if (redshifterStacks > 0)
                    inv.GiveItemPermanent(RiskOfImpactContent.GetRedshifterItemDef(), redshifterStacks);

                if (comboStarStacks > 0)
                    inv.GiveItemPermanent(RiskOfImpactContent.GetComboStarItemDef(), comboStarStacks);

                if (cheerfulMugStacks > 0)
                    inv.GiveItemPermanent(RiskOfImpactContent.GetMugItemDef(), cheerfulMugStacks);
                
                if (bioticShellStacks > 0)
                    inv.GiveItemPermanent(RiskOfImpactContent.GetBioticShellItemDef(), bioticShellStacks);
                
                if (a > 0)
                    inv.GiveItemPermanent(RoR2Content.Items.RandomDamageZone, a);
                if (b > 0)
                    inv.GiveItemPermanent(DLC1Content.Items.MushroomVoid, b);
                if (c > 0)
                    inv.GiveItemPermanent(DLC2Content.Items.TriggerEnemyDebuffs, c);
                if (d > 0)
                    inv.GiveItemPermanent(DLC3Content.Items.ShockDamageAura, d);
                if (e == 1)
                {
                    if (RiskOfImpactContent.GetLanceEquipmentDef().equipmentIndex == EquipmentIndex.None)
                        RiskOfImpactContent.GetLanceEquipmentDef().equipmentIndex = EquipmentCatalog.FindEquipmentIndex(RiskOfImpactContent.GetLanceEquipmentDef().name);

                    if (RiskOfImpactContent.GetLanceEquipmentDef().equipmentIndex == EquipmentIndex.None) return;
                    
                    inv.SetEquipmentIndex(RiskOfImpactContent.GetLanceEquipmentDef().equipmentIndex);
                }
                    

            }
        }
    }
}