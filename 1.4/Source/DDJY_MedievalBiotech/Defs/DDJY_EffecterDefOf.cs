using RimWorld;
using Verse;

namespace DDJY
{
    [DefOf]
    public static class DDJY_EffecterDefOf
    {
        static DDJY_EffecterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DDJY_EffecterDefOf));
        }

        public static EffecterDef DDJY_Effecter_TransmutationCircle;
    }
}