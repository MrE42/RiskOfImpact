using UnityEngine;
using RoR2;

namespace RiskOfImpact
{
    public static class BioticShellHooks
    {
        // “12% per stack” parameter in your hyperbolic form.
        private const float k = 0.12f;

        public static void Init()
        {
            On.RoR2.HealthComponent.GetBarrierDecayRate += HealthComponent_GetBarrierDecayRate;
        }

        private static float HealthComponent_GetBarrierDecayRate(
            On.RoR2.HealthComponent.orig_GetBarrierDecayRate orig,
            HealthComponent self)
        {
            float rate = orig(self);

            var body = self?.body;
            var inv = body?.inventory;
            var itemDef = RiskOfImpactContent.GetBioticShellItemDef();

            if (inv != null && itemDef != null)
            {
                int x = inv.GetItemCount(itemDef);
                if (x > 0)
                {
                    // f(x) = kx/(kx+1) slows degradation by f(x)
                    // => decay rate *= (1 - f(x)) = 1/(kx+1)
                    float mult = 1f / (1f + k * x);
                    rate *= mult;
                }
            }

            return rate;
        }
    }
}