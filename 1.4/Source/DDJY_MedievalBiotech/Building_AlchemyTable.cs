using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;

namespace DDJY
{
    [StaticConstructorOnStartup]
    public class Building_AlchemyTable :  Building, IThingHolder 
    { 
        public ThingOwner innerContainer;

        //表示需要重新组合的基因包列表
        private List<Genepack> genepacksToRecombine;

        //需要的超凡基因
        private int architesRequired;

        //工作状态
        private bool workingInt;

        //表示上次工作的时间戳
        private int lastWorkedTick = -999;

        //已完成的工作量
        private float workDone;

        //总共需要完成的工作量
        private float totalWorkRequired;

        //异种名字
        public string xenotypeName;

        //异种图标
        public XenotypeIconDef iconDef;

        //记录上次的工作量
        [Unsaved(false)]
        private float lastWorkAmount = -1f;

        //临时存储基因包
        [Unsaved(false)]
        private List<Genepack> tmpGenepacks = new List<Genepack>();

        //临时存储被使用的设施
        [Unsaved(false)]
        private HashSet<Thing> tmpUsedFacilities = new HashSet<Thing>();

        //缓存复杂度值
        [Unsaved(false)]
        private int? cachedComplexity;

        private const int CheckContainersInterval = 180;

        private static readonly Texture2D CancelIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        private static readonly CachedTexture RecombineIcon = new CachedTexture("UI/Gizmos/RecombineGenes");


        //返回当前的工作进度百分比
        public float ProgressPercent => workDone / totalWorkRequired;

        //是否在工作
        public bool Working => workingInt;

        //连接设备的列表
        public List<Thing> ConnectedFacilities => this.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading;

        //容器内的超凡胶囊数量
        public int ArchitesCount
        {
            get
            {
                int num = 0;
                for (int i = 0; i < innerContainer.Count; i++)
                {
                    if (innerContainer[i].def == ThingDefOf.ArchiteCapsule)
                    {
                        num += innerContainer[i].stackCount;
                    }
                }

                return num;
            }
        }

        //需要的超凡胶囊数量
        public int ArchitesRequiredNow => architesRequired - ArchitesCount;

        //连接的设施中正在使用的设施
        private HashSet<Thing> UsedFacilities
        {
            get
            {
                tmpUsedFacilities.Clear();
                if (!genepacksToRecombine.NullOrEmpty())
                {
                    List<Thing> connectedFacilities = ConnectedFacilities;
                    for (int i = 0; i < genepacksToRecombine.Count; i++)
                    {
                        for (int j = 0; j < connectedFacilities.Count; j++)
                        {
                            if (!tmpUsedFacilities.Contains(connectedFacilities[j]))
                            {
                                CompGenepackContainer compGenepackContainer = connectedFacilities[j].TryGetComp<CompGenepackContainer>();
                                if (compGenepackContainer != null && compGenepackContainer.ContainedGenepacks.Contains(genepacksToRecombine[i]))
                                {
                                    tmpUsedFacilities.Add(connectedFacilities[j]);
                                    break;
                                }
                            }
                        }
                    }
                }

                return tmpUsedFacilities;
            }
        }

        //处理器是否可以开始工作
        public AcceptanceReport CanBeWorkedOnNow
        {
            get
            {
                if (!Working)
                {
                    return false;
                }

                if (ArchitesRequiredNow > 0)
                {
                    return false;
                }

                if (MaxComplexity() < TotalGCX)
                {
                    return "GeneProcessorUnpowered".Translate();
                }

                return true;
            }
        }

        //计算当前基因处理器中所有基因的总复杂度
        private int TotalGCX
        {
            get
            {
                if (!Working)
                {
                    return 0;
                }

                if (!cachedComplexity.HasValue)
                {
                    cachedComplexity = 0;
                    if (!genepacksToRecombine.NullOrEmpty())
                    {
                        List<GeneDefWithType> list = new List<GeneDefWithType>();
                        for (int i = 0; i < genepacksToRecombine.Count; i++)
                        {
                            if (genepacksToRecombine[i].GeneSet != null)
                            {
                                for (int j = 0; j < genepacksToRecombine[i].GeneSet.GenesListForReading.Count; j++)
                                {
                                    list.Add(new GeneDefWithType(genepacksToRecombine[i].GeneSet.GenesListForReading[j], xenogene: true));
                                }
                            }
                        }

                        List<GeneDef> list2 = list.NonOverriddenGenes();
                        for (int k = 0; k < list2.Count; k++)
                        {
                            cachedComplexity += list2[k].biostatCpx;
                        }
                    }
                }

                return cachedComplexity.Value;
            }
        }

