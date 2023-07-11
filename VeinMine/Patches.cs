using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Veinmine
{
    [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
    static class MineRockDamagePatch
    {
        static bool Prefix(MineRock __instance, HitData hit)
        {
            Player closestPlayer = Player.GetClosestPlayer(hit.m_point, 5f);

            if (VeinMinePlugin.veinMineKey.Value.IsKeyHeld())
            {
                Vector3 firstHitPoint = hit.m_point;
                foreach (var area in __instance.m_hitAreas)
                {
                    if (area == null) continue;

                    if (VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.On)
                    {
                        float radius = VeinMinePlugin.progressiveMult.Value * (float)Functions.GetSkillLevel(closestPlayer.GetSkills(), Skills.SkillType.Pickaxes);

                        if (Functions.GetDistanceFromPlayer(closestPlayer.GetTransform().position, area.bounds.center) <= radius)
                        {
                            ProcessHitArea(__instance, hit, area, firstHitPoint);
                        }
                    }
                    else
                    {
                        string hpAreaName = $"Health{__instance.GetAreaIndex(area)}";
                        hit.m_damage.m_pickaxe = __instance.m_nview.GetZDO().GetFloat(hpAreaName, __instance.m_health);
                        hit.m_point = area.bounds.center;
                        ProcessHitArea(__instance, hit, area, firstHitPoint);
                    }
                }

                return false;
            }

            return true;
        }

        private static void ProcessHitArea(MineRock __instance, HitData hit, Collider area, Vector3 firstHitPoint)
        {
            hit.m_hitCollider = area;
            if (hit.m_hitCollider == null)
            {
                VeinMinePlugin.logger.LogInfo("Minerock hit has no collider");
                return;
            }

            int areaIndex = __instance.GetAreaIndex(hit.m_hitCollider);
            if (areaIndex == -1) return;

            VeinMinePlugin.logger.LogInfo($"Hit mine rock area {areaIndex}");
            __instance.m_nview.InvokeRPC("Hit", hit, areaIndex);
            hit.m_point = firstHitPoint;
        }
    }


    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
    static class MineRock5DamagePatch
    {
        static void Prefix(MineRock5 __instance, HitData hit, out Dictionary<int, Vector3> __state)
        {
            __instance.SetupColliders();
            __state = new Dictionary<int, Vector3>();

            Player closestPlayer = Player.GetClosestPlayer(hit.m_point, 5f);
            float radius = VeinMinePlugin.progressiveMult.Value * (float)Functions.GetSkillLevel(closestPlayer.GetSkills(), Skills.SkillType.Pickaxes);

            if (VeinMinePlugin.veinMineKey.Value.IsKeyHeld())
            {
                IEnumerable<Collider> radiusColliders;

                if (VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.On)
                    radiusColliders = Physics.OverlapSphere(hit.m_point, radius);
                else
                    radiusColliders = __instance.m_hitAreas.Select(area => area.m_collider);

                foreach (var area in radiusColliders)
                {
                    int areaIndex = __instance.GetAreaIndex(area);
                    if (areaIndex >= 0)
                    {
                        __state.Add(areaIndex,
                            __instance.GetHitArea(areaIndex).m_bound.m_pos +
                            __instance.GetHitArea(areaIndex).m_collider.transform.position);
                    }
                }
            }
        }

        public static void Postfix(MineRock5 __instance, ZNetView ___m_nview, HitData hit, Dictionary<int, Vector3> __state)
        {
            Player closestPlayer = Player.GetClosestPlayer(hit.m_point, 5f);
            if (closestPlayer != null && hit.m_attacker == closestPlayer.GetZDOID() && VeinMinePlugin.veinMineKey.Value.IsKeyHeld())
            {
                var currentWeapon = closestPlayer.GetCurrentWeapon();
                if (currentWeapon.GetDamage().m_pickaxe > 0)
                {
                    foreach (var index in __state)
                    {
                        if (currentWeapon.m_durability > 0 || !currentWeapon.m_shared.m_useDurability)
                        {
                            try
                            {
                                ___m_nview.InvokeRPC("Damage", hit, index.Key);
                            }
                            catch
                            {
                                VeinMinePlugin.logger.LogInfo($"Skipping section: {index.Key}.");
                            }
                        }
                    }
                }
            }
        }
    }


    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.DamageArea))]
    static class MineRock5DamageAreaPatch
    {
        static bool Prefix(MineRock5 __instance, HitData hit, int hitAreaIndex, ref EffectList ___m_destroyedEffect, ref EffectList ___m_hitEffect, out float __state, ref bool __result)
        {
            Player closestPlayer2 = Player.GetClosestPlayer(hit.m_point, 5f);
            ItemDrop.ItemData? currentWeapon = closestPlayer2?.GetCurrentWeapon();

            if (hit == null || closestPlayer2 == null || currentWeapon == null)
            {
                __state = 0f;
                __result = false;
                return false;
            }

            if (VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.Off && currentWeapon.GetDamage().m_pickaxe > 0f) hit.m_damage.m_pickaxe = __instance.m_health;

            MineRock5.HitArea hitArea = __instance.GetHitArea(hitAreaIndex);
            if (hitArea == null)
            {
                VeinMinePlugin.logger.LogInfo($"Missing hit area {hitAreaIndex}");
                __state = 0f;
                __result = false;
                return false;
            }

            __state = hitArea.m_health;
            Vector3 hitPoint = hitArea.m_collider.bounds.center;

            if (VeinMinePlugin.enableSpreadDamage.Value == VeinMinePlugin.Toggle.On) hit = Functions.SpreadDamage(hit);

            bool isVeinmined = VeinMinePlugin.veinMineKey.Value.IsKeyHeld();
            VeinMinePlugin.logger.LogInfo($"Hit mine rock {hitAreaIndex}");

            if (hitArea == null)
            {
                VeinMinePlugin.logger.LogInfo($"Missing hit area {hitAreaIndex}");
                __result = false;
                return false;
            }

            __instance.LoadHealth();
            if (hitArea.m_health <= 0f)
            {
                VeinMinePlugin.logger.LogInfo("Already destroyed");
                __result = false;
                return false;
            }

            HitData.DamageModifier type;
            hit.ApplyResistance(__instance.m_damageModifiers, out type);
            float totalDamage = hit.GetTotalDamage();
            if (hit.m_toolTier < __instance.m_minToolTier)
            {
                DamageText.instance.ShowText(DamageText.TextType.TooHard, hit.m_point, 0f, false);
                __result = false;
                return false;
            }

            DamageText.instance.ShowText(type, hitPoint, totalDamage, false);
            if (totalDamage <= 0f)
            {
                __result = false;
                return false;
            }

            hitArea.m_health -= totalDamage;
            __instance.SaveHealth();
            if (VeinMinePlugin.removeEffects.Value == VeinMinePlugin.Toggle.Off) __instance.m_hitEffect.Create(hitPoint, Quaternion.identity, null, 1f, -1);
            Player closestPlayer = Player.GetClosestPlayer(hit.m_point, 10f);
            if (closestPlayer)
            {
                closestPlayer.AddNoise(100f);
            }

            if (hitArea.m_health <= 0f)
            {
                __instance.m_nview.InvokeRPC(ZNetView.Everybody, "SetAreaHealth", new object[]
                {
                    hitAreaIndex,
                    hitArea.m_health
                });
                if (VeinMinePlugin.removeEffects.Value == VeinMinePlugin.Toggle.Off) __instance.m_destroyedEffect.Create(hitPoint, Quaternion.identity, null, 1f, -1);
                foreach (GameObject gameObject in __instance.m_dropItems.GetDropList())
                {
                    if (isVeinmined)
                    {
                        Vector3 position = hit.m_point + UnityEngine.Random.insideUnitSphere * 0.3f;
                        //Vector3 position = closestPlayer.GetTransform().localPosition + new Vector3 { x = 0, y = 2, z = 0 } + UnityEngine.Random.insideUnitSphere * 0.3f;
                        UnityEngine.Object.Instantiate<GameObject>(gameObject, position, Quaternion.identity);
                        //hit.m_point = closestPlayer.GetTransform().localPosition + new Vector3 { x = 0, y = 2, z = 0 };
                    }
                    else if (!isVeinmined)
                    {
                        Vector3 position = hit.m_point + UnityEngine.Random.insideUnitSphere * 0.3f;
                        UnityEngine.Object.Instantiate<GameObject>(gameObject, position, Quaternion.identity);
                    }
                }

                if (__instance.AllDestroyed())
                {
                    __instance.m_nview.Destroy();
                }

                __result = true;
                return false;
            }

            __result = false;
            return false;
        }

        static void Postfix(MineRock5 __instance, HitData hit, float __state, bool __result)
        {
            Player closestPlayer = Player.GetClosestPlayer(hit.m_point, 5f);
            ItemDrop.ItemData? currentWeapon = closestPlayer?.GetCurrentWeapon();

            if (hit != null && closestPlayer != null && currentWeapon != null && VeinMinePlugin.veinMineKey.Value.IsKeyHeld() && currentWeapon.GetDamage().m_pickaxe > 0)
            {
                if (__state > 0f && hit.m_attacker == closestPlayer.GetZDOID())
                {
                    var skills = closestPlayer.GetSkills();
                    float skillIncreaseStep = Functions.GetSkillIncreaseStep(skills, Skills.SkillType.Pickaxes);

                    if (VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.Off)
                    {
                        closestPlayer.RaiseSkill(Skills.SkillType.Pickaxes, skillIncreaseStep);
                    }
                    else // VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.On
                    {
                        closestPlayer.RaiseSkill(Skills.SkillType.Pickaxes, skillIncreaseStep * VeinMinePlugin.xpMult.Value);
                    }

                    if (VeinMinePlugin.veinMineDurability.Value == VeinMinePlugin.Toggle.On && currentWeapon.m_shared.m_useDurability)
                    {
                        float durabilityLoss = VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.On
                            ? currentWeapon.m_shared.m_useDurabilityDrain * ((120 - Functions.GetSkillLevel(skills, Skills.SkillType.Pickaxes)) / (20 * VeinMinePlugin.durabilityMult.Value))
                            : currentWeapon.m_shared.m_useDurabilityDrain;

                        currentWeapon.m_durability -= durabilityLoss;
                    }
                }
            }
        }
    }
}