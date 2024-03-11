using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;


namespace DDJY
{
    public class Dialog_CreateXenogerm : GeneCreationDialogBase
    {
        private Building_TransmutationCircle TransmutationCircle;

        private List<Genepack> libraryGenepacks = new List<Genepack>();

        private List<Genepack> selectedGenepacks = new List<Genepack>();

        private HashSet<Genepack> matchingGenepacks = new HashSet<Genepack>();

        private List<GeneDef> tmpGenes = new List<GeneDef>();

        private Pawn acter;

        private Pawn containedPawn;

        private List<Genepack> containedPawnpawnEndogenes = new List<Genepack>();

        protected float endogenesSelectedHeight;

        private List<Genepack> containedPawnpawnXenogenes = new List<Genepack>();

        private bool inheritable;

        private bool selfGenomeEdit;


        private CompGeneAssembler compGeneAssembler => TransmutationCircle.TryGetComp<CompGeneAssembler>();

        public override Vector2 InitialSize => new Vector2(1016f, UI.screenHeight);

        protected override string Header => "DDJY_AssembleGenes".Translate();

        protected override string AcceptButtonLabel => "DDJY_StartCombining".Translate();

        //选择的基因列表
        protected override List<GeneDef> SelectedGenes
        {
            get
            {
                tmpGenes.Clear();
                foreach (Genepack selectedGenepack in selectedGenepacks)
                {
                    foreach (GeneDef item in selectedGenepack.GeneSet.GenesListForReading)
                    {
                        tmpGenes.Add(item);
                    }
                }

                return tmpGenes;
            }
        }

        //初始化Class
        public Dialog_CreateXenogerm(Building_TransmutationCircle TransmutationCircle, Pawn acter, Pawn containedPawn)
        {
            this.TransmutationCircle = TransmutationCircle;
            this.acter = acter;
            this.containedPawn = containedPawn;

            maxGCX = compGeneAssembler.MaxComplexity();
            libraryGenepacks.AddRange(compGeneAssembler.GetGenepacks(includePowered: true, includeUnpowered: true));
            xenotypeName = string.Empty;
            closeOnAccept = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            inheritable = false;
            selfGenomeEdit = false;
            searchWidgetOffsetX = GeneCreationDialogBase.ButSize.x * 2f + 4f;
            libraryGenepacks.SortGenepacks();
            containedPawnpawnEndogenes.AddRange(GeneListToGenepackList(containedPawn?.genes?.Endogenes));
            containedPawnpawnXenogenes.AddRange(GeneListToGenepackList(containedPawn?.genes?.Xenogenes));
        }

        //打开窗口时调用，检查是否有Dlc
        public override void PostOpen()
        {
            if (!ModLister.CheckBiotech("gene assembly"))
            {
                Close(doCloseSound: false);
            }
            else
            {
                base.PostOpen();
            }
        }
        

        //检查如果正在工作，弹出窗口询问是否继续，否则创建一个新的工作，！！！！！！！！！！   点击确认按钮后调用    ！！！！！！！
        protected override void Accept()
        {
            StartAssembly();
        }

        //启动基因组装过程，播放开始重新组合的声音，并关闭当前的窗口。 调用 geneAssembler.star（）启动
        private void StartAssembly()
        {
            compGeneAssembler.Start(selectedGenepacks, arc, xenotypeName?.Trim(), iconDef, inheritable, acter);
            SoundDefOf.StartRecombining.PlayOneShotOnCamera();
            Close(doCloseSound: false);
        }
        
