using UnityEngine;
using RoR2;
using UnityEngine.Networking;

namespace RiskOfImpact
{
    public class LancePickupTrigger : MonoBehaviour
    {
        public EquipmentDef lanceEquipmentDef;
        public float pickupActivationTime;

        private void Start()
        {
            Collider col = GetComponent<Collider>();
            if (col) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!NetworkServer.active) return;
            if (Time.time < pickupActivationTime) return;

            CharacterBody body = other.GetComponentInParent<CharacterBody>();
            if (!body || !body.isPlayerControlled) return;

            EquipmentSlot slot = body.GetComponent<EquipmentSlot>();
            if (!slot) return;

            var eqIndex = lanceEquipmentDef.equipmentIndex;
            if (eqIndex == EquipmentIndex.None)
                eqIndex = EquipmentCatalog.FindEquipmentIndex(lanceEquipmentDef.name);

            if (eqIndex == EquipmentIndex.None) return;

            // Restore equipment + enable usage again
            if (body.inventory)
                body.inventory.SetEquipmentIndex(eqIndex, false);
            else
                slot.equipmentIndex = eqIndex;

            slot.stock = 1;

            Destroy(gameObject);
        }
    }
}