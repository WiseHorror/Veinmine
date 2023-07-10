using UnityEngine;

namespace WiseHorror.Veinmine
{
    class Functions
    {
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
                    float modifier = (float)GetSkillLevel(Player.GetClosestPlayer(hit.m_point, 5f).GetSkills(), Skills.SkillType.Pickaxes) * 0.01f;
                    hit.m_damage.m_pickaxe *= modifier;
                }
                else
                {
                    hit.m_damage.m_pickaxe = Player.GetClosestPlayer(hit.m_point, 5f).GetCurrentWeapon().GetDamage().m_pickaxe;
                    float distance = Vector3.Distance(Player.GetClosestPlayer(hit.m_point, 5f).GetTransform().position, hit.m_point);
                    if (distance >= 2f) hit.m_damage.m_pickaxe /= distance * 1.25f;
                }
            }

            return hit;
        }
    }
}