        //绘制选择基因窗口
        protected override void DrawGenes(Rect rect)
        {
            GUI.BeginGroup(rect);
            Rect rect2 = new Rect(0f, 0f, rect.width - 16f, scrollHeight);
            float curY = 0f;
            Widgets.BeginScrollView(rect.AtZero(), ref scrollPosition, rect2);
            Rect containingRect = rect2;
            containingRect.y = scrollPosition.y;
            containingRect.height = rect.height;
            DrawSection(rect, selectedGenepacks, "DDJY_SelectedGenepacks".Translate(), ref curY, ref selectedHeight, adding: false, containingRect);
            if (selfGenomeEdit)
            {
                if (inheritable)
                {
                    curY += 8f;
                    DrawSection(rect, containedPawnpawnEndogenes, "Endogenes".Translate(), ref curY, ref endogenesSelectedHeight, adding: true, containingRect);
                }
                else
                {
                    curY += 8f;
                    DrawSection(rect, containedPawnpawnXenogenes, "Xenogenes".Translate(), ref curY, ref endogenesSelectedHeight, adding: true, containingRect);
                }
            }
            curY += 8f;
            DrawSection(rect, libraryGenepacks, "DDJY_GenepackLibrary".Translate(), ref curY, ref unselectedHeight, adding: true, containingRect);
            if ((int)Event.current.type == 8)
            {
                scrollHeight = curY;
            }

            Widgets.EndScrollView();
            GUI.EndGroup();
        }

        //负责绘制基因包列表，并处理基因包的选择和操作
        private void DrawSection(Rect rect, List<Genepack> genepacks, string label, ref float curY, ref float sectionHeight, bool adding, Rect containingRect)
        {
            float curX = 4f;
            Rect rect2 = new Rect(10f, curY, rect.width - 16f - 10f, Text.LineHeight);
            Widgets.Label(rect2, label);
            if (!adding)
            {
                Text.Anchor = (TextAnchor)2;
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(rect2, "ClickToAddOrRemove".Translate());
                GUI.color = Color.white;
                Text.Anchor = (TextAnchor)0;
            }
            
            curY += Text.LineHeight + 3f;
            float num = curY;
            Rect rect3 = new Rect(0f, curY, rect.width, sectionHeight);
            Widgets.DrawRectFast(rect3, Widgets.MenuSectionBGFillColor);
            curY += 4f;
            if (!genepacks.Any())
            {
                Text.Anchor = (TextAnchor)4;
                GUI.color = ColoredText.SubtleGrayColor;
                Widgets.Label(rect3, "(" + "NoneLower".Translate() + ")");
                GUI.color = Color.white;
                Text.Anchor = (TextAnchor)0;
            }
            else
            {
                for (int i = 0; i < genepacks.Count; i++)
                {
                    Genepack genepack = genepacks[i];
                    if (quickSearchWidget.filter.Active && (!matchingGenepacks.Contains(genepack) || (adding && selectedGenepacks.Contains(genepack))))
                    {
                        continue;
                    }

                    float num2 = 34f + GeneCreationDialogBase.GeneSize.x * (float)genepack.GeneSet.GenesListForReading.Count + 4f * (float)(genepack.GeneSet.GenesListForReading.Count + 2);
                    if (curX + num2 > rect.width - 16f)
                    {
                        curX = 4f;
                        curY += GeneCreationDialogBase.GeneSize.y + 8f + 14f;
                    }

                    if (adding && selectedGenepacks.Contains(genepack))
                    {
                        Widgets.DrawLightHighlight(new Rect(curX, curY, num2, GeneCreationDialogBase.GeneSize.y + 8f));
                        curX += num2 + 14f;
                    }
                    else if (DrawGenepack(genepack, ref curX, curY, num2, containingRect))
                    {
                        if (adding)
                        {
                            SoundDefOf.Tick_High.PlayOneShotOnCamera();
                            selectedGenepacks.Add(genepack);
                        }
                        else
                        {
                            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                            selectedGenepacks.Remove(genepack);
                        }

                        if (!xenotypeNameLocked)
                        {
                            xenotypeName = GeneUtility.GenerateXenotypeNameFromGenes(SelectedGenes);
                        }

                        OnGenesChanged();
                        break;
                    }
                }
            }

            curY += GeneCreationDialogBase.GeneSize.y + 12f;
            if ((int)Event.current.type == 8)
            {
                sectionHeight = curY - num;
            }
        }

