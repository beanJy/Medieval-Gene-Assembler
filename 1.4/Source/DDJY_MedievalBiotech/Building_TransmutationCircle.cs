using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;
using System;
using static System.Net.Mime.MediaTypeNames;

namespace DDJY
{
    [StaticConstructorOnStartup]
    public class Building_TransmutationCircle : Building_Enterable, IThingHolderWithDrawnPawn, IThingHolder
    {
        //执行工作的pwan
        public Pawn actor = null;
        //CompGeneAssembler组件
        public CompGeneAssembler compGeneAssembler => this.TryGetComp<CompGeneAssembler>();
        //是否有actor
        public bool IsHasActor()
        {
            if (actor != null)
            {
                if(actor.CurJob != null && (actor.CurJob.targetA == this || actor.CurJob.targetB == this))
                {
                    return true;
                }
                actor = null;
                return false;
            }
            else
            {
                return false;
            }
        }

        //建筑内人物
        public Pawn ContainedPawn
        {
            get
            {
                foreach (var item in innerContainer)
                {
                    if (item is Pawn)
                    {
                        return (Pawn)item;
                    }
                }
                return null; // 如果没有找到 Pawn 对象，则返回 null
            }
        }

        //缓存人物纹理
        [Unsaved(false)]
        private Texture2D cachedInsertPawnTex;
        
        //取消图标
        private static readonly Texture2D CancelIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
        //随机提取图标
        private static readonly Texture2D RandomlyExtractGenesIcon = ContentFinder<Texture2D>.Get("UI/DDJY_RandomlyExtractGenesIcon");
        //融合灵魂图标
        private static readonly Texture2D AssembleGenesIcon = ContentFinder<Texture2D>.Get("UI/DDJY_AssembleGenesIcon");

        //人物的需求暂停
        public override bool IsContentsSuspended => false;

        // 插入pwan纹理
        public Texture2D InsertPawnTex
        {
            get
            {
                if (cachedInsertPawnTex == null)
                {
                    cachedInsertPawnTex = ContentFinder<Texture2D>.Get("UI/Gizmos/InsertPawn");
                }

                return cachedInsertPawnTex;
            }
        }
        
        // pawn Y轴位置
        public float HeldPawnDrawPos_Y => DrawPos.y + 3f / 74f;
        
        // pawn身体角度
        public float HeldPawnBodyAngle => base.Rotation.Opposite.AsAngle;
       
        // pawn身体方向
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;
        
        // pawn偏移位置
        public override Vector3 PawnDrawOffset => IntVec3.Zero.RotatedBy(base.Rotation).ToVector3();

        //没有Dlc销毁建筑
        public override void PostPostMake()
        {
            if (!ModLister.CheckBiotech("gene extractor"))
            {
                this.Destroy(DestroyMode.Vanish);
                return;
            }
            base.PostPostMake();
        }

        // 移除建筑执行
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
        }
        
        // 周期性更新逻辑
        public override void Tick()
        {
            base.Tick();
            innerContainer.ThingOwnerTick();

            if (IsHasActor())
            {
                if (ContainedPawn == null)
                {
                    actor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    Cancel();
                    return;
                }
                return;
            }
            else
            {
                if (selectedPawn != null && (selectedPawn.Dead))
                {
                    Cancel();
                }
            }
        }
        
        //可以接受的角色
        public override AcceptanceReport CanAcceptPawn(Pawn pawn)
        {
            if (!pawn.IsColonist && !pawn.IsSlaveOfColony && !pawn.IsPrisonerOfColony)
            {
                return false;
            }

            if (selectedPawn != null && selectedPawn != pawn)
            {
                return false;
            }

            if (!pawn.RaceProps.Humanlike || pawn.IsQuestLodger())
            {
                return false;
            }

            if (innerContainer.Count > 0)
            {
                return "Occupied".Translate();
            }
            return true;
        }
        
