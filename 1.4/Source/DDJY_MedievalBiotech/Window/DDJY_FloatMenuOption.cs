using System;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DDJY
{
    [StaticConstructorOnStartup]
    public class DDJY_FloatMenuOption : FloatMenuOption
    {
        private Color color;
        public DDJY_FloatMenuOption(string label, Action action, Thing iconThing, Color iconColor, Color color, MenuOptionPriority priority = MenuOptionPriority.Default, Action<Rect> mouseoverGuiAction = null, Thing revalidateClickTarget = null, float extraPartWidth = 0f, Func<Rect, bool> extraPartOnGUI = null, WorldObject revalidateWorldClickTarget = null, bool playSelectionSound = true, int orderInPriority = 0)
        : base(label, action, iconThing, iconColor,priority, mouseoverGuiAction, revalidateClickTarget, extraPartWidth, extraPartOnGUI, revalidateWorldClickTarget, playSelectionSound, orderInPriority)
        {
            this.color = color;
        }

        public override bool DoGUI(Rect rect, bool colonistOrdering, FloatMenu floatMenu)
        {
            Color originalTextColor = GUI.color;
            GUI.color = color; 
            bool result = base.DoGUI(rect, colonistOrdering, floatMenu);
            GUI.color = originalTextColor;
            return result;
        }
    }

}
