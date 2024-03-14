using RimWorld;
using Verse;

namespace DDJY
{
    [DefOf]
    public static class DDJY_ResearchProjectDefOf
    {
        static DDJY_ResearchProjectDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DDJY_ResearchProjectDefOf));
        }

        public static ResearchProjectDef DDJY_ArchiteSoulAlchemy;
        public static ResearchProjectDef DDJY_InheritableSoul;
        public static ResearchProjectDef DDJY_SelfSoulEdit;
    }
}