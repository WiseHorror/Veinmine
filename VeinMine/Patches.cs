using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace WiseHorror.Veinmine
{
    class Patches
    {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MineRock), "Damage")]
        public static void MineRock_Damage_Prefix(MineRock __instance, HitData hit)
        {
            if (!VeinMine.progressiveMode.Value) hit.m_damage.m_pickaxe = __instance.m_health + 10; // Insta mine if progressive mode is off.
            hit.m_point = __instance.GetHitArea(__instance.GetAreaIndex(hit.m_hitCollider)).bounds.center; // Make damage numbers display on the correct rock section.
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MineRock5), "Damage")]
        public static void MineRock5_Damage_Prefix(MineRock5 __instance, HitData hit, out Dictionary<int, Vector3> __state)
        {
            __instance.SetupColliders();
            __state = new Dictionary<int, Vector3>();

            if (Input.GetKey(VeinMine.veinMineKey.Value) && VeinMine.progressiveMode.Value)
            {
                // Getting all rock sections in radius.
                var radiusColliders = Physics.OverlapSphere(hit.m_point, VeinMine.progressiveMult.Value * (float)GetSkillLevel(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes));

                if (radiusColliders != null)
                {
                    foreach (var area in radiusColliders)
                    {
                        if (__instance.GetAreaIndex(area) >= 0)
                        {
                            __state.Add(__instance.GetAreaIndex(area), __instance.GetHitArea(__instance.GetAreaIndex(area)).m_bound.m_pos +
                                __instance.GetHitArea(__instance.GetAreaIndex(area)).m_collider.transform.position); // storing each section's index and position to pass to postfix.
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
                Vector3 hit_point = hit.m_point;
                if (Input.GetKey(VeinMine.veinMineKey.Value) && !VeinMine.progressiveMode.Value) // If progressive is off
                {
                    for (int i = 0; i < (___m_hitAreas.Count <= 128 ? ___m_hitAreas.Count : 128); i++) // TODO: figure out why over 128 throws exceptions.
                    {
                        if (Player.m_localPlayer.GetCurrentWeapon().m_durability > 0 || !Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
                        {
                            hit.m_point = __instance.GetHitArea(i).m_bound.m_pos;
                            hit.m_damage.m_pickaxe = __instance.m_health + 10;
                            try
                            {
                                ___m_nview.InvokeRPC("Damage", new object[]
                                {
                                                hit,
                                                i
                                });
                            }
                            catch (Exception e)
                            {
                                VeinMine.logger.LogInfo("Skipping section: " + i + ".");
                            }
                        }
                    }
                }
                else if (Input.GetKey(VeinMine.veinMineKey.Value) && VeinMine.progressiveMode.Value)
                {
                    foreach (var index in __state) // go through rock sections inside radius (check Prefix)
                    {
                        if (VeinMine.enableSpreadDamage.Value)
                        {
                            if (Player.m_localPlayer.GetCurrentWeapon().m_durability > 0 || !Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
                            {
                                try
                                {
                                    ___m_nview.InvokeRPC("Damage", new object[]
                                       {
                                                VeinMine.spreadDamageType.Value == VeinMine.spreadTypes.even ? SpreadDamage(hit, __state.Count, index.Value) : SpreadDamageByDist(hit, index.Value),
                                                index.Key
                                       });
                                }
                                catch (Exception e)
                                {
                                    VeinMine.logger.LogInfo("Skipping section: " + index.Key + ".");
                                }
                            }
                        }
                        else if (!VeinMine.enableSpreadDamage.Value)
                        {
                            hit.m_point = index.Value;

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
                                catch (Exception e)
                                {
                                    VeinMine.logger.LogInfo("Skipping section: " + index.Key + ".");
                                }
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
            // this prefix is essentially a copy of the original method so it shouldn't cause many mod incompatibilities.

            MineRock5.HitArea hitArea = __instance.GetHitArea(hitAreaIndex);
            __state = hitArea.m_health;
            bool isVeinmined = false;

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
            if (!VeinMine.removeEffects.Value) __instance.m_hitEffect.Create(hit.m_point, Quaternion.identity, null, 1f);
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
                if (!VeinMine.removeEffects.Value) __instance.m_destroyedEffect.Create(hit.m_point, Quaternion.identity, null, 1f);
                foreach (GameObject gameObject in __instance.m_dropItems.GetDropList())
                {
                    if (isVeinmined) // changing the spawn position of drops to be on top of the player, to prevent them from spawning out of bounds.
                    {
                        Vector3 position = Player.m_localPlayer.GetTransform().position + new Vector3 { x = 0, y = 2, z = 0 } + UnityEngine.Random.insideUnitSphere * 0.3f;
                        VeinMine.logger.LogInfo("Dropping " + gameObject.name + " at position: " + position);
                        UnityEngine.Object.Instantiate<GameObject>(gameObject, position, Quaternion.identity);
                        hit.m_point = Player.m_localPlayer.GetTransform().position + new Vector3 { x = 0, y = 2, z = 0 };
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

        /*
        [HarmonyPatch(typeof(MineRock5), "DamageArea")]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            for (int i = 0; i < list.Count; i++)
            {
                bool flag = list[i].opcode == OpCodes.Ldarg_2 && list[i + 1].opcode == OpCodes.Ldfld && list[i + 2].opcode == OpCodes.Call && list[i + 3].opcode == OpCodes.Ldc_R4;
                if (flag)
                {
                    logger.LogInfo("Removing vanilla ore drop coordinates...");
                    list[i + 9].opcode = OpCodes.Nop;
                }
            }
            return list.AsEnumerable<CodeInstruction>();
        }*/

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MineRock5), "DamageArea")]
        public static void MineRock5_DamageArea_Patch(MineRock5 __instance, HitData hit, float __state, bool __result)
        {
            // Handling durability and xp
            if (Input.GetKey(VeinMine.veinMineKey.Value))
            {
                if (__state > 0f && hit.m_attacker == Player.m_localPlayer.GetZDOID() && !VeinMine.progressiveMode.Value)
                {
                    Player.m_localPlayer.RaiseSkill(Skills.SkillType.Pickaxes, GetSkillIncreaseStep(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes));

                    if (VeinMine.veinMineDurability.Value && Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
                    {
                        Player.m_localPlayer.GetCurrentWeapon().m_durability -= Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurabilityDrain;
                    }
                    else if (VeinMine.veinMineDurability.Value && !Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability) // this does nothing and only is here for logging purposes.
                    {
                        //VeinMine.logger.LogInfo("No durability used.");
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

        public static HitData SpreadDamage(HitData hit, int numOfAreas, Vector3 position)
        {
            hit.m_point = position;

            if (hit != null && numOfAreas > 0)
            {
                hit.m_damage.m_pickaxe /= numOfAreas;
            }

            return hit;
        }
        public static HitData SpreadDamageByDist(HitData hit, Vector3 position)
        {
            hit.m_damage.m_pickaxe = Player.m_localPlayer.GetCurrentWeapon().GetDamage().m_pickaxe;

            float distance = Vector3.Distance(hit.m_point, position);
            hit.m_point = position;

            if (hit != null)
            {
                if (distance >= 2f) hit.m_damage.m_pickaxe /= distance * 1.25f;
            }

            return hit;
        }

        /*public static void GenerateOreDrops(MineRock5 instance, MineRock5.HitArea hitArea, HitData hit)
        {
            if (hitArea.m_health < 0f && Input.GetKey(veinMineKey.Value))
            {
                foreach (GameObject original in instance.m_dropItems.GetDropList())
                {
                    Vector3 position = Player.m_localPlayer.GetTransform().position + new Vector3 { x = 0, y = 2, z = 0 } + UnityEngine.Random.insideUnitSphere * 0.3f;
                    UnityEngine.Object.Instantiate<GameObject>(original, position, Quaternion.identity);
                }
            }
            else if (hitArea.m_health < 0f && !Input.GetKey(veinMineKey.Value))
            {
                foreach (GameObject original in instance.m_dropItems.GetDropList())
                {
                    Vector3 position = hit.m_point + UnityEngine.Random.insideUnitSphere * 0.3f;
                    UnityEngine.Object.Instantiate<GameObject>(original, position, Quaternion.identity);
                }
            }
        }*/
    }
}
