
using System.Collections.Generic;
using RimWorld;
using Verse.AI;
using Verse;
using System;
using UnityEngine;

namespace DDJY
{
    public class JobDriver_HaulToContainer : JobDriver
    {
        private Effecter graveDigEffect;

        protected const TargetIndex CarryThingIndex = TargetIndex.A;

        public const TargetIndex DestIndex = TargetIndex.B;

        protected const TargetIndex PrimaryDestIndex = TargetIndex.C;

        protected const int DiggingEffectInterval = 80;

        public Thing ThingToCarry => (Thing)job.GetTarget(TargetIndex.A);

        public Building_TransmutationCircle transmutationCircle => (Building_TransmutationCircle)job.GetTarget(TargetIndex.B);

        protected virtual int Duration
        {
            get
            {
                if (transmutationCircle == null || !(transmutationCircle is Building))
                {
                    return 0;
                }

                return transmutationCircle.def.building.haulToContainerDuration;
            }
        }

        protected virtual EffecterDef WorkEffecter => null;

        protected virtual SoundDef WorkSustainer => null;

        public override string GetReport()
        {
            Thing thing = null;
            thing = ((pawn.CurJob != job || pawn.carryTracker.CarriedThing == null) ? base.TargetThingA : pawn.carryTracker.CarriedThing);
            if (thing == null || !job.targetB.HasThing)
            {
                return "ReportHaulingUnknown".Translate();
            }

            return ((job.GetTarget(TargetIndex.B).Thing is Building_Grave) ? "ReportHaulingToGrave" : "ReportHaulingTo").Translate(thing.Label, job.targetB.Thing.LabelShort.Named("DESTINATION"), thing.Named("THING"));
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Log.Message("TryMakePreToilReservations");
            if (!pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed))
            {
                Log.Message("TargetIndex.A false");
                return false;
            }

            if (!pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, -1, null, errorOnFailed))
            {
                Log.Message("TargetIndex.b false");
                return false;
            }
            
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.A), job);
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.B), job);
            Log.Message("true");
            return true;
        }

        protected virtual void ModifyPrepareToil(Toil toil)
        {
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            transmutationCircle.actor = pawn;
            pawn.drafter.Drafted = false;
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => TransporterUtility.WasLoadingCanceled(transmutationCircle));
            this.FailOn(() => CompBiosculpterPod.WasLoadingCanceled(transmutationCircle));
            this.FailOn(() => Building_SubcoreScanner.WasLoadingCancelled(transmutationCircle));
            this.FailOn(delegate
            {
                ThingOwner thingOwner = transmutationCircle.TryGetInnerInteractableThingOwner();
                if (thingOwner != null && !thingOwner.CanAcceptAnyOf(ThingToCarry))
                {
                    return true;
                }

                IHaulDestination haulDestination = transmutationCircle as IHaulDestination;
                return (haulDestination != null && !haulDestination.Accepts(ThingToCarry)) ? true : false;
            });
            Toil getToHaulTarget = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            Toil uninstallIfMinifiable = Toils_Construct.UninstallIfMinifiable(TargetIndex.A).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            Toil startCarryingThing = Toils_Haul.StartCarryThing(TargetIndex.A, putRemainderInQueue: false, subtractNumTakenFromJobCount: true);
            Toil jumpIfAlsoCollectingNextTarget = Toils_Haul.JumpIfAlsoCollectingNextTargetInQueue(getToHaulTarget, TargetIndex.A);
            Toil carryToContainer = Toils_Haul.CarryHauledThingToContainer();
            yield return Toils_Jump.JumpIf(jumpIfAlsoCollectingNextTarget, () => pawn.IsCarryingThing(ThingToCarry));
            yield return getToHaulTarget;
            yield return uninstallIfMinifiable;
            yield return startCarryingThing;
            yield return jumpIfAlsoCollectingNextTarget;
            yield return carryToContainer;
            yield return Toils_Goto.MoveOffTargetBlueprint(TargetIndex.B);
            Toil toil = Toils_General.Wait(Duration, TargetIndex.B);
            toil.WithProgressBarToilDelay(TargetIndex.B);
            EffecterDef workEffecter = WorkEffecter;
            if (workEffecter != null)
            {
                toil.WithEffect(workEffecter, TargetIndex.B);
            }

            SoundDef workSustainer = WorkSustainer;
            if (workSustainer != null)
            {
                toil.PlaySustainerOrSound(workSustainer);
            }

            Thing destThing = job.GetTarget(TargetIndex.B).Thing;
            toil.tickAction = delegate
            {
                if (pawn.IsHashIntervalTick(80) && destThing is Building_Grave && graveDigEffect == null)
                {
                    graveDigEffect = EffecterDefOf.BuryPawn.Spawn();
                    graveDigEffect.Trigger(destThing, destThing);
                }

                graveDigEffect?.EffectTick(destThing, destThing);
            };
            ModifyPrepareToil(toil);
            yield return toil;
            yield return DepositHauledThingInContainer(TargetIndex.B, TargetIndex.C, delegate { transmutationCircle.TryGetComp<CompGeneAssembler>().SelectJob();});
        }
        public static Toil DepositHauledThingInContainer(TargetIndex containerInd, TargetIndex reserveForContainerInd, Action onDeposited = null)
        {
            Toil toil = ToilMaker.MakeToil("DepositHauledThingInContainer");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                if (actor.carryTracker.CarriedThing == null)
                {
                    Log.Error(string.Concat(actor, " tried to place hauled thing in container but is not hauling anything."));
                }
                else
                {
                    Thing thing = curJob.GetTarget(containerInd).Thing;
                    ThingOwner thingOwner = thing.TryGetInnerInteractableThingOwner();
                    if (thingOwner != null)
                    {
                        int num = actor.carryTracker.CarriedThing.stackCount;
                        if (thing is IConstructible)
                        {
                            num = Mathf.Min(GenConstruct.AmountNeededByOf((IConstructible)thing, actor.carryTracker.CarriedThing.def), num);
                            if (reserveForContainerInd != 0)
                            {
                                Thing thing2 = curJob.GetTarget(reserveForContainerInd).Thing;
                                if (thing2 != null && thing2 != thing)
                                {
                                    int num2 = GenConstruct.AmountNeededByOf((IConstructible)thing2, actor.carryTracker.CarriedThing.def);
                                    num = Mathf.Min(num, actor.carryTracker.CarriedThing.stackCount - num2);
                                }
                            }
                        }
                        Thing carriedThing = actor.carryTracker.CarriedThing;
                        actor.carryTracker.innerContainer.TryTransferToContainer(carriedThing, thingOwner, num);
                        Log.Message("执行");
                        onDeposited?.Invoke();
                        
                    }
                    else if (curJob.GetTarget(containerInd).Thing.def.Minifiable)
                    {
                        actor.carryTracker.innerContainer.ClearAndDestroyContents();
                    }
                    else
                    {
                        Log.Error("Could not deposit hauled thing in container: " + curJob.GetTarget(containerInd).Thing);
                    }
                }
            };
            return toil;
        }
    }
}

