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
    public class JobDriver_RandomlyExtractGenes : JobDriver
    {
        private Building_TransmutationCircle TransmutationCircle => (Building_TransmutationCircle)base.TargetThingA;
        private Pawn containedPawn => (Pawn)base.TargetThingB;

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
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, TransmutationCircle.InteractionCell + new IntVec3(0,0,1).RotatedBy(TransmutationCircle.Rotation));
            //等待3000
            Toil Toils_Wait = ToilMaker.MakeToil("wait");
            Toils_Wait.initAction = delegate 
            {
                Pawn actor = Toils_Wait.actor;
                //Toils_Wait剩余时间3000
                actor.jobs.curDriver.ticksLeftThisToil = 3000;
                //人物朝向建筑
                actor.Rotation = TransmutationCircle.Rotation.Opposite;
 
            };
            Toils_Wait.defaultCompleteMode = ToilCompleteMode.Delay;
            Toils_Wait.WithProgressBar(TargetIndex.B, delegate{ return 1f - (float)Toils_Wait.actor.jobs.curDriver.ticksLeftThisToil /  3000;}, false, -0.5f, false);
            yield return Toils_Wait;
            //完成
            Toil Toil_Done = ToilMaker.MakeToil("done");
            Toil_Done.initAction = delegate
            {
                 RandomlyExtractGenes();
            };
            yield return Toil_Done;
        }

        private void RandomlyExtractGenes()
        {
            List<GeneDef> genesToAdd = new List<GeneDef>();
            Genepack genepack = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);
            int num = Mathf.Min((int)GeneCountChanceCurve.RandomElementByWeight((CurvePoint p) => p.y).x, containedPawn.genes.GenesListForReading.Count((Gene x) => x.def.biostatArc == 0));
            for (int i = 0; i < num; i++)
            {
                if (!containedPawn.genes.GenesListForReading.TryRandomElementByWeight(SelectionWeight, out var result))
                {
                    break;
                }

                genesToAdd.Add(result.def);
            }

            if (genesToAdd.Any())
            {
                genepack.Initialize(genesToAdd);
            }
            IntVec3 intVec = (TransmutationCircle.def.hasInteractionCell ? TransmutationCircle.InteractionCell : TransmutationCircle.Position);
            if (!containedPawn.Dead && (containedPawn.IsPrisonerOfColony || containedPawn.IsSlaveOfColony))
            {
                containedPawn.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.XenogermHarvested_Prisoner);
            }

            if (genesToAdd.Any())
            {
                GenPlace.TryPlaceThing(genepack, intVec, base.Map, ThingPlaceMode.Near);
                //移除身体器官
                TransmutationCircle.GetComp<CompRemovePart>()?.RandomReMoveNoVitalsParts(containedPawn);
            }
            Messages.Message("GeneExtractionComplete".Translate(containedPawn.Named("PAWN")) + ": " + genesToAdd.Select((GeneDef x) => x.label).ToCommaList().CapitalizeFirst(), new LookTargets(containedPawn, genepack), MessageTypeDefOf.PositiveEvent);
            float SelectionWeight(Gene g)
            {
                if (genesToAdd.Contains(g.def))
                {
                    return 0f;
                }

                if (g.def.biostatArc > 0)
                {
                    return 0f;
                }

                if (g.def.endogeneCategory == EndogeneCategory.Melanin)
                {
                    return 0f;
                }
                if (!Includes(g.def.biostatMet + genesToAdd.Sum((GeneDef x) => x.biostatMet), GeneTuning.BiostatRange))
                {
                    return 0f;
                }

                if (g.def.biostatCpx > 0)
                {
                    return 3f;
                }
                bool Includes(int val, IntRange x)
                {
                    return val >= x.min && val <= x.max;
                }
                return 1f;
            }

        }

        private static readonly SimpleCurve GeneCountChanceCurve = new SimpleCurve
        {
            new CurvePoint(1f, 0.7f),
            new CurvePoint(2f, 0.2f),
            new CurvePoint(3f, 0.08f),
            new CurvePoint(4f, 0.02f)
        };
    }
}