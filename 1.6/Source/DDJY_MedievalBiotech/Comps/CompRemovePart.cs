using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace DDJY
{
    public class CompRemovePart : ThingComp
    {
        //是否是重要器官
        private bool isVitalsPart(BodyPartRecord part)
        {
            foreach (BodyPartTagDef tags in part.def.tags)
            {
                if (tags.vital)
                {
                    return true;
                }
            }
            return false;
        }

        //筛选出非重要器官
        private IEnumerable<BodyPartRecord> ScreenNoIsVitalsPart(Pawn pawn)
        {
            //获取所有器官
            IEnumerable<BodyPartRecord> PartsList = pawn.def.race.body.AllParts;
            List<BodyPartDef> filterateBodyPartDefs = new List<BodyPartDef>() { DDJY_BodyPartDefOf.Femur, DDJY_BodyPartDefOf.Humerus, DDJY_BodyPartDefOf.Radius, DDJY_BodyPartDefOf.Tibia };
            if (PartsList.Any())
            {
                foreach (BodyPartRecord bodyPart in PartsList)
                {
                    //过滤不存在的器官
                    if (pawn.health.hediffSet.PartIsMissing(bodyPart)) { continue; }
                    //人造器官
                    if (pawn.health.hediffSet.HasDirectlyAddedPartFor(bodyPart)) { continue; }
                    //过滤躯干（骨骼和内脏）
                    if (bodyPart.IsInGroup(BodyPartGroupDefOf.Torso)) { continue; }
                    //过滤上半部分头
                    if (bodyPart.IsInGroup(BodyPartGroupDefOf.UpperHead)) { continue; }
                    //过滤重要器官
                    if (isVitalsPart(bodyPart)) { continue; };
                    //过滤指定part
                    if (!filterateBodyPartDefs.All(part => part != bodyPart.def)) { continue; }
                    yield return bodyPart;
                }
            }
        }

        //筛选重要器官
        private IEnumerable<BodyPartRecord> ScreenIsVitalsPart(Pawn pawn)
        {
            IEnumerable<BodyPartRecord> PartsList = pawn.def.race.body.AllParts;
            if (PartsList.Any())
            {
                foreach (BodyPartRecord bodyPart in PartsList)
                {
                    //过滤不存在的器官
                    if (pawn.health.hediffSet.PartIsMissing(bodyPart)) { continue; }
                    //人造器官
                    if (pawn.health.hediffSet.HasDirectlyAddedPartFor(bodyPart)) { continue; }
                    //过滤重要器官
                    if (!isVitalsPart(bodyPart)) { continue; };
                    if (bodyPart.def == DDJY_BodyPartDefOf.Neck) { continue; };
                    yield return bodyPart;
                }
            }
        }

        //随机移除非重要器官
        public void RandomReMoveNoVitalsParts(Pawn pawn)
        {
            BodyPartRecord removePart = null;
            //调用过滤器方法
            IEnumerable<BodyPartRecord> noIsVitalsList = ScreenNoIsVitalsPart(pawn).Distinct();
            IEnumerable<BodyPartRecord> isVitalsList = ScreenIsVitalsPart(pawn).Distinct();
            //非致命器官列表不为空
            if (noIsVitalsList.Any())
            {
                // 创建一个 Random 对象
                System.Random random = new System.Random();
                // 生成一个随机索引
                int randomIndex = random.Next(0, noIsVitalsList.Count());
                // 获取随机索引处的元素
                removePart = noIsVitalsList.ElementAt(randomIndex);
            }
            //致命器官列表不为空
            else if (isVitalsList.Any())
            {
                // 创建一个 Random 对象
                System.Random random = new System.Random();
                // 生成一个随机索引
                int randomIndex = random.Next(0, isVitalsList.Count());
                // 获取随机索引处的元素
                removePart = isVitalsList.ElementAt(randomIndex);
            }
            //移除指定器官
            if (removePart!= null && !pawn.health.hediffSet.PartIsMissing(removePart) && !pawn.Dead)
            {
                pawn.health.AddHediff(HediffDefOf.MissingBodyPart, removePart);
                Messages.Message("DDJY_RemovePart".Translate(pawn.LabelShortCap, removePart.Label), new LookTargets(pawn), MessageTypeDefOf.PositiveEvent);
            }
        }
    }
}
