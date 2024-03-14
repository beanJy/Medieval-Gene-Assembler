using RimWorld;
using UnityEngine;
using Verse;

namespace DDJY
{
    [StaticConstructorOnStartup]
    public class CompDarklightOverlay : CompFireOverlayBase
    {
        
        public static readonly Graphic DarklightGraphic = GraphicDatabase.Get<Graphic_Flicker>("Things/Special/Darklight", ShaderDatabase.TransparentPostLight, Vector2.one, Color.white);

        public new CompProperties_DarklightOverlay Props => (CompProperties_DarklightOverlay)props;

        public bool IsActive = false;
        public override void PostDraw()
        {
            base.PostDraw();
            if (IsActive)
            {
                Vector3 drawPos = parent.DrawPos;
                drawPos.y += 3f / 74f;
                DarklightGraphic.Draw(drawPos, Rot4.North, parent);
            }
        }
    }
}