        //绘制基因包
        private bool DrawGenepack(Genepack genepack, ref float curX, float curY, float packWidth, Rect containingRect)
        {
            bool result = false;
            if (genepack.GeneSet == null || genepack.GeneSet.GenesListForReading.NullOrEmpty())
            {
                return result;
            }

            Rect rect = new Rect(curX, curY, packWidth, GeneCreationDialogBase.GeneSize.y + 8f);
            if (!containingRect.Overlaps(rect))
            {
                curX = rect.xMax + 14f;
                return false;
            }

            Widgets.DrawHighlight(rect);
            GUI.color = GeneCreationDialogBase.OutlineColorUnselected;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
            curX += 4f;
            GeneUIUtility.DrawBiostats(genepack.GeneSet.ComplexityTotal, genepack.GeneSet.MetabolismTotal, genepack.GeneSet.ArchitesTotal, ref curX, curY, 4f);
            List<GeneDef> genesListForReading = genepack.GeneSet.GenesListForReading;
            for (int i = 0; i < genesListForReading.Count; i++)
            {
                GeneDef gene = genesListForReading[i];
                bool num = quickSearchWidget.filter.Active && matchingGenes.Contains(gene) && matchingGenepacks.Contains(genepack);
                bool overridden = leftChosenGroups.Any((GeneLeftChosenGroup x) => x.overriddenGenes.Contains(gene));
                Rect rect2 = new Rect(curX, curY + 4f, GeneCreationDialogBase.GeneSize.x, GeneCreationDialogBase.GeneSize.y);
                if (num)
                {
                    Widgets.DrawStrongHighlight(rect2.ExpandedBy(6f));
                }

                string extraTooltip = null;
                if (leftChosenGroups.Any((GeneLeftChosenGroup x) => x.leftChosen == gene))
                {
                    extraTooltip = GroupInfo(leftChosenGroups.FirstOrDefault((GeneLeftChosenGroup x) => x.leftChosen == gene));
                }
                else if (cachedOverriddenGenes.Contains(gene))
                {
                    extraTooltip = GroupInfo(leftChosenGroups.FirstOrDefault((GeneLeftChosenGroup x) => x.overriddenGenes.Contains(gene)));
                }
                else if (randomChosenGroups.ContainsKey(gene))
                {
                    extraTooltip = ("GeneWillBeRandomChosen".Translate() + ":\n" + randomChosenGroups[gene].Select((GeneDef x) => x.label).ToLineList("  - ", capitalizeItems: true)).Colorize(ColoredText.TipSectionTitleColor);
                }

                GeneUIUtility.DrawGeneDef_NewTemp(genesListForReading[i], rect2, this.inheritable ? GeneType.Endogene : GeneType.Xenogene, () => extraTooltip, doBackground: false, clickable: false, overridden);
                curX += GeneCreationDialogBase.GeneSize.x + 4f;
            }

            Widgets.InfoCardButton(rect.xMax - 24f, rect.y + 2f, genepack);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            if ((int)Event.current.type == 0 && Mouse.IsOver(rect) && Event.current.button == 1)
            {
                List<FloatMenuOption> list = new List<FloatMenuOption>();
                list.Add(new FloatMenuOption("EjectGenepackFromGeneBank".Translate(), delegate
                {
                    CompGenepackContainer geneBankHoldingPack = compGeneAssembler.GetGeneBankHoldingPack(genepack);
                    if (geneBankHoldingPack != null)
                    {
                        ThingWithComps parent = geneBankHoldingPack.parent;
                        if (geneBankHoldingPack.innerContainer.TryDrop(genepack, parent.def.hasInteractionCell ? parent.InteractionCell : parent.Position, parent.Map, ThingPlaceMode.Near, 1, out var _))
                        {
                            if (selectedGenepacks.Contains(genepack))
                            {
                                selectedGenepacks.Remove(genepack);
                            }

                            tmpGenes.Clear();
                            libraryGenepacks.Clear();
                            matchingGenepacks.Clear();
                            libraryGenepacks.AddRange(compGeneAssembler.GetGenepacks(includePowered: true, includeUnpowered: true));
                            libraryGenepacks.SortGenepacks();
                            OnGenesChanged();
                        }
                    }
                }));
                Find.WindowStack.Add(new FloatMenu(list));
            }
            else if (Widgets.ButtonInvisible(rect))
            {
                result = true;
            }

            curX = Mathf.Max(curX, rect.xMax + 14f);
            return result;
            string GroupInfo(GeneLeftChosenGroup group)
            {
                if (group == null)
                {
                    return null;
                }

                return ("GeneOneActive".Translate() + ":\n  - " + group.leftChosen.LabelCap + " (" + "Active".Translate() + ")" + "\n" + group.overriddenGenes.Select((GeneDef x) => (x.label + " (" + "Suppressed".Translate() + ")").Colorize(ColorLibrary.RedReadable)).ToLineList("  - ", capitalizeItems: true)).Colorize(ColoredText.TipSectionTitleColor);
            }
        }

