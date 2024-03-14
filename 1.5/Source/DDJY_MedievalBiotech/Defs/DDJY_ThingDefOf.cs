using RimWorld;
using Verse;

namespace DDJY
{
    [DefOf]
    public static class DDJY_ThingDefOf
    {
        static DDJY_ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DDJY_ThingDefOf));
        }

        public static ThingDef DDJY_TransmutationCircle;
    }
}
