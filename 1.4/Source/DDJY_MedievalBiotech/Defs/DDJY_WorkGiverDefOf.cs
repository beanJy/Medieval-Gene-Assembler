using RimWorld;
using Verse;

namespace DDJY
{
    [DefOf]
    public static class DDJY_WorkGiverDef
    {
        static DDJY_WorkGiverDef()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DDJY_WorkGiverDef));
        }

        public static WorkGiverDef DDJY_CarryToTransmutationCircle;
    }
}
