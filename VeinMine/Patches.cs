using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Veinmine
{
    [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
    static class MineRockDamagePatch
    {
        static bool Prefix(MineRock __instance, HitData hit)
        {
            if (VeinMinePlugin.veinMineKey.Value.IsKeyHeld())
            {
                if (VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.On)
                {
                    float radius = VeinMinePlugin.progressiveMult.Value * (float)Functions.GetSkillLevel(Player.GetClosestPlayer(hit.m_point, 5f).GetSkills(), Skills.SkillType.Pickaxes);
                    Vector3 firstHitPoint = hit.m_point;

                    foreach (var area in __instance.m_hitAreas)
                    {
                        if (Functions.GetDistanceFromPlayer(Player.GetClosestPlayer(hit.m_point, 5f).GetTransform().position, area.bounds.center) <= radius && area != null)
                        {
                            hit.m_point = area.transform.position;
                            hit.m_hitCollider = area;
                            if (hit.m_hitCollider == null)
                            {
                                ZLog.Log("Minerock hit has no collider");
                                return false;
                            }

                            int areaIndex = __instance.GetAreaIndex(hit.m_hitCollider);
                            if (areaIndex == -1)
                            {
                                //ZLog.Log("Invalid hit area on " + base.gameObject.name);
                                return false;
                            }

                            ZLog.Log("Hit mine rock area " + areaIndex);
                            __instance.m_nview.InvokeRPC("Hit", new object[]
                            {
                                hit,
                                areaIndex
                            });
                            hit.m_point = firstHitPoint;
                        }
                    }
                }
                else
                {
                    foreach (var area in __instance.m_hitAreas)
                    {
                        string hpAreaName = "Health" + __instance.GetAreaIndex(area).ToString();
                        hit.m_damage.m_pickaxe = __instance.m_nview.GetZDO().GetFloat(hpAreaName, __instance.m_health);
                        hit.m_point = __instance.GetHitArea(__instance.GetAreaIndex(area)).bounds.center;
                        hit.m_hitCollider = area;
                        if (hit.m_hitCollider == null)
                        {
                            ZLog.Log("Minerock hit has no collider");
                            return false;
                        }

                        int areaIndex = __instance.GetAreaIndex(hit.m_hitCollider);
                        if (areaIndex == -1)
                        {
                            //ZLog.Log("Invalid hit area on " + base.gameObject.name);
                            return false;
                        }

                        ZLog.Log("Hit mine rock area " + areaIndex);
                        __instance.m_nview.InvokeRPC("Hit", new object[]
                        {
                            hit,
                            areaIndex
                        });
                    }
                }

                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
    static class MineRock5DamagePatch
    {
        static void Prefix(MineRock5 __instance, HitData hit, out Dictionary<int, Vector3> __state)
        {
            __instance.SetupColliders();
            __state = new Dictionary<int, Vector3>();

            if (VeinMinePlugin.veinMineKey.Value.IsKeyHeld() && VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.On)
            {
                var radiusColliders = Physics.OverlapSphere(hit.m_point, VeinMinePlugin.progressiveMult.Value * (float)Functions.GetSkillLevel(Player.GetClosestPlayer(hit.m_point, 5f).GetSkills(), Skills.SkillType.Pickaxes));

                if (radiusColliders != null)
                {
                    foreach (var area in radiusColliders)
                    {
                        if (__instance.GetAreaIndex(area) >= 0)
                        {
                            __state.Add(__instance.GetAreaIndex(area), __instance.GetHitArea(__instance.GetAreaIndex(area)).m_bound.m_pos +
                                                                       __instance.GetHitArea(__instance.GetAreaIndex(area)).m_collider.transform.position);
                        }
                    }
                }
            }

            if (VeinMinePlugin.veinMineKey.Value.IsKeyHeld() && VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.Off)
            {
                List<Collider> radiusColliders = new List<Collider>();
                foreach (var area in __instance.m_hitAreas)
                {
                    radiusColliders.Add(area.m_collider);
                }

                if (radiusColliders != null)
                {
                    foreach (var area in radiusColliders)
                    {
                        if (__instance.GetAreaIndex(area) >= 0)
                        {
                            __state.Add(__instance.GetAreaIndex(area), __instance.GetHitArea(__instance.GetAreaIndex(area)).m_bound.m_pos +
                                                                       __instance.GetHitArea(__instance.GetAreaIndex(area)).m_collider.transform.position);
                        }
                    }
                }
            }
        }

        public static void Postfix(MineRock5 __instance, ZNetView ___m_nview, List<HitArea> ___m_hitAreas, HitData hit, Dictionary<int, Vector3> __state)
        {
            if (Player.GetClosestPlayer(hit.m_point, 5f) != null && hit.m_attacker == Player.GetClosestPlayer(hit.m_point, 5f).GetZDOID())
            {
                if (VeinMinePlugin.veinMineKey.Value.IsKeyHeld() && Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().GetDamage().m_pickaxe > 0)
                {
                    foreach (var index in __state)
                    {
                        if (Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().m_durability > 0 || !Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().m_shared.m_useDurability)
                        {
                            try
                            {
                                ___m_nview.InvokeRPC("Damage", new object[]
                                {
                                    hit,
                                    index.Key
                                });
                            }
                            catch
                            {
                                VeinMinePlugin.logger.LogInfo("Skipping section: " + index.Key + ".");
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
            if (
                hit == null
                || Player.GetClosestPlayer(hit.m_point, 5f) == null
                || Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon() == null
            )
            {
                __result = false;
                __state = 0f;
                return false;
            }

            if (VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.Off && Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().GetDamage().m_pickaxe > 0f) hit.m_damage.m_pickaxe = __instance.m_health;
            bool isVeinmined = false;
            MineRock5.HitArea hitArea = __instance.GetHitArea(hitAreaIndex);
            __state = hitArea.m_health;
            Vector3 hitPoint = hitArea.m_collider.bounds.center;

            if (VeinMinePlugin.enableSpreadDamage.Value == VeinMinePlugin.Toggle.On) hit = Functions.SpreadDamage(hit);
            if (VeinMinePlugin.veinMineKey.Value.IsKeyHeld()) isVeinmined = true;

            ZLog.Log("hit mine rock " + hitAreaIndex);
            if (hitArea == null)
            {
                ZLog.Log("Missing hit area " + hitAreaIndex);
                __result = false;
                return false;
            }

            __instance.LoadHealth();
            if (hitArea.m_health <= 0f)
            {
                ZLog.Log("Already destroyed");
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
            if (
                hit != null
                && Player.GetClosestPlayer(hit.m_point, 5f) != null
                && Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon() != null
            )
            {
                if (VeinMinePlugin.veinMineKey.Value.IsKeyHeld() && Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().GetDamage().m_pickaxe > 0)
                {
                    if (__state > 0f && hit.m_attacker == Player.GetClosestPlayer(hit.m_point, 5f).GetZDOID() && VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.Off)
                    {
                        Player.GetClosestPlayer(hit.m_point, 5f).RaiseSkill(Skills.SkillType.Pickaxes, Functions.GetSkillIncreaseStep(Player.GetClosestPlayer(hit.m_point, 5f).GetSkills(), Skills.SkillType.Pickaxes));

                        if (VeinMinePlugin.veinMineDurability.Value == VeinMinePlugin.Toggle.On && Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().m_shared.m_useDurability)
                        {
                            Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().m_durability -= Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().m_shared.m_useDurabilityDrain;
                        }
                    }
                    else if (__state > 0f && hit.m_attacker == Player.GetClosestPlayer(hit.m_point, 5f).GetZDOID() && VeinMinePlugin.progressiveMode.Value == VeinMinePlugin.Toggle.On)
                    {
                        Player.GetClosestPlayer(hit.m_point, 5f).RaiseSkill(Skills.SkillType.Pickaxes, Functions.GetSkillIncreaseStep(Player.GetClosestPlayer(hit.m_point, 5f).GetSkills(), Skills.SkillType.Pickaxes) * VeinMinePlugin.xpMult.Value);

                        if (VeinMinePlugin.veinMineDurability.Value == VeinMinePlugin.Toggle.On && Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().m_shared.m_useDurability)
                        {
                            float durabilityLoss = Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().m_shared.m_useDurabilityDrain * ((120 - Functions.GetSkillLevel(Player.GetClosestPlayer(hit.m_point, 5f).GetSkills(), Skills.SkillType.Pickaxes)) / (20 * VeinMinePlugin.durabilityMult.Value));
                            Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().m_durability -= durabilityLoss;
                        }
                    }
                }
            }
        }
    }
}