        //创建对象后立即调用
        public override void PostPostMake()
        {
            if (!ModLister.CheckBiotech("Gene assembler"))
            {
                Destroy();
                return;
            }

            base.PostPostMake();
            innerContainer = new ThingOwner<Thing>(this);
        }


        //!!!!!!定时执行检查
        public override void Tick()
        {
            base.Tick();
            this.innerContainer.ThingOwnerTick(true);

            if (Working && this.IsHashIntervalTick(180))
            {
                CheckAllContainersValid();
            }
        }
        public void Start(List<Genepack> packs, int architesRequired, string xenotypeName, XenotypeIconDef iconDef)
        {
            Reset();
            genepacksToRecombine = packs;
            this.architesRequired = architesRequired;
            this.xenotypeName = xenotypeName;
            this.iconDef = iconDef;
            workingInt = true;
            totalWorkRequired = GeneTuning.ComplexityToCreationHoursCurve.Evaluate(TotalGCX) * 2500f;
        }

        //更新基因装配器的工作进度和记录最近的工作状态
        public void DoWork(float workAmount)
        {
            workDone += workAmount;
            lastWorkAmount = workAmount;
            lastWorkedTick = Find.TickManager.TicksGame;
        }

        public void Finish()
        {
            if (!genepacksToRecombine.NullOrEmpty())
            {
                SoundDefOf.GeneAssembler_Complete.PlayOneShot(SoundInfo.InMap(this));
                Xenogerm xenogerm = (Xenogerm)ThingMaker.MakeThing(ThingDefOf.Xenogerm);
                //创建异种注入器
                xenogerm.Initialize(genepacksToRecombine, xenotypeName, iconDef);
                if (GenPlace.TryPlaceThing(xenogerm, InteractionCell, base.Map, ThingPlaceMode.Near))
                {
                    Messages.Message("MessageXenogermCompleted".Translate(), xenogerm, MessageTypeDefOf.PositiveEvent);
                }
            }

            if (architesRequired > 0)
            {
                for (int num = innerContainer.Count - 1; num >= 0; num--)
                {
                    if (innerContainer[num].def == ThingDefOf.ArchiteCapsule)
                    {
                        Thing thing = innerContainer[num].SplitOff(Mathf.Min(innerContainer[num].stackCount, architesRequired));
                        architesRequired -= thing.stackCount;
                        thing.Destroy();
                        if (architesRequired <= 0)
                        {
                            break;
                        }
                    }
                }
            }

            Reset();
        }

        //返回与给定设施连接的所有基因包列表
        public List<Genepack> GetGenepacks(bool includePowered, bool includeUnpowered)
        {
            tmpGenepacks.Clear();
            List<Thing> connectedFacilities = ConnectedFacilities;
            if (connectedFacilities != null)
            {
                foreach (Thing item in connectedFacilities)
                {
                    CompGenepackContainer compGenepackContainer = item.TryGetComp<CompGenepackContainer>();
                    if (compGenepackContainer != null)
                    {
                            tmpGenepacks.AddRange(compGenepackContainer.ContainedGenepacks);
                    }
                }
            }

            return tmpGenepacks;
        }

        //查找持有特定基因包的基因库
        public CompGenepackContainer GetGeneBankHoldingPack(Genepack pack)
        {
            List<Thing> connectedFacilities = ConnectedFacilities;
            if (connectedFacilities != null)
            {
                foreach (Thing item in connectedFacilities)
                {
                    CompGenepackContainer compGenepackContainer = item.TryGetComp<CompGenepackContainer>();
                    if (compGenepackContainer == null)
                    {
                        continue;
                    }

                    foreach (Genepack containedGenepack in compGenepackContainer.ContainedGenepacks)
                    {
                        if (containedGenepack == pack)
                        {
                            return compGenepackContainer;
                        }
                    }
                }
            }

            return null;
        }

        //基因复杂度的最大值
        public int MaxComplexity()
        {
            int num = 6;
            List<Thing> connectedFacilities = ConnectedFacilities;
            if (connectedFacilities != null)
            {
                foreach (Thing item in connectedFacilities)
                {
                        num += (int)item.GetStatValue(StatDefOf.GeneticComplexityIncrease);
                }

                return num;
            }

            return num;
        }

        private void Reset()
        {
            workingInt = false;
            genepacksToRecombine = null;
            xenotypeName = null;
            cachedComplexity = null;
            iconDef = XenotypeIconDefOf.Basic;
            workDone = 0f;
            lastWorkedTick = -999;
            architesRequired = 0;
            innerContainer.TryDropAll(def.hasInteractionCell ? InteractionCell : base.Position, base.Map, ThingPlaceMode.Near);
        }

