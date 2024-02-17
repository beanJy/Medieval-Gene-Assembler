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
        private Pawn actor = null;

        //设置actor
        public void SetActor(Pawn pawn) { actor = pawn; }
        
        //是否有actor
        public bool IsHasActor() {
            if (actor != null && actor.CurJob.targetA == this) {
                if (actor.CurJob != null && actor.CurJob.targetA != null && actor.CurJob.targetA == this)
                {
                    return true;
                }
                SetActor(null);
            }
            return false;
        }


        //缓存人物纹理
        [Unsaved(false)]
        private Texture2D cachedInsertPawnTex;
        
        //取消图标
        private static readonly Texture2D CancelIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
       
        //建筑内人物
        private Pawn ContainedPawn => innerContainer.FirstOrDefault() as Pawn;

        //建筑内物品是否暂停，人物的需求暂停
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
                    Cancel();
                    return;
                }
                return;
            }
            else
            {
                if (selectedPawn != null && selectedPawn.Dead)
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
            startTick = -1;
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
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmExtractXenogermWillKill".Translate(pawn.Named("PAWN")), delegate
                {
                    base.SelectPawn(pawn);
                }));
            }
            else
            {
                base.SelectPawn(pawn);
            }
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
                //取消提取按钮
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "CommandCancelExtraction".Translate();
                command_Action.defaultDesc = "CommandCancelExtractionDesc".Translate();
                command_Action.icon = CancelIcon;
                command_Action.action = delegate
                {
                    SetActor(null);
                };
                command_Action.activateSound = SoundDefOf.Designate_Cancel;
                yield return command_Action;
                yield break;
            }

            if (selectedPawn != null)
            {
                //取消装载
                Command_Action command_Action3 = new Command_Action();
                command_Action3.defaultLabel = "CommandCancelLoad".Translate();
                command_Action3.defaultDesc = "CommandCancelLoadDesc".Translate();
                command_Action3.icon = CancelIcon;
                command_Action3.activateSound = SoundDefOf.Designate_Cancel;
                command_Action3.action = delegate
                {
                    innerContainer.TryDropAll(base.Position, base.Map, ThingPlaceMode.Near);
                    if (selectedPawn.CurJobDef == JobDefOf.EnterBuilding)
                    {
                        selectedPawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                    selectedPawn = null;
                    startTick = -1;
                };
                yield return command_Action3;
                //如果已经装进建筑
                if (ContainedPawn != null)
                {
                    Pawn pawn = selectedPawn;
                    AcceptanceReport acceptanceReport1 = CanExtractGenes(selectedPawn);
                    //开始随机提取基因
                    Command_Action command_Action5 = new Command_Action();
                    command_Action5.defaultLabel = "随机提取基因".Translate();
                    command_Action5.defaultDesc = "CommandCancelLoadDesc".Translate();
                    command_Action5.icon = CancelIcon;
                    command_Action5.activateSound = SoundDefOf.Designate_Cancel;
                    command_Action5.action = delegate
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
                                        list.Add(new FloatMenuOption(item.LabelShortCap + " " + "DownedLower".Translate(), null, item, Color.white));
                                    }
                                    else
                                    {
                                        string text = item.LabelShortCap + " 主持仪式".Translate();
                                        list.Add(new FloatMenuOption(text, delegate
                                        {
                                            item.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DDJY_JobDefOf.DDJY_RandomlyExtractGenes, this, pawn), JobTag.Misc);
                                        }, item, Color.white));
                                    }
                                }
                            }
                        }
                        if (!list.Any())
                        {
                            list.Add(new FloatMenuOption("没有合适的主持人 ".Translate(), null));
                        }
                        Find.WindowStack.Add(new FloatMenu(list));
                    };

                    //开始定向提取基因
                    Command_Action command_Action6 = new Command_Action();
                    command_Action6.defaultLabel = "定向提取基因".Translate();
                    command_Action6.defaultDesc = "CommandCancelLoadDesc".Translate();
                    command_Action6.icon = CancelIcon;
                    command_Action6.activateSound = SoundDefOf.Designate_Cancel;
                    command_Action6.action = delegate
                    {
                        this.TryGetComp<CompRemovePart>()?.RandomReMoveNoVitalsParts(ContainedPawn);
                    };
                    //设置人物基因
                    Command_Action command_Action7 = new Command_Action();
                    command_Action7.defaultLabel = "设置基因".Translate();
                    command_Action7.defaultDesc = "CommandCancelLoadDesc".Translate();
                    command_Action7.icon = CancelIcon;
                    command_Action7.activateSound = SoundDefOf.Designate_Cancel;
                    command_Action7.action = delegate
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
                                        list.Add(new FloatMenuOption(item.LabelShortCap + " " + "DownedLower".Translate(), null, item, Color.white));
                                    }
                                    else
                                    {
                                        string text = item.LabelShortCap + " 主持仪式".Translate();
                                        list.Add(new FloatMenuOption(text, delegate
                                        {
                                            //Find.WindowStack.Add(new Dialog_CreateXenogerm(this));
                                            item.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DDJY_JobDefOf.DDJY_GeneAssembler, this, pawn), JobTag.Misc);
                                        }, item, Color.white));
                                    }
                                }
                            }
                        }
                        if (!list.Any())
                        {
                            list.Add(new FloatMenuOption("没有合适的主持人 ".Translate(), null));
                        }
                        Find.WindowStack.Add(new FloatMenu(list));
                    };
                    //禁用按钮
                    if (!acceptanceReport1.Accepted)
                    {
                        command_Action5.Disable(acceptanceReport1.Reason);
                        command_Action6.Disable(acceptanceReport1.Reason);
                    }

                    yield return command_Action5;
                    yield return command_Action6;
                    yield return command_Action7;
                }
                yield break;
            }

            //置入人员
            Command_Action command_Action4 = new Command_Action();
            command_Action4.defaultLabel = "InsertPerson".Translate() + "...";
            command_Action4.defaultDesc = "InsertPersonGeneExtractorDesc".Translate();
            command_Action4.icon = InsertPawnTex;
            command_Action4.action = delegate
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
                if (!list.Any())
                {
                    list.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));
                }

                Find.WindowStack.Add(new FloatMenu(list));
            };
            yield return command_Action4;
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

                text = text + "ExtractingXenogermFrom".Translate(ContainedPawn.Named("PAWN")).Resolve();
            }

            return text;
        }
        
        // 保存和加载游戏
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref actor, "DDJY_actor");
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

    }
}