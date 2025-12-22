using RoR2;
using UnityEngine;

namespace RiskOfImpact
{
    public static class LanceOfLonginusEquipmentHook
    {
        public static void Init()
        {
            Debug.Log("[LanceHook] Initializing hook...");
            On.RoR2.EquipmentSlot.PerformEquipmentAction += EquipmentSlot_PerformEquipmentAction;
            Debug.Log("[LanceHook] Hook added.");
        }

        // Modern signature: (orig, self, EquipmentDef equipmentDef)
        private static bool EquipmentSlot_PerformEquipmentAction(
            On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig,
            EquipmentSlot self,
            EquipmentDef equipmentDef)
        {
            var lanceDef = RiskOfImpactContent.GetLanceEquipmentDef();
            if (equipmentDef == lanceDef)
            {
                Debug.Log("[LanceHook] Detected Lance activation on " + self.characterBody.name);
                var lanceBehavior = self.GetComponent<LanceOfLonginusEquipment>();
                if (lanceBehavior == null)
                {
                    Debug.Log("[LanceHook] Lance behavior not found; adding component.");
                    lanceBehavior = self.gameObject.AddComponent<LanceOfLonginusEquipment>();
                    lanceBehavior.lanceEquipmentDef = lanceDef;
                    lanceBehavior.lanceProjectilePrefab = RiskOfImpactContent.GetLanceProjectilePrefab();
                }
                bool result = lanceBehavior.Activate(self);
                Debug.Log("[LanceHook] Activation result: " + result);
                return false;
            }
            return orig(self, equipmentDef);
        }
    }
}