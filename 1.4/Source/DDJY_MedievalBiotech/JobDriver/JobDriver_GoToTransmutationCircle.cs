using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
namespace DDJY
{
    public class JobDriver_GoToTransmutationCircle : JobDriver
    {
        private Building_TransmutationCircle transmutationCircle => (Building_TransmutationCircle)base.TargetThingA;

        private CompGeneAssembler compGeneAssembler => transmutationCircle.TryGetComp<CompGeneAssembler>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {

            if (transmutationCircle.def.hasInteractionCell)
            {
                return pawn.ReserveSittableOrSpot(transmutationCircle.InteractionCell, job, errorOnFailed);
            }

            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            transmutationCircle.actor = pawn;
            pawn.drafter.Drafted = false;
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            //移动
            yield return Toils_Goto.GotoThing(TargetIndex.A, transmutationCircle.InteractionCell + new IntVec3(0, 0, 1).RotatedBy(transmutationCircle.Rotation));
            //调用基因选择窗口
            Toil Toils_Star = ToilMaker.MakeToil("star");
            Toils_Star.initAction = delegate
            {
                Find.WindowStack.Add(new Dialog_CreateXenogerm(transmutationCircle, compGeneAssembler.Start));
            };
            yield return Toils_Star;
            //重要！等待tick使 new Dialog_CreateXenogerm 执行完毕
            yield return Toils_General.Wait(2, TargetIndex.None);

        }
    }
}