        //绘制加载与保存按钮
        protected override void DrawSearchRect(Rect rect)
        {
            base.DrawSearchRect(rect);
            if (Widgets.ButtonText(new Rect(rect.xMax - GeneCreationDialogBase.ButSize.x, rect.y, GeneCreationDialogBase.ButSize.x, GeneCreationDialogBase.ButSize.y), "LoadXenogermTemplate".Translate()))
            {
                Find.WindowStack.Add(new Dialog_XenogermList_Load(delegate (CustomXenogerm xenogerm)
                {
                    xenotypeName = xenogerm.name;
                    xenotypeNameLocked = true;
                    iconDef = xenogerm.iconDef;
                    IEnumerable<Genepack> collection = CustomXenogermUtility.GetMatchingGenepacks(xenogerm.genesets, libraryGenepacks);
                    selectedGenepacks.Clear();
                    selectedGenepacks.AddRange(collection);
                    OnGenesChanged();
                    IEnumerable<GeneSet> source = xenogerm.genesets.Where((GeneSet gp) => !selectedGenepacks.Any((Genepack g) => g.GeneSet.Matches(gp)));
                    if (source.Any())
                    {
                        string text = null;
                        int num = source.Count();
                        if (num == 1)
                        {
                            text = "MissingGenepackForXenogerm".Translate(xenogerm.name.Named("NAME"));
                            text = text + ": " + source.Select((GeneSet g) => g.Label).ToCommaList().CapitalizeFirst();
                        }
                        else
                        {
                            text = "MissingGenepacksForXenogerm".Translate(num.Named("COUNT"), xenogerm.name.Named("NAME"));
                        }

                        Messages.Message(text, null, MessageTypeDefOf.CautionInput, historical: false);
                    }
                }));
            }

            if (Widgets.ButtonText(new Rect(rect.xMax - GeneCreationDialogBase.ButSize.x * 2f - 4f, rect.y, GeneCreationDialogBase.ButSize.x, GeneCreationDialogBase.ButSize.y), "SaveXenogermTemplate".Translate()))
            {
                AcceptanceReport acceptanceReport = CustomXenogermUtility.SaveXenogermTemplate(xenotypeName, iconDef, selectedGenepacks);
                if (!acceptanceReport.Reason.NullOrEmpty())
                {
                    Messages.Message(acceptanceReport.Reason, MessageTypeDefOf.RejectInput, historical: false);
                }
            }
        }
        
