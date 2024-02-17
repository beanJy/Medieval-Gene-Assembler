using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace DDJY
{
    public class CompGeneAssembler : ThingComp
    {
        //容器内物品
        //public ThingOwner innerContainer = (parent as Building_TransmutationCircle).innerContainer;

        //新的异种基因名称
        public string xenotypeName;

        //需要的超凡胶囊数量
        private int architesRequired;

        //新的异种基因图标
        public XenotypeIconDef iconDef;

        //获取到的基因
        private List<Genepack> genepacksToRecombine;

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

        //获取基因包
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
            genepacksToRecombine = packs;
            this.architesRequired = architesRequired;
            this.xenotypeName = xenotypeName;
            this.iconDef = iconDef;
        }

        //重置
        public void Reset()
        {
            genepacksToRecombine = null;
            xenotypeName = null;
            cachedComplexity = null;
            iconDef = XenotypeIconDefOf.Basic;
            architesRequired = 0;
            //innerContainer.TryDropAll(def.hasInteractionCell ? InteractionCell : base.Position, base.Map, ThingPlaceMode.Near);
        }

        //完成
        public void Finish(Pawn pawn)
        {
            if (!genepacksToRecombine.NullOrEmpty())
            {
                //SoundDefOf.GeneAssembler_Complete.PlayOneShot(SoundInfo.InMap(this));
                Xenogerm xenogerm = (Xenogerm)ThingMaker.MakeThing(ThingDefOf.Xenogerm);
                //创建异种注入器
                xenogerm.Initialize(genepacksToRecombine, xenotypeName, iconDef);
                GeneUtility.ImplantXenogermItem(pawn, xenogerm);
                //通知
                //if (GenPlace.TryPlaceThing(xenogerm, InteractionCell, base.Map, ThingPlaceMode.Near))
                //{
                //    Messages.Message("MessageXenogermCompleted".Translate(), xenogerm, MessageTypeDefOf.PositiveEvent);
                //}
            }

            //if (architesRequired > 0)
            //{
            //    for (int num = innerContainer.Count - 1; num >= 0; num--)
            //    {
            //        if (innerContainer[num].def == ThingDefOf.ArchiteCapsule)
            //        {
            //            Thing thing = innerContainer[num].SplitOff(Mathf.Min(innerContainer[num].stackCount, architesRequired));
            //            architesRequired -= thing.stackCount;
            //            thing.Destroy();
            //            if (architesRequired <= 0)
            //            {
            //                break;
            //            }
            //        }
            //    }
            //}

            Reset();
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
                ThingOwner innerContainer = (parent as Building_TransmutationCircle)?.innerContainer;
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
    }
}