        //！！！！！！！检查正在重组的基因是否在基因库
        private void CheckAllContainersValid()
        {
            if (genepacksToRecombine.NullOrEmpty())
            {
                return;
            }

            List<Thing> connectedFacilities = ConnectedFacilities;
            for (int i = 0; i < genepacksToRecombine.Count; i++)
            {
                bool flag = false;
                for (int j = 0; j < connectedFacilities.Count; j++)
                {
                    CompGenepackContainer compGenepackContainer = connectedFacilities[j].TryGetComp<CompGenepackContainer>();
                    if (compGenepackContainer != null && compGenepackContainer.ContainedGenepacks.Contains(genepacksToRecombine[i]))
                    {
                        flag = true;
                        break;
                    }
                }

                if (!flag)
                {
                    Messages.Message("MessageXenogermCancelledMissingPack".Translate(this), this, MessageTypeDefOf.NegativeEvent);
                    Reset();
                    break;
                }
            }
        }

        //获取该对象持有的子对象的持有者列表
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        //管理该对象直接持有的物品
        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }
        //按钮
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            Command_Action command_Action = new Command_Action();
            command_Action.defaultLabel = "Recombine".Translate() + "...";
            command_Action.defaultDesc = "RecombineDesc".Translate();
            command_Action.icon = RecombineIcon.Texture;
            command_Action.action = delegate
            {
                //Find.WindowStack.Add(new Dialog_CreateXenogerm(this));
            };
            if (!def.IsResearchFinished)
            {
                command_Action.Disable("MissingRequiredResearch".Translate() + ": " + (from x in def.researchPrerequisites
                                                                                       where !x.IsFinished
                                                                                       select x.label).ToCommaList(useAnd: true).CapitalizeFirst());
            }
            else if (!GetGenepacks(includePowered: true, includeUnpowered: false).Any())
            {
                command_Action.Disable("CannotUseReason".Translate("NoGenepacksAvailable".Translate().CapitalizeFirst()));
            }

            yield return command_Action;
            if (!Working)
            {
                yield break;
            }

            Command_Action command_Action2 = new Command_Action();
            command_Action2.defaultLabel = "CancelXenogerm".Translate();
            command_Action2.defaultDesc = "CancelXenogermDesc".Translate();
            command_Action2.action = delegate
            {
                Reset();
            };
            command_Action2.icon = CancelIcon;
            yield return command_Action2;
            if (DebugSettings.ShowDevGizmos)
            {
                Command_Action command_Action3 = new Command_Action();
                command_Action3.defaultLabel = "DEV: Finish xenogerm";
                command_Action3.action = delegate
                {
                    Finish();
                };
                yield return command_Action3;
            }
        }

        //检查字符串
        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (Working)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }

                text = text + (string)("CreatingXenogerm".Translate() + ": " + xenotypeName.CapitalizeFirst() + "\n" + "ComplexityTotal".Translate() + ": ") + TotalGCX;
                text += "\n" + "Progress".Translate() + ": " + ProgressPercent.ToStringPercent();
                int numTicks = Mathf.RoundToInt((totalWorkRequired - workDone) / ((lastWorkAmount > 0f) ? lastWorkAmount : this.GetStatValue(StatDefOf.AssemblySpeedFactor)));
                text = text + " (" + "DurationLeft".Translate(numTicks.ToStringTicksToPeriod()).Resolve() + ")";
                AcceptanceReport canBeWorkedOnNow = CanBeWorkedOnNow;
                if (!canBeWorkedOnNow.Accepted && !canBeWorkedOnNow.Reason.NullOrEmpty())
                {
                    text = text + "\n" + ("AssemblyPaused".Translate() + ": " + canBeWorkedOnNow.Reason).Colorize(ColorLibrary.RedReadable);
                }

                if (architesRequired > 0)
                {
                    text = text + (string)("\n" + "ArchitesRequired".Translate() + ": ") + ArchitesCount + " / " + architesRequired;
                }
            }

            return text;
        }

        //存档
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Collections.Look(ref genepacksToRecombine, "genepacksToRecombine", LookMode.Reference);
            Scribe_Values.Look(ref workingInt, "workingInt", defaultValue: false);
            Scribe_Values.Look(ref workDone, "workDone", 0f);
            Scribe_Values.Look(ref totalWorkRequired, "totalWorkRequired", 0f);
            Scribe_Values.Look(ref lastWorkedTick, "lastWorkedTick", -999);
            Scribe_Values.Look(ref architesRequired, "architesRequired", 0);
            Scribe_Values.Look(ref xenotypeName, "xenotypeName");
            Scribe_Defs.Look(ref iconDef, "iconDef");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && iconDef == null)
            {
                iconDef = XenotypeIconDefOf.Basic;
            }
        }
    }
}
