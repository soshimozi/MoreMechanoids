using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreMechanoids
{
    public class JobGiver_Standby : ThinkNode_JobGiver
    {
        public Danger maxDanger = Danger.None;
        public bool instantly;

        protected override Job TryGiveTerminalJob(Pawn pawn)
        {
            if (instantly)
            {
                return new Job(Defs.StandbyDef, pawn.Position);
            }
            Building_RestSpot restSpot = Building_RestSpot.Find(pawn);
            if (restSpot != null)
            {
                //if (spot.owner != pawn)
                //{
                //   spot.Claim(pawn as PawnConverted);
                //}
                return new Job(Defs.StandbyDef, restSpot);
            }

            {
                var restPos = GetRestSpot(pawn.Position, maxDanger, pawn);
                
                return new Job(Defs.StandbyDef, restPos);
            }
        }

        private static IntVec3 GetRestSpot(IntVec3 originCell, Danger maxDanger, Pawn me)
        {
            if (!(me is PawnConverted)) return originCell;

            var currentDanger = me.Position.GetDangerFor(me);
            var danger = (Danger)Math.Max((int) maxDanger, (int)currentDanger);

            Predicate<IntVec3> validator = c => c.Standable() && Find.RoofGrid.Roofed(c) 
                && CoverUtility.TotalSurroundingCoverScore(c) > 2.5f && !NextToDoor(c, me)
                && originCell.CanReach(c, PathEndMode.OnCell, TraverseMode.PassDoors, danger);

            if (validator(originCell) && InRange(originCell, me, 20)) return originCell;

            for (int i = 0; i < 50; i++)
            {
                Thing thing = GetRandom(Find.ListerBuildings.allBuildingsColonist);
                
                if (thing == null) thing = GetRandom(Find.ListerBuildings.allBuildingsColonistCombatTargets);
                if (thing == null) thing = GetRandom(Find.ListerPawns.FreeColonists);
                if (thing == null) thing = GetRandom(Find.ListerPawns.ColonistsAndPrisoners);

                if (thing == null) break;
                IntVec3 result;
                if (CellFinder.TryFindRandomCellNear(thing.Position, 10, validator, out result))
                {
                    return result;
                }
            }

            Predicate<IntVec3> simpleValidator = c => c.Standable()
               && CoverUtility.TotalSurroundingCoverScore(c) > 1 && !NextToDoor(c, me)
               && originCell.CanReach(c, PathEndMode.OnCell, TraverseMode.PassDoors, danger);

            IntVec3 randomCell;
            return CellFinder.TryFindRandomCellNear(originCell, 20, simpleValidator, out randomCell) ? randomCell : originCell;
        }

        private static bool InRange(IntVec3 originCell, Pawn me, int range)
        {
            return originCell.InHorDistOf(me.Position, range);
        }

        private static T GetRandom<T>(IEnumerable<T> list) where T : class 
        {
            if (list == null) return null;
            var array = list as T[] ?? list.ToArray();
            return array.Any() ? array.RandomElement() : null;
        }

        private static bool NextToDoor(IntVec3 c, Pawn me)
        {
            // Any door or pawn (excluding myself)?
            Func<IntVec3, bool> predicate = a => a.InBounds() && a.GetThingList().Any(t =>
                                                                                      {
                                                                                          if (t == me) return false;
                                                                                          return t is Building_Door;
                                                                                      });

            return predicate(c) || c.GetThingList().Any(t=>t != me && t is Pawn) || GenAdj.CellsAdjacentCardinal(c, new Rot4(), IntVec2.One).Any(predicate);
        }
    }
}