        // 取消当前对象的操作，并执行一些清理工作
        public void Cancel()
        {
            selectedPawn = null;
            innerContainer.TryDropAll(def.hasInteractionCell ? InteractionCell + new IntVec3(0, 0, -1).RotatedBy(this.Rotation) : base.Position, base.Map, ThingPlaceMode.Near);
        }
       
        // 完成基因提取操作
        public void Finish()
        {
            startTick = -1;
            selectedPawn = null;
            if (ContainedPawn == null)
            {
                return;
            }
        }
        
        // 启动基因提取器
        public override void TryAcceptPawn(Pawn pawn)
        {
            if ((bool)CanAcceptPawn(pawn))
            {
                selectedPawn = pawn;
                bool num = pawn.DeSpawnOrDeselect();
                if (innerContainer.TryAddOrTransfer(pawn))
                {
                    //startTick = Find.TickManager.TicksGame;
                    //Log.Message(startTick.ToString());
                }
                if (num)
                {
                    Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                }
            }
        }
        
        // 选择角色
        protected override void SelectPawn(Pawn pawn)
        {
            base.SelectPawn(pawn);
        }
        
        // 生成基因提取器的浮动菜单选项
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(selPawn))
            {
                yield return floatMenuOption;
            }
            if (selPawn.Drafted)
            {
                yield break;
            }

            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption("CannotEnterBuilding".Translate(this) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
                yield break;
            }

            AcceptanceReport acceptanceReport = CanAcceptPawn(selPawn);
            if (acceptanceReport.Accepted)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("EnterBuilding".Translate(this), delegate
                {
                    SelectPawn(selPawn);
                }), selPawn, this);
            }
            else if (base.SelectedPawn == selPawn && !selPawn.IsPrisonerOfColony)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("EnterBuilding".Translate(this), delegate
                {
                    selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.EnterBuilding, this), JobTag.Misc);
                }), selPawn, this);
            }
            else if (!acceptanceReport.Reason.NullOrEmpty())
            {
                yield return new FloatMenuOption("CannotEnterBuilding".Translate(this) + ": " + acceptanceReport.Reason.CapitalizeFirst(), null);
            }
        }
        
        // 获取基因提取器的操作按钮
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            //如果在工作
            if (IsHasActor())
            {
                //取消仪式按钮
                yield return CommandCancelCeremony();
                yield break;
            }
            if (selectedPawn != null)
            {
                //取消装载
                yield return CommandCancelLoad();
                //如果已经装进建筑
                if (ContainedPawn != null)
                {
                    //随机提取基因
                    Command_Action commandRandomlyExtractGenes = CommandRandomlyExtractGenes();
                    //设置人物基因
                    Command_Action commandAssembleGenes = CommandAssembleGenes();
                    //禁用按钮
                    if (!CanExtractGenes(selectedPawn).Accepted)
                    {
                        commandRandomlyExtractGenes.Disable(CanExtractGenes(selectedPawn).Reason);
                    }
                    if (ContainedPawn.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))
                    {
                        commandAssembleGenes.Disable("DDJY_XenogermReplicating".Translate(ContainedPawn.Named("PAWN")));
                    }
                    yield return commandRandomlyExtractGenes;
                    yield return commandAssembleGenes;
                }
                yield break;
            }
            //置入人员
            yield return CommandInsertPerson();
        }
        
        // 绘制基因提取器及其内部的选定角色
        public override void Draw()
        {
            base.Draw();
            if (ContainedPawn != null && selectedPawn != null && innerContainer.Contains(selectedPawn))
            {
                selectedPawn.Drawer.renderer.RenderPawnAt(DrawPos + this.PawnDrawOffset, null, neverAimWeapon: true);
            }
        }
        // 获取基因提取器的检查字符串
        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (selectedPawn != null && innerContainer.Count == 0)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }

                text += "WaitingForPawn".Translate(selectedPawn.Named("PAWN")).Resolve();
            }
            else if (IsHasActor() && ContainedPawn != null)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }

                text = text + "DDJY_CeremonyInProgress".Translate(ContainedPawn.Named("PAWN")).Resolve();
            }

            return text;
        }
        
        // 保存和加载游戏
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref actor, "DDJY_Building_TransmutationCircle_actor");
        }
        
        //能否提取基因
        private AcceptanceReport CanExtractGenes(Pawn pawn)
        {
            if (pawn.genes == null || !pawn.genes.GenesListForReading.Any((Gene x) => x.def.passOnDirectly))
            {
                return "PawnHasNoGenes".Translate(pawn.Named("PAWN"));
            }

            if (!pawn.genes.GenesListForReading.Any((Gene x) => x.def.biostatArc == 0))
            {
                return "PawnHasNoNonArchiteGenes".Translate(pawn.Named("PAWN"));
            }

            if (pawn.health.hediffSet.HasHediff(HediffDefOf.XenogerminationComa))
            {
                return "InXenogerminationComa".Translate();
            }

            return true;
        }

        //取消仪式按钮
        private Command_Action CommandCancelCeremony()
        {
            Command_Action command_Action = new Command_Action();
            command_Action.defaultLabel = "DDJY_CommandCancelCeremony".Translate();
            command_Action.defaultDesc = "DDJY_CommandCancelCeremonyDesc".Translate();
            command_Action.icon = CancelIcon;
            command_Action.action = delegate
            {
                if(IsHasActor())
                {
                    actor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                actor = null;
            };
            command_Action.activateSound = SoundDefOf.Designate_Cancel;
            return command_Action;
        }

        //取消装载按钮
        private Command_Action CommandCancelLoad() 
        {
            Command_Action command_Action2 = new Command_Action();
            command_Action2.defaultLabel = "CommandCancelLoad".Translate();
            command_Action2.defaultDesc = "CommandCancelLoadDesc".Translate();
            command_Action2.icon = CancelIcon;
            command_Action2.activateSound = SoundDefOf.Designate_Cancel;
            command_Action2.action = delegate
            {
                innerContainer.TryDropAll(base.Position, base.Map, ThingPlaceMode.Near);
                if (selectedPawn.CurJobDef == JobDefOf.EnterBuilding)
                {
                    selectedPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }
                selectedPawn = null;
            };
            return command_Action2;
        }

        //随机提取基因按钮
        private Command_Action CommandRandomlyExtractGenes()
        {
            Command_Action command_Action3 = new Command_Action();
            command_Action3.defaultLabel = "DDJY_CommandRandomlyExtractGenes".Translate();
            command_Action3.defaultDesc = "DDJY_CommandRandomlyExtractGenesDesc".Translate();
            command_Action3.icon = RandomlyExtractGenesIcon;
            command_Action3.activateSound = SoundDefOf.Designate_Cancel;
            command_Action3.action = delegate
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                foreach (Pawn item in base.Map.mapPawns.AllPawnsSpawned)
                {
                    if (item.RaceProps.Humanlike && !item.IsPrisonerOfColony && (item.IsColonist || item.IsSlaveOfColony))
                    {
                        if (item.skills.GetSkill(SkillDefOf.Intellectual).Level > 10)
                        {
                            if (item.Downed)
                            {
                                list.Add(new FloatMenuOption(item.LabelShortCap + ": " + "DownedLower".Translate(), null, item, Color.white));
                            }
                            else
                            {
                                string text = "DDJY_HostCeremony".Translate(item.LabelShortCap);
                                list.Add(new FloatMenuOption(text, delegate
                                {
                                    if (ContainedPawn.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))
                                    {
                                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmExtractXenogermWillKill".Translate(ContainedPawn.Named("PAWN")), delegate
                                        {
                                            item.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DDJY_JobDefOf.DDJY_RandomlyExtractGenes, this, ContainedPawn), JobTag.Misc);
                                        }));
                                    }
                                    else
                                    {
                                        item.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DDJY_JobDefOf.DDJY_RandomlyExtractGenes, this, ContainedPawn), JobTag.Misc);
                                    }
                                }, item, Color.white));
                            }
                        }
                    }
                }
                if (!list.Any())
                {
                    list.Add(new FloatMenuOption("DDJY_NobodyHostCeremony".Translate(), null));
                }
                Find.WindowStack.Add(new FloatMenu(list));
            };
            return command_Action3;
        }

        //融合灵魂按钮
        private Command_Action CommandAssembleGenes()
        {
            Command_Action command_Action4 = new Command_Action();
            command_Action4.defaultLabel = "DDJY_AssembleGenes".Translate();
            command_Action4.defaultDesc = "DDJY_AssembleGenesDesc".Translate();
            command_Action4.icon = AssembleGenesIcon;
            command_Action4.activateSound = SoundDefOf.Designate_Cancel;
            command_Action4.action = delegate
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                foreach (Pawn item in base.Map.mapPawns.AllPawnsSpawned)
                {
                    if (item.RaceProps.Humanlike && !item.IsPrisonerOfColony && (item.IsColonist || item.IsSlaveOfColony))
                    {
                        if (item.skills.GetSkill(SkillDefOf.Intellectual).Level > 10)
                        {
                            if (item.Downed)
                            {
                                list.Add(new FloatMenuOption(item.LabelShortCap + ": " + "DownedLower".Translate(), null, item, Color.white));
                            }
                            else if (item.InMentalState)
                            {
                                list.Add(new FloatMenuOption(item.LabelShortCap + ": " + item.MentalStateDef.label, null, item, Color.white));
                            }
                            else
                            {
                                string text = "DDJY_HostCeremony".Translate(item.LabelShortCap);
                                list.Add(new FloatMenuOption(text, delegate
                                {
                                    Find.WindowStack.Add(new Dialog_CreateXenogerm(this, item, compGeneAssembler.Start));
                                }, item, Color.white));
                            }
                        }
                    }
                }
                if (!list.Any())
                {
                    list.Add(new FloatMenuOption("DDJY_NobodyHostCeremony".Translate(), null));
                }
                Find.WindowStack.Add(new FloatMenu(list));
            };

            return command_Action4;
        }

        //置入人员按钮
        private Command_Action CommandInsertPerson()
        {
            Command_Action command_Action5 = new Command_Action();
            command_Action5.defaultLabel = "InsertPerson".Translate() + "...";
            command_Action5.defaultDesc = "InsertPersonGeneExtractorDesc".Translate();
            command_Action5.icon = InsertPawnTex;
            command_Action5.action = delegate
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                foreach (Pawn item in base.Map.mapPawns.AllPawnsSpawned)
                {
                    Pawn pawn = item;
                    if (pawn.genes != null)
                    {
                        AcceptanceReport acceptanceReport = CanAcceptPawn(pawn);
                        string text = pawn.LabelShortCap + ", " + pawn.genes.XenotypeLabelCap;
                        if (!acceptanceReport.Accepted)
                        {
                            if (!acceptanceReport.Reason.NullOrEmpty())
                            {
                                list.Add(new FloatMenuOption(text + ": " + acceptanceReport.Reason, null, pawn, Color.white));
                            }
                        }
                        else
                        {
                            if (pawn.InMentalState) {
                                list.Add(new FloatMenuOption(text+" ("+ pawn.MentalStateDef.label + ")", null, pawn, Color.white));
                            }
                            else
                            {
                                Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.XenogermReplicating);
                                if (firstHediffOfDef != null)
                                {
                                    text = text + " (" + firstHediffOfDef.LabelBase + ", " + firstHediffOfDef.TryGetComp<HediffComp_Disappears>().ticksToDisappear.ToStringTicksToPeriod(allowSeconds: true, shortForm: true).Colorize(ColoredText.SubtleGrayColor) + ")";
                                }

                                list.Add(new FloatMenuOption(text, delegate
                                {
                                    SelectPawn(pawn);
                                }, pawn, Color.white));
                            }
                        }
                    }
                }
                if (!list.Any())
                {
                    list.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));
                }

                Find.WindowStack.Add(new FloatMenu(list));
            };
            return command_Action5;
        }
    }
}