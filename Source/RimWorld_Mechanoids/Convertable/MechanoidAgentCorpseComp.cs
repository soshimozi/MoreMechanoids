using UnityEngine;
using Verse;

namespace MoreMechanoids
{
    public class MechanoidAgentCorpseComp : ThingComp
    {
        private int raiseTicks;
        private float raiseVelocity;
        private int ticksToNextRepair;
        private const int TicksBetweenRepairs = 7;
        private const float AppearTicks = 70f;
        private bool showRealFace;
        private int appearTicks = 70;
        private Color originalSkinColor;

        private Pawn Pawn
        {
            get
            {
                var corpse = (Corpse) parent;
                var pawn = corpse.innerPawn;
                return pawn;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.LookValue(ref raiseTicks, "raiseTicks");
            Scribe_Values.LookValue(ref raiseVelocity, "raiseVelocity");
            Scribe_Values.LookValue(ref ticksToNextRepair, "ticksToNextRepair");
            Scribe_Values.LookValue(ref showRealFace, "showRealFace");
            Scribe_Values.LookValue(ref appearTicks, "appearTicks");
            Scribe_Values.LookValue(ref originalSkinColor, "originalSkinColor");
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            
            Log.Message("Created agent corpse");
            raiseTicks = Rand.Range(60*1, 60*4);
            Log.Message(parent.def.category.ToString());
            Log.Message("Class: " + parent.def.thingClass);
            Log.Message(parent.def.thingClass == typeof(Corpse) ? "Is corprse" : "Not corpse");

            //var smt = Type.GetType("EdB.Interface.SquadManagerThing,EdBInterface");
            //if (smt != null)
            //{
            //    var instance = smt.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
            //    var missingPawns = (List<Pawn>)smt.GetField("missingPawns", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
            //    Log.Message("Missing pawns: " + missingPawns.Count);
            //    var result = missingPawns.RemoveAll(p => p.ThingID == ThingID);
            //    Log.Message("Removed " + result + " elements with ID = " +ThingID);
            //}
        }

        public override void CompTick()
        {
            base.CompTick();
            raiseTicks--;
            if(raiseTicks <= 0)
            {
                TickHeal();
                if (!showRealFace || appearTicks > 0) ShowRealFace();
                if (Pawn.health.hediffSet.hediffs.Count <= 0)
                {
                    //if (!raisePosition.IsValid)
                    //{
                    //    raisePosition = CellFinder.RandomClosewalkCellNear(Position, 3);
                    //    Find.PawnDestinationManager.ReserveDestinationFor(innerPawn, raisePosition);
                    //    raisePosition.Walkable()
                    //}
                    var wiggler = Pawn.drawer.renderer.wiggler;
                    wiggler.SetToCustomRotation(Mathf.SmoothDampAngle(wiggler.downedAngle, 0, ref raiseVelocity, 1.2f));
                    if (wiggler.downedAngle < 1 || wiggler.downedAngle > 359)
                    {
                        wiggler.SetToCustomRotation(0);
                        CreateAgent();
                    }
                }}

        }

        private void TickHeal()
        {
            if (ticksToNextRepair-- > 0) return;
            ticksToNextRepair = TicksBetweenRepairs;

            if(Pawn.def.repairEffect!=null)
            Pawn.def.repairEffect.Spawn();

            // Injuries
            var damages = Pawn.health.hediffSet.hediffs;
            if (damages.Count == 0)
            {
                return;
            }
            var hediff = damages.RandomElement();
            var severity = hediff.Severity;
            Pawn.health.HealHediff(hediff, 1);

            // Healing had no effect? Remove eventually
            if (severity >= hediff.Severity)
            {
                if (Rand.Value < 0.10f)
                {
                    Pawn.health.RemoveHediff(hediff);
                }
            }
        }

        private void ShowRealFace()
        {
            if (Pawn == null) return;

            if (!showRealFace)
            {
                showRealFace = true;
                if (Pawn.story.skinColor == Color.white)
                {
                    appearTicks = 0;
                }
                else
                {
                    originalSkinColor = Pawn.story.skinColor;
                }
            }

            appearTicks--;
            if (appearTicks <= 0)
            {
                Pawn.story.skinColor = Color.white;
                Pawn.drawer.renderer.graphics.headGraphic =
                    GraphicDatabase.Get<Graphic_Multi>("Things/Pawn/Humanlike/Heads/Female/Female_Average_Agent",
                        ShaderDatabase.Cutout, Vector2.one, Color.white);
                Pawn.drawer.renderer.graphics.nakedGraphic.color = Color.white;
                Log.Message("Head fixed.");
            }
            else
            {
                var color = Color.Lerp(Color.white, originalSkinColor, appearTicks/AppearTicks);
                Pawn.story.skinColor = color;
                Pawn.drawer.renderer.graphics.headGraphic =
                    GraphicDatabase.Get<Graphic_Multi>(
                        "Things/Pawn/Humanlike/Heads/Female/Female_Average_Agent"
                        + Mathf.RoundToInt(1 + appearTicks/AppearTicks*6), ShaderDatabase.Cutout, Vector2.one, color);
                Pawn.drawer.renderer.graphics.nakedGraphic.color = color;
            }

            //Log.Message(innerPawn.story.headGraphicPath);
            //pawn.story.headGraphicPath = "Things/Pawn/Humanlike/Heads/Female/Female_Average_Agent";
            //pawn.drawer.renderer.graphics.ResolveAllGraphics();
        }

        private void CreateAgent()
        {
            //Thing thing;
            //GenPlace.TryPlaceThing(innerPawn, Position, ThingPlaceMode.Direct, out thing);
            //var raisedPawn = thing as MechanoidAgent;
            Log.Message("Creating agent.");
            parent.Destroy();
            MechanoidAgent.Discover(Pawn, parent.Position);
        }
    }
}