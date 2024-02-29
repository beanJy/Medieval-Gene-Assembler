using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DDJY
{
    public class JobDriver_CreateXenogerm : JobDriver
    {
        private Building_TransmutationCircle TransmutationCircle => (Building_TransmutationCircle)base.TargetThingA;

        private CompGeneAssembler compGeneAssembler => TransmutationCircle.TryGetComp<CompGeneAssembler>();
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
            TransmutationCircle.actor = pawn;
            pawn.drafter.Drafted = false;
            packsList = compGeneAssembler.genepacksToRecombine;
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            //移动toil
            yield return Toils_Goto.GotoThing(TargetIndex.A, TransmutationCircle.InteractionCell + new IntVec3(0, 0, 1).RotatedBy(TransmutationCircle.Rotation));

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
            Toils_Wait.AddFinishAction(delegate {
                ConnectedFacilities.Any(
                    i => i.TryGetComp<CompDarklightOverlay>().IsActive = false
                ); ;
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


        //使用异种注入器
        private void Finish()
        {
            if (!packsList.NullOrEmpty())
            {
                xenogerm = (Xenogerm)ThingMaker.MakeThing(ThingDefOf.Xenogerm);
                xenogerm.Initialize(packsList, compGeneAssembler.xenotypeName, compGeneAssembler.iconDef);
                //GeneUtility.
                ImplantXenogermItem(TransmutationCircle.ContainedPawn, xenogerm);
                Messages.Message("DDJY_CeremonyDone".Translate(TransmutationCircle.ContainedPawn), new LookTargets(TransmutationCircle.ContainedPawn), MessageTypeDefOf.PositiveEvent);
            }
            if (compGeneAssembler.architesRequired > 0)
            {
                for (int i = compGeneAssembler.innerContainer.Count - 1; i >= 0; i--)
                {
                    if (compGeneAssembler.innerContainer[i].def == ThingDefOf.ArchiteCapsule)
                    {
                        Thing thing = compGeneAssembler.innerContainer[i].SplitOff(Mathf.Min(compGeneAssembler.innerContainer[i].stackCount, compGeneAssembler.architesRequired));
                        compGeneAssembler.architesRequired -= thing.stackCount;
                        thing.Destroy(DestroyMode.Vanish);
                        if (compGeneAssembler.architesRequired <= 0)
                        {
                            break;
                        }
                    }
                }
            }
        }
        //运行时检测
        public bool CheckAllContainersValid()
        {
            if (packsList.NullOrEmpty())
            {
                return false;
            }

            List<Thing> connectedFacilities = ConnectedFacilities;
            for (int i = 0; i < packsList.Count; i++)
            {
                bool flag = false;
                for (int j = 0; j < connectedFacilities.Count; j++)
                {
                    CompGenepackContainer compGenepackContainer = connectedFacilities[j].TryGetComp<CompGenepackContainer>();
                    CompDarklightOverlay compDarklightOverlay = connectedFacilities[j].TryGetComp<CompDarklightOverlay>();
                    if (compGenepackContainer != null && compGenepackContainer.ContainedGenepacks.Contains(packsList[i]))
                    {
                        compDarklightOverlay.IsActive = true;
                        flag = true;
                        break;
                    }
                    if (packsList[i].GeneSet.GenesListForReading.Count == 1 )
                    {
                        List<Gene> list = new List<Gene>();
                        if (compGeneAssembler.inheritable)
                        {
                            list = TransmutationCircle.ContainedPawn.genes.Endogenes;
                        }
                        else
                        {
                            list = TransmutationCircle.ContainedPawn.genes.Xenogenes;
                        }
                        foreach(Gene gene in list)
                        {
                            gene.def = packsList[i].GeneSet.GenesListForReading[0];
                            flag = true;
                            break;
                        }

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

        private  void ImplantXenogermItem(Pawn pawn, Xenogerm xenogerm)
        {
            if (!ModLister.CheckBiotech("xenogerm implantation"))
            {
                return;
            }

            UpdateXenogermReplication(pawn);
            if (xenogerm.GeneSet == null || pawn.genes == null)
            {
                return;
            }
            if (compGeneAssembler.inheritable)
            {
                for (int num = pawn.genes.Endogenes.Count - 1; num >= 0; num--)
                {
                    pawn.genes.RemoveGene(pawn.genes.Endogenes[num]);
                }
            }
            else
            {
                pawn.genes.SetXenotype(XenotypeDefOf.Baseliner);
            }
            pawn.genes.xenotypeName = xenogerm.xenotypeName;
            pawn.genes.iconDef = xenogerm.iconDef;
            foreach (GeneDef item in xenogerm.GeneSet.GenesListForReading)
            {
                pawn.genes.AddGene(item, xenogene: !compGeneAssembler.inheritable);
            }
        }
        public static void UpdateXenogermReplication(Pawn pawn)
        {
            if (ModsConfig.BiotechActive)
            {
                Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.XenogermReplicating);
                if (firstHediffOfDef != null)
                {
                    pawn.health.RemoveHediff(firstHediffOfDef);
                }

                pawn.health.AddHediff(HediffDefOf.XenogermReplicating);
            }
        }
        
    }
}
