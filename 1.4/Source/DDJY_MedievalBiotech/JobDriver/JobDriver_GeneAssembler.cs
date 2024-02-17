using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine.Events;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using System.Reflection.Emit;
using System;

namespace DDJY
{
    public class JobDriver_GeneAssembler : JobDriver
    {
        private Building_TransmutationCircle TransmutationCircle => (Building_TransmutationCircle)base.TargetThingA;
        private Pawn containedPawn => (Pawn)base.TargetThingB;
        //异种植入器
        private Xenogerm xenogerm;
        //待合成的基因列表
        private List<Genepack> packsList;
        //连接到建筑的建筑列表
        public List<Thing> ConnectedFacilities => TransmutationCircle.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {

            if (TransmutationCircle.def.hasInteractionCell)
            {
                return pawn.ReserveSittableOrSpot(TransmutationCircle.InteractionCell, job, errorOnFailed);
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            TransmutationCircle.SetActor(pawn);
            pawn.drafter.Drafted = false;
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !TransmutationCircle.IsHasActor());

            //移动toil
            yield return Toils_Goto.GotoThing(TargetIndex.A, TransmutationCircle.InteractionCell + new IntVec3(0, 0, 1).RotatedBy(TransmutationCircle.Rotation));

            //初始化toil
            Toil Toils_Star = ToilMaker.MakeToil("star");
            Toils_Star.initAction = delegate
            {
                Find.WindowStack.Add(new Dialog_CreateXenogerm(TransmutationCircle, StarAction));
            };
            yield return Toils_Star;

            //重要！等待tick使starAction执行完毕
            yield return Toils_General.Wait(2, TargetIndex.None);

            //等待toil
            Toil Toils_Wait = ToilMaker.MakeToil("wait");
            Toils_Wait.initAction = delegate
            {
                Pawn actor = Toils_Wait.actor;
                actor.jobs.curDriver.ticksLeftThisToil = 3000;
                //人物朝向建筑
                actor.Rotation = TransmutationCircle.Rotation.Opposite;
            };
            Toils_Wait.AddFailCondition(delegate
            {
                return !CheckAllContainersValid();
            });
            Toils_Wait.defaultCompleteMode = ToilCompleteMode.Delay;
            Toils_Wait.WithProgressBar(TargetIndex.B, delegate { return 1f - (float)Toils_Wait.actor.jobs.curDriver.ticksLeftThisToil / 3000; }, false, -0.5f, false);
            yield return Toils_Wait;

            //完成toil
            Toil Toil_Done = ToilMaker.MakeToil("done");
            Toil_Done.initAction = delegate
            {
                Finish();
            };
            yield return Toil_Done;
        }

        //接收基因选择菜单的数据创建异种注入器
        private void StarAction(List<Genepack> packs, int architesRequired, string xenotypeName, XenotypeIconDef iconDef)
        {
            packsList = packs;
            xenogerm = (Xenogerm)ThingMaker.MakeThing(ThingDefOf.Xenogerm);
            //创建异种注入器
            xenogerm.Initialize(packs, xenotypeName, iconDef);
            
        }

        //使用异种注入器
        private void Finish()
        {
            GeneUtility.ImplantXenogermItem(containedPawn, xenogerm);
        }

        //运行时检测
        public bool CheckAllContainersValid()
        {
            if (packsList.NullOrEmpty())
            {
                Log.Message("packsList.NullOrEmpty");
                return false;
            }

            List<Thing> connectedFacilities = ConnectedFacilities;
            for (int i = 0; i < packsList.Count; i++)
            {
                bool flag = false;
                for (int j = 0; j < connectedFacilities.Count; j++)
                {
                    CompGenepackContainer compGenepackContainer = connectedFacilities[j].TryGetComp<CompGenepackContainer>();
                    if (compGenepackContainer != null && compGenepackContainer.ContainedGenepacks.Contains(packsList[i]))
                    {
                        flag = true;
                        break;
                    }
                }

                if (!flag)
                {
                    Messages.Message("MessageXenogermCancelledMissingPack".Translate(TransmutationCircle), TransmutationCircle, MessageTypeDefOf.NegativeEvent);
                    return false;
                }
            }
            return true;
        }
    }
}