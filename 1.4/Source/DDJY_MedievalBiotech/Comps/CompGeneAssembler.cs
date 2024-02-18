using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace DDJY
{
    public class CompGeneAssembler : ThingComp
    {
        private Building_TransmutationCircle transmutationCircle => parent as Building_TransmutationCircle;
        //容器内物品
        public ThingOwner innerContainer => transmutationCircle.innerContainer;

        //工作执行人
        private Pawn actor = null;

        //新的异种基因名称
        public string xenotypeName;

        //需要的超凡胶囊数量
        public int architesRequired;

        //新的异种基因图标
        public XenotypeIconDef iconDef;

        //获取到的基因
        public List<Genepack> genepacksToRecombine;

        //连接设备存储的基因包
        [Unsaved(false)]
        private List<Genepack> tmpGenepacks = new List<Genepack>();
        
        //缓存的基因复杂度
        [Unsaved(false)]
        private int? cachedComplexity;

        //连接设备的列表
        public List<Thing> ConnectedFacilities => parent.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading;

        //最大复杂性
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

        //获取连接设备存储的基因包
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

        //获取存着基因包的建筑
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

        //开始
        public void Start(List<Genepack> packs, int architesRequired, string xenotypeName, XenotypeIconDef iconDef)
        {
            Reset();
            this.actor = transmutationCircle.actor;
            this.genepacksToRecombine = packs;
            this.architesRequired = architesRequired;
            this.xenotypeName = xenotypeName;
            this.iconDef = iconDef;
            SelectJob();
        }

        //重置
        public void Reset()
        {   
            actor = null;
            genepacksToRecombine = null;
            xenotypeName = null;
            cachedComplexity = null;
            iconDef = XenotypeIconDefOf.Basic;
            architesRequired = 0;
        }

        //检查正在重组的基因是否在基因库
        public bool CheckAllContainersValid()
        {
            if (genepacksToRecombine.NullOrEmpty())
            {
                return false;
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
                    Messages.Message("MessageXenogermCancelledMissingPack".Translate(this.parent), this.parent, MessageTypeDefOf.NegativeEvent);
                    return false;
                }
            }
            return true;
        }

        //容器内的超凡胶囊数量
        public int ArchitesCount
        {
            get
            {
                int num = 0;
                ThingOwner innerContainer = transmutationCircle.innerContainer;
                if (innerContainer != null)
                {
                    for (int i = 0; i < innerContainer.Count; i++)
                    {
                        if (innerContainer[i].def == ThingDefOf.ArchiteCapsule)
                        {
                            num += innerContainer[i].stackCount;
                        }
                    }
                }

                return num;
            }
        }

        //需要的超凡胶囊数量
        public int ArchitesRequiredNow => architesRequired - ArchitesCount;

        //选择工作
        public void SelectJob()
        {
            Building_TransmutationCircle t = transmutationCircle;
            if (ArchitesRequiredNow > 0)
            {
                Thing thing = FindArchiteCapsule(actor);
                if (thing != null)
                {
                    Job job = JobMaker.MakeJob(DDJY_JobDefOf.DDJY_HaulToContainer, thing, t);
                    job.count = Mathf.Min(ArchitesRequiredNow, thing.stackCount);
                    actor.jobs.TryTakeOrderedJob(job);
                    return;
                }
            }
            else
            {
                actor.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DDJY_JobDefOf.DDJY_CreateXenogerm, t));
                return;
            }
        }

        //寻找最短路径
        private Thing FindArchiteCapsule(Pawn pawn)
        {
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(ThingDefOf.ArchiteCapsule), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false), null, 0, -1, false, RegionType.Set_Passable, false);
        }
    }

}