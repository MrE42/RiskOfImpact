using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace RiskOfImpact
{
    public class LanceOfLonginusEquipment : MonoBehaviour
    {
        public EquipmentDef lanceEquipmentDef;
        public GameObject lanceProjectilePrefab;

        // 9000% = 90x
        public float damageCoefficient = 90f;
        public float projectileSpeed = 200f;
        public float projectileForce = 0f;

        public bool Activate(EquipmentSlot slot)
        {
            if (!slot || !slot.characterBody) return false;
            if (!NetworkServer.active) return false; // server authoritative

            var body = slot.characterBody;

            Ray aimRay = slot.GetAimRay();
            Vector3 spawnPos = body.corePosition + Vector3.up * 1.0f + aimRay.direction * 1.5f;

            // Your model points "up" by default, so rotate +90 on X like you were doing.
            Quaternion rot = Util.QuaternionSafeLookRotation(aimRay.direction) * Quaternion.Euler(90f, 0f, 0f);

            var info = new FireProjectileInfo
            {
                projectilePrefab = lanceProjectilePrefab,
                position = spawnPos,
                rotation = rot,
                owner = body.gameObject,
                damage = body.damage * damageCoefficient,
                force = projectileForce,
                crit = Util.CheckRoll(body.crit, body.master),
                speedOverride = projectileSpeed,
                damageColorIndex = DamageColorIndex.Default
            };

            ProjectileManager.instance.FireProjectile(info);
            
            slot.stock = 0;
            
            if (body.inventory)
                body.inventory.SetEquipmentIndex(EquipmentIndex.None);
            else
                slot.equipmentIndex = EquipmentIndex.None;

            body.AddBuff(DLC2Content.Buffs.SoulCost);
            body.AddBuff(DLC2Content.Buffs.SoulCost);

            return true;
        }
    }
}