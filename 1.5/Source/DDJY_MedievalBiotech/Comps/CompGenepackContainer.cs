using RimWorld;
using System.Collections.Generic;
using Verse;

namespace DDJY
{
    public class CompGenepackContainer : RimWorld.CompGenepackContainer
    {
        private static readonly CachedTexture EjectTex = new CachedTexture("UI/DDJY_EjectAll");
        public new bool PowerOn => true;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction == Faction.OfPlayer && innerContainer.Any)
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "EjectAll".Translate();
                command_Action.defaultDesc = "EjectAllDesc".Translate();
                command_Action.icon = EjectTex.Texture;
                command_Action.action = delegate
                {
                    EjectContents(parent.Map);
                };
                yield return command_Action;
            }

            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: Fill with new packs",
                action = delegate
                {
                    innerContainer.ClearAndDestroyContents();
                    for (int i = 0; i < Props.maxCapacity; i++)
                    {
                        innerContainer.TryAdd(ThingMaker.MakeThing(ThingDefOf.Genepack));
                    }
                }
            };
        }
    }
}