using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace WiseHorror.VeinMine
{
    [BepInPlugin(MOD_ID, MOD_NAME, VERSION)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim_server.exe")]
    [HarmonyPatch]
    public class VeinMine : BaseUnityPlugin
    {
        private const string MOD_ID = "com.wisehorror.Veinmine";
        private const string MOD_NAME = "Veinmine";
        private const string VERSION = "1.1.9";

        public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("Veinmine");

        private enum spreadTypes
        {
            even,
            distance
        }

        void Awake()
        {
            Harmony harmony = new Harmony(MOD_ID);
            harmony.PatchAll();

            veinMineKey = Config.Bind("General",
                "Veinmine",
                KeyCode.LeftAlt,
                "Key (or button) to hold for veinmining. Refer to https://docs.unity3d.com/Manual/class-InputManager.html");

            veinMineDurability = Config.Bind("General",
                "Durability",
                true,
                "Veinmining takes durability as if you mined every section manually.\nDoesn't do anything in Progressive mode, as durability is always removed there.");

            removeEffects = Config.Bind("Visual",
                "Remove Effects",
                false,
                "Remove mining visual effects to try to *possibly* reduce fps lag ");

            progressiveMode = Config.Bind("Progressive",
                "Enable Progressive",
                false,
                "Progressive mode scales the number of sections mined according to the player's Pickaxes level.");

            progressiveMult = Config.Bind("Progressive",
                "Radius Multiplier",
                0.1f,
                "Radius around your hit area to veinmine. This value is multiplied by the player's Pickaxes level.\n" +
                "Keep in mind 1 is the same length as the 1x1 wood floor.\n" +
                "So if you have Lvl 30 and set this value to 0.1, you will veinmine rocks in a radius of 3 (0.1 * 30 = 3).");

            durabilityMult = Config.Bind("Progressive",
                "Durability Multiplier",
                1f,
                "Determines durability lost when veinmining.\n" +
                "The formula is (120 - Pickaxes level) / (20 * multiplier) where \"multiplier\" is this value.");

            xpMult = Config.Bind("Progressive",
                "XP Multiplier",
                0.2f,
                "Multiplier for XP gained per rock section veinmined.");

            enableSpreadDamage = Config.Bind("Progressive",
                "Enable Spread Damage",
                false,
                "Spreads your hit damage throughout all rock sections mined, as opposed to hitting every section for the total amount of damage.");

            spreadDamageType = Config.Bind("Progressive",
                "Spread Damage Type",
                spreadTypes.distance,
                "Even: Divides your hit damage by the number of sections, and does the resulting damage to each section evenly.\n" +
                "Keep in mind that if you mine veins with a large amount of sections (such as copper) you might do almost no damage." +
                "Distance: Calculates damage done to each section depending on your distance from it, the farther away, the less damage you do.");

        }

        private static ConfigEntry<KeyCode> veinMineKey;
        private static ConfigEntry<bool> veinMineDurability;
        private static ConfigEntry<bool> removeEffects;
        private static ConfigEntry<bool> progressiveMode;
        private static ConfigEntry<bool> enableSpreadDamage;
        private static ConfigEntry<spreadTypes> spreadDamageType;
        private static ConfigEntry<float> progressiveMult;
        private static ConfigEntry<float> durabilityMult;
        private static ConfigEntry<float> xpMult;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MineRock), "Damage")]
        public static void MineRock_Damage_Prefix(MineRock __instance, HitData hit)
        {
            if (!progressiveMode.Value) hit.m_damage.m_pickaxe = __instance.m_health + 10;
            hit.m_point = __instance.GetHitArea(__instance.GetAreaIndex(hit.m_hitCollider)).bounds.center;
        }

        [HarmonyPatch]
        public class MineRock5_Patch
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(MineRock5), "Damage")]
            public static void MineRock5_Damage_Prefix(MineRock5 __instance, HitData hit, out Dictionary<int, Vector3> __state)
            {
                __instance.SetupColliders();
                __state = new Dictionary<int, Vector3>();

                if (Input.GetKey(veinMineKey.Value) && progressiveMode.Value)
                {
                    var radiusColliders = Physics.OverlapSphere(hit.m_point, progressiveMult.Value * (float)GetSkillLevel(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes));

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
                    Vector3 hit_point = hit.m_point;
                    if (Input.GetKey(veinMineKey.Value) && !progressiveMode.Value)
                    {
                        for (int i = 0; i < (___m_hitAreas.Count <= 128 ? ___m_hitAreas.Count : 128); i++)
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
                                    logger.LogInfo("Skipping section: " + i + ".");
                                }
                            }
                        }
                    }
                    else if (Input.GetKey(veinMineKey.Value) && progressiveMode.Value)
                    {
                        foreach (var index in __state)
                        {
                            if (enableSpreadDamage.Value)
                            {
                                if (Player.m_localPlayer.GetCurrentWeapon().m_durability > 0 || !Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
                                {
                                    try
                                    {
                                        ___m_nview.InvokeRPC("Damage", new object[]
                                           {
                                                spreadDamageType.Value == spreadTypes.even ? SpreadDamage(hit, __state.Count, index.Value) : SpreadDamageByDist(hit, index.Value),
                                                index.Key
                                           });
                                    }
                                    catch (Exception e)
                                    {
                                        logger.LogInfo("Skipping section: " + index.Key + ".");
                                    }
                                }
                            }
                            else if (!enableSpreadDamage.Value)
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
                                        logger.LogInfo("Skipping section: " + index.Key + ".");
                                    }
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
            MineRock5.HitArea hitArea = __instance.GetHitArea(hitAreaIndex);
            __state = hitArea.m_health;

            bool isVeinmined = false;
            if (Input.GetKey(veinMineKey.Value)) isVeinmined = true;

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
            if (!removeEffects.Value) __instance.m_hitEffect.Create(hit.m_point, Quaternion.identity, null, 1f);
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
                if(!removeEffects.Value) __instance.m_destroyedEffect.Create(hit.m_point, Quaternion.identity, null, 1f);
                foreach (GameObject gameObject in __instance.m_dropItems.GetDropList())
                {
                    if (isVeinmined)
                    {
                        Vector3 position = Player.m_localPlayer.GetTransform().position + new Vector3 { x = 0, y = 2, z = 0 } + UnityEngine.Random.insideUnitSphere * 0.3f;
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
            if (Input.GetKey(veinMineKey.Value))
            {
                if (__state > 0f && hit.m_attacker == Player.m_localPlayer.GetZDOID() && !progressiveMode.Value)
                {
                    Player.m_localPlayer.RaiseSkill(Skills.SkillType.Pickaxes, GetSkillIncreaseStep(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes));

                    if (veinMineDurability.Value && Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
                    {
                        Player.m_localPlayer.GetCurrentWeapon().m_durability -= Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurabilityDrain;
                    }
                    else if (veinMineDurability.Value && !Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
                    {
                    }

                }
                else if (__state > 0f && hit.m_attacker == Player.m_localPlayer.GetZDOID() && progressiveMode.Value)
                {
                    Player.m_localPlayer.RaiseSkill(Skills.SkillType.Pickaxes, GetSkillIncreaseStep(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes) * xpMult.Value);

                    if (Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurability)
                    {
                        float durabilityLoss = Player.m_localPlayer.GetCurrentWeapon().m_shared.m_useDurabilityDrain * ((120 - GetSkillLevel(Player.m_localPlayer.GetSkills(), Skills.SkillType.Pickaxes)) / (20 * durabilityMult.Value));
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