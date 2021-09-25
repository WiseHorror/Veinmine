using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WiseHorror.Veinmine
{
    [HarmonyPatch]
    class Patches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MineRock), "Damage")]
        public static bool MineRock_Damage_Prefix(MineRock __instance, HitData hit)
        {
            if (Input.GetKey(VeinMine.veinMineKey.Value))
            {
                if (VeinMine.progressiveMode.Value)
                {
                    float radius = VeinMine.progressiveMult.Value * (float)GetSkillLevel(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes);
                    Vector3 firstHitPoint = hit.m_point;

                    foreach (var area in __instance.m_hitAreas)
                    {
                        if (GetDistanceFromPlayer(Player.GetClosestPlayer(hit.m_point, 5f).GetTransform().position, area.bounds.center) <= radius && area != null)
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MineRock5), "Damage")]
        public static void MineRock5_Damage_Prefix(MineRock5 __instance, HitData hit, out Dictionary<int, Vector3> __state)
        {
            __instance.SetupColliders();
            __state = new Dictionary<int, Vector3>();

            if (Input.GetKey(VeinMine.veinMineKey.Value) && VeinMine.progressiveMode.Value)
            {
                var radiusColliders = Physics.OverlapSphere(hit.m_point, VeinMine.progressiveMult.Value * (float)GetSkillLevel(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes));

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
            if (Input.GetKey(VeinMine.veinMineKey.Value) && !VeinMine.progressiveMode.Value)
            {
                List<Collider> radiusColliders = new List<Collider>();
                foreach(var area in __instance.m_hitAreas)
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MineRock5), "Damage")]
        public static void MineRock5_Damage_Patch(MineRock5 __instance, ZNetView ___m_nview, List<HitArea> ___m_hitAreas, HitData hit, Dictionary<int, Vector3> __state)
        {
            if (Player.m_localPlayer != null && hit.m_attacker == Player.m_localPlayer.GetZDOID())
            {
                if (Input.GetKey(VeinMine.veinMineKey.Value) && Player.m_localPlayer.GetCurrentWeapon().GetDamage().m_pickaxe > 0)
                {
                    foreach (var index in __state)
                    {
                        //hit.m_point = index.Value;
                        if (Player.m_localPlayer.GetCurrentWeapon().m_durability > 0 || !Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
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
                                VeinMine.logger.LogInfo("Skipping section: " + index.Key + ".");
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MineRock5), "DamageArea")]
        public static bool MineRock5_DamageArea_Prefix(MineRock5 __instance, HitData hit, int hitAreaIndex, ref EffectList ___m_destroyedEffect, ref EffectList ___m_hitEffect, out float __state, ref bool __result)
        {
            if (!VeinMine.progressiveMode.Value && Player.GetClosestPlayer(hit.m_point, 10f).GetCurrentWeapon().GetDamage().m_pickaxe > 0f) hit.m_damage.m_pickaxe = __instance.m_health;
            bool isVeinmined = false;
            MineRock5.HitArea hitArea = __instance.GetHitArea(hitAreaIndex);
            __state = hitArea.m_health;

            if (VeinMine.enableSpreadDamage.Value) hit = SpreadDamage(hit);
            if (Input.GetKey(VeinMine.veinMineKey.Value)) isVeinmined = true;

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
            DamageText.instance.ShowText(type, hit.m_point, totalDamage, false);
            if (totalDamage <= 0f)
            {
                __result = false;
                return false;
            }
            hitArea.m_health -= totalDamage;
            __instance.SaveHealth();
            if (!VeinMine.removeEffects.Value) __instance.m_hitEffect.Create(hit.m_point, Quaternion.identity, null, 1f, -1);
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
                if (!VeinMine.removeEffects.Value) __instance.m_destroyedEffect.Create(hit.m_point, Quaternion.identity, null, 1f, -1);
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MineRock5), "DamageArea")]
        public static void MineRock5_DamageArea_Patch(MineRock5 __instance, HitData hit, float __state, bool __result)
        {
            if (Input.GetKey(VeinMine.veinMineKey.Value) && Player.m_localPlayer.GetCurrentWeapon().GetDamage().m_pickaxe > 0)
            {
                if (__state > 0f && hit.m_attacker == Player.m_localPlayer.GetZDOID() && !VeinMine.progressiveMode.Value)
                {
                    Player.m_localPlayer.RaiseSkill(Skills.SkillType.Pickaxes, GetSkillIncreaseStep(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes));

                    if (VeinMine.veinMineDurability.Value && Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
                    {
                        Player.m_localPlayer.GetCurrentWeapon().m_durability -= Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurabilityDrain;
                    }

                }
                else if (__state > 0f && hit.m_attacker == Player.m_localPlayer.GetZDOID() && VeinMine.progressiveMode.Value)
                {
                    Player.m_localPlayer.RaiseSkill(Skills.SkillType.Pickaxes, GetSkillIncreaseStep(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes) * VeinMine.xpMult.Value);

                    if (Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
                    {
                        float durabilityLoss = Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurabilityDrain * ((120 - GetSkillLevel(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes)) / (20 * VeinMine.durabilityMult.Value));
                        Player.m_localPlayer.GetCurrentWeapon().m_durability -= durabilityLoss;
                    }
                }
            }
        }

        public static float GetSkillIncreaseStep(Skills playerSkills, Skills.SkillType skillType)
        {
            if (playerSkills != null)
                foreach (var skill in playerSkills.m_skills)
                {
                    if (skill.m_skill == skillType)
                    {
                        return skill.m_increseStep;
                    }
                }
            return 1f;
        }
        public static float GetSkillLevel(Skills playerSkills, Skills.SkillType skillType)
        {
            if (playerSkills != null) return playerSkills.GetSkill(skillType).m_level;

            return 1;
        }
        public static float GetDistanceFromPlayer(Vector3 playerPos, Vector3 colliderPos)
        {
            return Vector3.Distance(playerPos, colliderPos);
        }

        public static HitData SpreadDamage(HitData hit)
        {
            if (hit != null)
            {
                if (VeinMine.spreadDamageType.Value == VeinMine.spreadTypes.level)
                {
                    float modifier = (float)GetSkillLevel(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes) * 0.01f;
                    hit.m_damage.m_pickaxe *= modifier;
                }
                else
                {
                    hit.m_damage.m_pickaxe = Player.m_localPlayer.GetCurrentWeapon().GetDamage().m_pickaxe;
                    float distance = Vector3.Distance(Player.m_localPlayer.GetTransform().position, hit.m_point);
                    if (distance >= 2f) hit.m_damage.m_pickaxe /= distance * 1.25f;
                }
            }
            return hit;
        }
    }
}