        //是否可以执行“接受”操作
        protected override bool CanAccept()
        {
            if (!base.CanAccept())
            {
                return false;
            }

            if (!selectedGenepacks.Any())
            {
                Messages.Message("MessageNoSelectedGenepacks".Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            if (this.arc > 0 && this.inheritable)
            {
                Messages.Message("DDJY_ArchiteSoulsCannotBeInherited".Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            if (arc > 0 && !DDJY_ResearchProjectDefOf.DDJY_ArchiteSoulAlchemy.IsFinished)
            {
                Messages.Message("AssemblingRequiresResearch".Translate(DDJY_ResearchProjectDefOf.DDJY_ArchiteSoulAlchemy), null, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            if (gcx > maxGCX)
            {
                Messages.Message("ComplexityTooHighToCreateXenogerm".Translate(gcx.Named("AMOUNT"), maxGCX.Named("MAX")), null, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            if (!ColonyHasEnoughArchites())
            {
                Messages.Message("NotEnoughArchites".Translate(), null, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            return true;
        }

        //检测超凡胶囊
        private bool ColonyHasEnoughArchites()
        {
            if (arc == 0 || TransmutationCircle.MapHeld == null)
            {
                return true;
            }

            List<Thing> list = TransmutationCircle.MapHeld.listerThings.ThingsOfDef(ThingDefOf.ArchiteCapsule);
            int num = 0;
            foreach (Thing item in list)
            {
                if (!item.Position.Fogged(TransmutationCircle.MapHeld))
                {
                    num += item.stackCount;
                    if (num >= arc)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        //搜索过滤器
        protected override void UpdateSearchResults()
        {
            quickSearchWidget.noResultsMatched = false;
            matchingGenepacks.Clear();
            matchingGenes.Clear();
            if (!quickSearchWidget.filter.Active)
            {
                return;
            }
            
            foreach (Genepack selectedGenepack in selectedGenepacks)
            {
                List<GeneDef> genesListForReading = selectedGenepack.GeneSet.GenesListForReading;
                for (int i = 0; i < genesListForReading.Count; i++)
                {
                    if (quickSearchWidget.filter.Matches(genesListForReading[i].label))
                    {
                        matchingGenepacks.Add(selectedGenepack);
                        matchingGenes.Add(genesListForReading[i]);
                    }
                }
            }

            foreach (Genepack libraryGenepack in libraryGenepacks)
            {
                if (selectedGenepacks.Contains(libraryGenepack))
                {
                    continue;
                }

                List<GeneDef> genesListForReading2 = libraryGenepack.GeneSet.GenesListForReading;
                for (int j = 0; j < genesListForReading2.Count; j++)
                {
                    if (quickSearchWidget.filter.Matches(genesListForReading2[j].label))
                    {
                        matchingGenepacks.Add(libraryGenepack);
                        matchingGenes.Add(genesListForReading2[j]);
                    }
                }
            }

            quickSearchWidget.noResultsMatched = !matchingGenepacks.Any();
        }

        //可遗传和自身基因编辑按钮
        protected override void PostXenotypeOnGUI(float curX, float curY)
        {
            TaggedString taggedString = "DDJY_InheritableSoul".Translate();
            TaggedString taggedString2 = "DDJY_SelfGenomeEdit".Translate();
            float width = Math.Max(Text.CalcSize(taggedString).x, Text.CalcSize(taggedString2).x ) + 4f + 24f;
            Rect rect = new Rect(curX, curY, width, Text.LineHeight);
            InheritableCheckboxLabeled(rect, taggedString, ref inheritable, !DDJY_ResearchProjectDefOf.DDJY_InheritableSoul.IsFinished, null, null, false);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, "DDJY_InheritableSoulDesc".Translate());
            }
            rect.y += Text.LineHeight;
            SelfGenomeEditCheckboxLabeled(rect, taggedString2, ref selfGenomeEdit, !DDJY_ResearchProjectDefOf.DDJY_SelfSoulEdit.IsFinished, null, null, false);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, "DDJY_SelfGenomeEditDesc".Translate());
            }
            this.postXenotypeHeight += rect.yMax - curY;
        }

        //可遗传基因按钮
        private void InheritableCheckboxLabeled(Rect rect, string label, ref bool checkOn, bool disabled = false, Texture2D texChecked = null, Texture2D texUnchecked = null, bool placeCheckboxNearText = false)
        {
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (placeCheckboxNearText)
            {
                rect.width = Mathf.Min(rect.width, Text.CalcSize(label).x + 24f + 10f);
            }

            Rect rect2 = rect;
            rect2.xMax -= 24f;
            Widgets.Label(rect2, label);
            if (!disabled && Widgets.ButtonInvisible(rect))
            {
                checkOn = !checkOn;
                if (checkOn)
                {
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                }
                else
                {
                    SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                }
                if (selfGenomeEdit)
                {
                    selectedGenepacks.Clear();
                    List<Genepack> list = new List<Genepack>();
                    if (checkOn)
                    {
                        list = containedPawnpawnEndogenes;
                    }
                    else
                    {
                        list = containedPawnpawnXenogenes;
                    }
                    foreach (Genepack genepack in list)
                    {
                        selectedGenepacks.Add(genepack);
                    }
                    OnGenesChanged();
                }
            }

            Widgets.CheckboxDraw(rect.x + rect.width - 24f, rect.y + (rect.height - 24f) / 2f, checkOn, disabled);
            Text.Anchor = anchor;
        }

        //自身基因编辑按钮
        private void SelfGenomeEditCheckboxLabeled(Rect rect, string label, ref bool checkOn, bool disabled = false, Texture2D texChecked = null, Texture2D texUnchecked = null, bool placeCheckboxNearText = false)
        {
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (placeCheckboxNearText)
            {
                rect.width = Mathf.Min(rect.width, Text.CalcSize(label).x + 24f + 10f);
            }

            Rect rect2 = rect;
            rect2.xMax -= 24f;
            Widgets.Label(rect2, label);
            if (!disabled && Widgets.ButtonInvisible(rect))
            {
                checkOn = !checkOn;
                if (checkOn)
                {
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                    selectedGenepacks.Clear();
                    List<Genepack> list = new List<Genepack>();
                    if (inheritable)
                    {
                        list = containedPawnpawnEndogenes;
                    }
                    else
                    {
                        list = containedPawnpawnXenogenes;
                    }
                    foreach (Genepack genepack in list)
                    {
                        selectedGenepacks.Add(genepack);
                    }
                }
                else
                {
                    SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                    selectedGenepacks.Clear();
                }
                OnGenesChanged();
            }

            Widgets.CheckboxDraw(rect.x + rect.width - 24f, rect.y + (rect.height - 24f) / 2f, checkOn, disabled);
            Text.Anchor = anchor;
        }
        
        //基因转换为基因包
        private Genepack GeneToGenepack(Gene gene)
        {
            List<GeneDef> genesList = new List<GeneDef>();
            genesList.Add(gene.def);
            Genepack genepack = (Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);
            genepack.Initialize(genesList);
            return genepack;
        }

        //系谱基因转基因包列表
        private List<Genepack> GeneListToGenepackList(List<Gene> geneList)
        {
            List<Genepack> GenepackList = new List<Genepack>();
            if(geneList != null && geneList.Any()) 
            { 
                foreach (Gene gene in geneList)
                {
                    GenepackList.Add(GeneToGenepack(gene));
                }
            }
            return GenepackList;
        }

        //基因改变时调用
        protected override void OnGenesChanged()
        {
            base.OnGenesChanged();

            if (selfGenomeEdit)
            {
                List<Genepack> geneList = new List<Genepack>();
                if (inheritable)
                {
                    geneList = containedPawnpawnEndogenes;
                }
                else
                {
                    geneList = containedPawnpawnXenogenes;
                }

                foreach (Genepack genePack in geneList)
                {
                    if (selectedGenepacks.Contains(genePack))
                    {
                        arc -= genePack.GeneSet.ArchitesTotal;
                        gcx -= genePack.GeneSet.ComplexityTotal;
                    }
                }
            }
        }
    }
}
