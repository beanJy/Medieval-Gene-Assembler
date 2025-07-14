using System;
using Verse;
using RimWorld;

namespace DDJY
{
    public class WorkGiver_CarryToTransmutationCircle : WorkGiver_CarryToBuilding
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForDef(DDJY_ThingDefOf.DDJY_TransmutationCircle);
            }
        }
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return base.ShouldSkip(pawn, forced) || !ModsConfig.BiotechActive;
        }
    }
}