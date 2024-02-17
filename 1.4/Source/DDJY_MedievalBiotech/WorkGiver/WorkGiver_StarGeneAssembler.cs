using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace DDJY
{
    public class WorkGiver_StarGeneAssembler : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(DDJY_ThingDefOf.DDJY_TransmutationCircle);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !ModsConfig.BiotechActive;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AlchemyTable building_GeneAssembler;
            if ((building_GeneAssembler = t as Building_AlchemyTable) == null)
            {
                return false;
            }

            if (building_GeneAssembler.ArchitesRequiredNow > 0)
            {
                if (FindArchiteCapsule(pawn) == null)
                {
                    JobFailReason.Is("NoIngredient".Translate(ThingDefOf.ArchiteCapsule));
                    return false;
                }

                return true;
            }

            if (!building_GeneAssembler.CanBeWorkedOnNow.Accepted)
            {
                return false;
            }

            if (!pawn.CanReserve(t, 1, -1, null, forced) || !pawn.CanReserveSittableOrSpot(t.InteractionCell, forced))
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AlchemyTable building_GeneAssembler;
            if ((building_GeneAssembler = t as Building_AlchemyTable) == null)
            {
                return null;
            }

            if (building_GeneAssembler.ArchitesRequiredNow > 0)
            {
                Thing thing = FindArchiteCapsule(pawn);
                if (thing != null)
                {
                    Job job = JobMaker.MakeJob(JobDefOf.HaulToContainer, thing, t);
                    job.count = Mathf.Min(building_GeneAssembler.ArchitesRequiredNow, thing.stackCount);
                    return job;
                }
            }

            return JobMaker.MakeJob(DDJY_JobDefOf.DDJY_CreateXenogerm, t, 1200, checkOverrideOnExpiry: true);
        }

        private Thing FindArchiteCapsule(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(ThingDefOf.ArchiteCapsule), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x));
        }
    }
}