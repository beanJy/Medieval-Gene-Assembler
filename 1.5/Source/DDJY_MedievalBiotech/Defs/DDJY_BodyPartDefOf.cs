using RimWorld;
using Verse;

namespace DDJY
{
    [DefOf]
    public static class DDJY_BodyPartDefOf
    {
        static DDJY_BodyPartDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DDJY_BodyPartDefOf));
        }

        public static BodyPartDef Tibia;
        public static BodyPartDef Radius;
        public static BodyPartDef Humerus;
        public static BodyPartDef Femur;
        public static BodyPartDef Neck;
    }
}
