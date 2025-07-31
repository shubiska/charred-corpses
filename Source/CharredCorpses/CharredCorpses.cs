using Verse;
using RimWorld;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace CharredCorpses
{
    [StaticConstructorOnStartup]
    public static class Init
    {
        static Init()
        {
            Harmony harmony = new Harmony("Xubisca.CharredCorpses");
            harmony.PatchAll();
        }
    }

    public static class Data
    {
        public static readonly Color charred = new Color(0.15f, 0.15f, 0.15f, 1f);
    }

    public class CompCharrable : ThingComp
    {
        public int Stage = 0;
        public float Severity = 0f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref Stage, "Stage", 0);
            Scribe_Values.Look(ref Severity, "Severity", 0f);
        }
    }

    [HarmonyPatch(typeof(Fire), "DoFireDamage")]
    public static class DoFireDamage_FirePatch
    {
        public static bool Prefix(Fire __instance, Thing targ)
        {
            if (!(targ is Corpse corpse) || __instance == null || __instance.Destroyed) return true;
            if (!(corpse?.InnerPawn is Pawn pawn)) return true;
            if (!(pawn.TryGetComp<CompCharrable>() is CompCharrable charrable)) return true;

            CompRottable rottable = corpse.TryGetComp<CompRottable>();

            if (rottable?.Stage == RotStage.Dessicated && charrable.Stage == 0) charrable.Severity = 0.5f;

            if (charrable.Stage == 2)
            {
                __instance.DeSpawn();
                return false;
            }

            charrable.Severity += 0.025f;

            if (charrable.Stage == 0 && charrable.Severity >= 0.5f)
            {
                charrable.Stage = 1;
                if (pawn.RaceProps.Humanlike)
                {
                    pawn.story.hairDef = DefDatabase<HairDef>.GetNamed("Bald");
                    pawn.story.HairColor = Data.charred;
                    pawn.style.beardDef = null;
                    pawn.story.skinColorOverride = Data.charred;
                    pawn.Drawer.renderer.SetAllGraphicsDirty();
                }
            }
            else if (charrable.Stage == 1 && charrable.Severity >= 1f)
            {
                charrable.Stage = 2;
                charrable.Severity = 1f;

                if (rottable != null && rottable.Stage != RotStage.Dessicated) rottable.RotProgress = rottable.PropsRot.TicksToDessicated + 10;

                if (pawn.RaceProps.Humanlike)
                {
                    List<Apparel> worn = pawn.apparel?.WornApparel;
                    if (worn != null)
                    {
                        foreach (Apparel gear in worn.ToList())
                        {
                            if (gear.GetStatValue(StatDefOf.Flammability) > 0.5f) gear.Destroy();
                        }
                    }
                }
            }

            if ((float)corpse.HitPoints / corpse.MaxHitPoints <= 0.5) return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnInternal")]
    public static class RenderPawnInternal_PawnRendererPatch
    {
        private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnField = AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");
        private static Color colorDefault = Color.white;
        private static bool shaded = false;
        private static Pawn pawn;

        public static void Prefix(PawnRenderer __instance)
        {
            pawn = PawnField(__instance);
            CompCharrable charrable = pawn.TryGetComp<CompCharrable>();
            if (charrable == null || charrable.Stage == 0) return;

            shaded = true;
            colorDefault = pawn.Drawer.renderer.BodyGraphic.MatSingle.color;

            if (pawn.RaceProps.Humanlike == false || (pawn.RaceProps.Humanlike && charrable.Stage == 2))
            {
                if (pawn.Drawer.renderer.BodyGraphic != null) pawn.Drawer.renderer.BodyGraphic.MatAt(pawn.Drawer.renderer.LayingFacing()).color = Data.charred;
                if (pawn.Drawer.renderer.HeadGraphic != null) pawn.Drawer.renderer.HeadGraphic.MatAt(pawn.Drawer.renderer.LayingFacing()).color = Data.charred;
            }
        }

        public static void Postfix()
        {
            if (!shaded) return;
            shaded = false;

            if (pawn.Drawer.renderer.BodyGraphic != null) pawn.Drawer.renderer.BodyGraphic.MatAt(pawn.Drawer.renderer.LayingFacing()).color = colorDefault;
            if (pawn.Drawer.renderer.HeadGraphic != null) pawn.Drawer.renderer.HeadGraphic.MatAt(pawn.Drawer.renderer.LayingFacing()).color = colorDefault;
        }
    }
}
