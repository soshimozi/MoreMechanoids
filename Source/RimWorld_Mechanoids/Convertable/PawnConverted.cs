using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreMechanoids
{
    public class PawnConverted : Pawn
    {
        private string raceType = String.Empty;
        public string RaceType { get { return raceType; } set { SetRaceType(value); } }

        private string workType = String.Empty;
        public string WorkType { get { return workType; } set { SetJob(value); } }

        private void SetRaceType(string value)
        {
            raceType = GenText.ToTitleCaseSmart(value);
            //if (story != null) Name = new NameSingle(raceType);
        }

        private void SetJob(string value)
        {
            workType = GenText.ToTitleCaseSmart(value);
        }

        public bool stayHome;
        public bool fullRepair;

        public bool InStandby
        {
            get
            {
                return (jobs.curDriver as JobDriver_Standby != null) && ((JobDriver_Standby) jobs.curDriver).StandingBy;
            }
        }

        public bool InStandbyPowered
        {
            get
            {
                if (jobs.curJob == null) return false;
                if (jobs.curJob.targetA == null) return false;
                var restSpot = jobs.curJob.targetA.Thing as Building_RestSpot;
                return InStandby && jobs.curJob.targetA.HasThing && restSpot != null && restSpot.powerComp.PowerOn;
            }
        }

        public bool Crashed { get { return BrokenStateDef == Defs.CrashedDef; } }

        public override string LabelBaseShort { get { return NameStringShort; } }

        public override string LabelBase
        {
            get { return String.Format("{0}, {1} ({2})", NameStringShort, RaceType, workType); }
        }

        private const bool DisplayThoughtTab = false;

        public static readonly Texture2D StayHomeTex =
            SolidColorMaterials.NewSolidColorTexture(new Color(0.29f, 0.6f, 0.7f, 0.25f));

        private static readonly string txtKeepInside = "KeepInside".Translate();
        private static readonly string txtFullRepair = "FullRepair".Translate();
        private static readonly string txtStayHome = "KeepInside".Translate();
        private static readonly string txtStoryAdultTitle = "BotStoryAdultTitle".Translate();
        private static readonly string txtStoryAdultShort = "BotStoryAdultShort".Translate();
        private static readonly string txtStoryAdultDesc = "BotStoryAdultDesc".Translate();
        private static readonly string txtStoryChildTitle = "BotStoryChildTitle".Translate();
        private static readonly string txtStoryChildDesc = "BotStoryChildDesc".Translate();
        private static Texture2D texUI_StayHome = ContentFinder<Texture2D>.Get("UI/Commands/PAL/UI_StayHome");
        private static Texture2D texUI_FullRepair = ContentFinder<Texture2D>.Get("UI/Commands/PAL/UI_FullRepair");
        // For initialization
        public List<WorkTypeDef> workTypes;
        private int lastCrashTime = -9999;
        private float nextDamageTicks = 60*60*10; // 10 minutes
        private HediffDef hediffDeterioration = HediffDef.Named("MM_Deterioration");
        public float workCapacity;

        public bool Busy { get { return pather.Moving; } }

        public static Command_Toggle GetCommandStayHome(int num)
        {
            return new Command_Toggle
            {
                icon = texUI_StayHome,
                defaultDesc = txtKeepInside,
                hotKey = KeyBindingDefOf.CommandColonistDraft,
                activateSound = SoundDef.Named("Click"),
                groupKey = num
            };
        }

        public static Command_Toggle GetCommandFullRepair(int num)
        {
            return new Command_Toggle
            {
                icon = texUI_FullRepair,
                defaultDesc = txtFullRepair,
                hotKey = KeyBindingDefOf.CommandTogglePower,
                activateSound = SoundDef.Named("Click"),
                groupKey = num
            };
        }

        private static Backstory Adulthood
        {
            get
            {
                return new Backstory
                {
                    title = txtStoryAdultTitle,
                    titleShort = txtStoryAdultShort,
                    baseDesc = txtStoryAdultDesc,
                    slot = BackstorySlot.Adulthood,
                    // need uniqueSaveKey, to store / reload backstory without error...
                    uniqueSaveKey = "MadScientist1156994384"
                };
            }
        }

        private static Backstory Childhood
        {
            get
            {
                return new Backstory
                {
                    title = txtStoryChildTitle,
                    titleShort = txtStoryChildTitle,
                    baseDesc = txtStoryChildDesc,
                    slot = BackstorySlot.Childhood,
                    workDisables = WorkTags.None,
                    uniqueSaveKey = "TragicTwin1582359021"
                };
            }
        }

        public override void SpawnSetup()
        {
            base.SpawnSetup();

            if (Name is NameSingle)
            {
                Name = new NameTriple("Mechanoid", Name.ToStringFull, def.LabelCap);
            }

            if (story == null)
            {
                Initialize();
            }
        }

        public override string GetInspectString()
        {
            return base.GetInspectString() + "Crash chance: " + Mathf.Round(100 - workCapacity*100) + "%\n";
        }

        public override void ExposeData()
        {
            base.ExposeData();
            story.childhood = Childhood;
            story.adulthood = Adulthood;

            // store / restore nickname
            Scribe_Values.LookValue(ref raceType, "raceType");
            Scribe_Values.LookValue(ref workType, "workType");
            Scribe_Values.LookValue(ref stayHome, "stayHome");
            Scribe_Values.LookValue(ref fullRepair, "fullRepair");
            Scribe_Values.LookValue(ref lastCrashTime, "lastCrashTime");
            Scribe_Values.LookValue(ref nextDamageTicks, "nextDamageTicks");
            Scribe_Values.LookValue(ref workCapacity, "workCapacity");
        }

        // Copied from DrawPawnGUIOverlay(), so we can have the overlay even without a story

        public override void DrawGUIOverlay()
        {
            if (!SpawnedInWorld || Find.FogGrid.IsFogged(Position) || health.Dead || Faction != Faction.OfColony) return;

            Vector3 vector = GenWorldUI.LabelDrawPosFor(this, -0.6f);
            //if (PawnUIOverlay.ShouldDrawOverlayOnMap(this))
            {
                Text.Font = GameFont.Tiny;
                float num2 = Text.CalcSize(NameStringShort).x;
                if (num2 < 20f)
                {
                    num2 = 20f;
                }
                Rect rect = new Rect(vector.x - num2/2f - 4f, vector.y, num2 + 8f, 12f);
                GUI.DrawTexture(rect, TexUI.GrayTextBG);
                if (stayHome)
                {
                    Rect screenRect = rect.ContractedBy(1f);
                    Widgets.FillableBar(screenRect, 1, StayHomeTex, BaseContent.ClearTex, false);
                }
                else if (health.summaryHealth.SummaryHealthPercent < 0.999f)
                {
                    Rect screenRect = rect.ContractedBy(1f);
                    Widgets.FillableBar(screenRect, health.summaryHealth.SummaryHealthPercent, PawnUIOverlay.HealthTex,
                        BaseContent.ClearTex, false);
                }
                //if (!AIEnabled)
                //{
                //    Rect innerRect = rect.GetInnerRect(1f);
                //    Widgets.FillableBar(innerRect, chipPower, PawnUIOverlay.HealthTex, false, BaseContent.ClearTex);
                //}

                GUI.color = PawnNameColorUtility.PawnNameColorOf(this);
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(vector.x - num2/2f, vector.y - 2f, num2, 999f), NameStringShort);
                //if (!AIEnabled)
                //{
                //    Widgets.DrawLineHorizontal(new Vector2(vector.x - num2/2f, vector.y + 6f), num2); // y + 11f
                //}
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            //foreach (var command in base.GetGizmos())
            //{
            //
            //    yield return command;
            //}

            const int num = 89327595;

            if (!Dead && BrokenStateDef == null)
            {
                var commandStayHome = GetCommandStayHome(num);
                commandStayHome.isActive = () => stayHome;
                commandStayHome.toggleAction = () => SetStayHome(!stayHome);
                commandStayHome.defaultDesc = txtKeepInside;
                yield return commandStayHome;

                var commandFullRepair = GetCommandFullRepair(num + 1);
                commandFullRepair.isActive = () => fullRepair;
                commandFullRepair.toggleAction = () => SetFullRepair(!fullRepair);
                commandFullRepair.defaultDesc = txtFullRepair;
                yield return commandFullRepair;
            }
        }

        public void SetStayHome(bool value)
        {
            stayHome = value;
            if (value) jobs.StopAll(true);
        }

        public void SetFullRepair(bool value)
        {
            fullRepair = value;
            if (value && jobs != null) jobs.StopAll(true);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            SetFactionDirect(Faction.OfMechanoids);
            base.Destroy(mode);
        }

        public override void PostMapInit()
        {
            //base.PostMapInit();

            // Nickname gets overwritten by story loader
            InitStory();
        }

        private static List<WorkTypeDef> GetRandomWorkTypes(Pawn_StoryTracker story)
        {
            List<WorkTypeDef> workTypes = new List<WorkTypeDef>();
            while (workTypes.Count == 0)
            {
                var typeDef = DefDatabase<WorkTypeDef>.GetRandom();
                // these are not allowed
                if (!story.WorkTypeIsDisabled(typeDef))
                {
                    workTypes.Add(typeDef);
                }
            }
            return workTypes;
        }

        private void InitStory()
        {
            // Story
            story = new Pawn_StoryTracker(this) {childhood = Childhood, adulthood = Adulthood,};
        }

        public void Initialize()
        {
            bool unexpected = workTypes == null;

            ageTracker.AgeBiologicalTicks = 0;
            ageTracker.SetChronologicalBirthDate(GenDate.CurrentYear, GenDate.DayOfYear);

            RaceType = def.label;

            RaceProps.nameGenerator = RulePackDef.Named("NamerAnimalGeneric");

            GiveRandomName();

            InitStory();

            // Skills
            skills = new Pawn_SkillTracker(this);
            skills.GetSkill(SkillDefOf.Melee);
            skills.Learn(SkillDefOf.Melee, KindDef.minSkillPoints);

            if (!DisplayThoughtTab)
            {
                //def.inspectorTabs.Remove(typeof (ITab_Pawn_Needs));
            }

            // Talker
            talker = new Pawn_Converted_TalkTracker(this);

            SpawnSetupWorkSettings();

            //needs.beauty = new Need_Beauty_Mechanoid(this);
            needs.food = new Need_Food(this);
            needs.rest = new Need_Rest(this);
            needs.mood = new Need_Mood(this);
            //needs.space = new Need_Space_Mechanoid(this);


            apparel = new Pawn_ApparelTracker(this);

            UpdateWorkCapacity();

            // Job (should go last!)
            if (jobs.curJob == null)
            {
                var jobPackage = thinker.ThinkNodeRoot.TryIssueJobPackage(this);
                mindState.lastJobGiver = jobPackage.SourceNode; //.finalNode;
                jobs.StartJob(jobPackage.Job);
            }
            Log.Message(Label + " initialized.");

            if (unexpected)
            {
                TakeDamage(new DamageInfo(DamageDefOf.Crush, Rand.Range(15000, 20000)));
            }
        }

        private void GiveRandomName()
        {
            // Hack namegen to avoid weird error
            List<string> names =
                (List<string>)
                    typeof (NameBank).GetMethod("NamesFor", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(PawnNameDatabaseShuffled.BankOf(PawnNameCategory.HumanStandard),
                            new object[] {gender, PawnNameSlot.Nick});

            //Name = new NameSingle(names.RandomElement()); //NameGenerator.GenerateName(this); << throws weird error
            Name = new NameTriple("Mechanoid", names.RandomElement(), def.LabelCap);
        }

        private void SpawnSetupWorkSettings()
        {
            if (workTypes == null)
            {
                //Log.Warning("Unexpected creation of converted pawn.");
                workTypes = GetRandomWorkTypes(story);
            }

            WorkType = workTypes.Count > 0 ? workTypes[0].ToString() : String.Empty;

            // Work
            workSettings = new Pawn_WorkSettings(this);
            workSettings.EnableAndInitialize();
            var skillsDefs = new List<SkillDef>();
            foreach (WorkTypeDef current in DefDatabase<WorkTypeDef>.AllDefs)
            {
                if (workTypes.Contains(current))
                {
                    workSettings.SetPriority(current, 1);
                    skillsDefs.AddRange(current.relevantSkills);
                }
                else
                {
                    workSettings.Disable(current);
                }
            }
            skillsDefs.RemoveDuplicates();
            foreach (var skillDef in skillsDefs)
            {
                var record = skills.GetSkill(skillDef);
                var minSkillLevel = KindDef.minSkillPoints;
                if (record == null || record.XpTotalEarned < minSkillLevel)
                {
                    skills.Learn(skillDef, Rand.Range(minSkillLevel, KindDef.maxSkillPoints));
                }
            }
            //foreach (WorkTypeDef current in DefDatabase<WorkTypeDef>.AllDefs)
            //{
            //    if (workTypes.Contains(current))
            //    {
            //    }
            //}
        }

        private PawnKindDef KindDef { get { return ((PawnKindDef) kindDef); } }

        public float GetCrashChance()
        {
            var timeSinceCrash = Find.TickManager.TicksGame - lastCrashTime;
            float crashChance = KindDef.crashChance; //0.05f;
            return 0.00001f*crashChance*timeSinceCrash*(1 - workCapacity);
        }

        public void UpdateWorkCapacity()
        {
            workCapacity = health.capacities.GetEfficiency(PawnCapacityDefOf.Consciousness)
                           *health.capacities.GetEfficiency(PawnCapacityDefOf.Sight)
                           *health.capacities.GetEfficiency(PawnCapacityDefOf.Manipulation)
                           *health.capacities.GetEfficiency(PawnCapacityDefOf.Moving);
        }

        // Copied from original tick

        public override void Tick()
        {
            //if (DebugSettings.noAnimals && RaceProps.IsAnimal)
            //{
            //    Destroy();
            //    return;
            //}
            if (stances != null && !stances.FullBodyBusy) pather.PatherTick();

            drawer.DrawTrackerTick();
            ageTracker.AgeTick();
            if(health != null) health.HealthTick();
            if(stances != null) stances.StanceTrackerTick();
            if(mindState != null) mindState.MindTick();

            if (equipment != null)
            {
                equipment.EquipmentTrackerTick();
            }
            if (apparel == null) apparel = new Pawn_ApparelTracker(this);
            //if (apparel != null)
            //{
            //    apparel.ApparelTrackerTick();
            //}
            if (jobs != null)
            {
                jobs.JobTrackerTick();
            }
            if (carrier != null)
            {
                carrier.CarryHandsTick();
            }
            if (talker as Pawn_Converted_TalkTracker != null)
            {
                ((Pawn_Converted_TalkTracker) talker).TalkTrackerTick();
            }
            //needs.NeedsTrackerTick();

            if (caller != null)
            {
                caller.CallTrackerTick();
            }
            if (skills != null)
            {
                skills.SkillsTick();
            }
            //if (playerController != null)
            //{
            //    playerController.PlayerControllerTick();
            //}

            CrashCheck();

            DamageCheck();

            // If at home and damaged, do full repair
            RepairCheck();

            if (needs != null && needs.mood == null) needs.mood = new Need_Mood(this);
        }

        private void RepairCheck()
        {
            if (fullRepair) return;

            if (Find.AreaHome[Position] && 100 - workCapacity*100 > KindDef.fullRepairThreshold*100) SetFullRepair(true);
            if (health.summaryHealth.SummaryHealthPercent < 1 - KindDef.fullRepairThreshold) SetFullRepair(true);
        }

        private void DamageCheck()
        {
            if (Dead) return;
            nextDamageTicks -= !InStandby ? 1f : 1f/KindDef.standbyDeteriorationFactor;
            if (nextDamageTicks < 0)
            {
                nextDamageTicks = 60*60*Rand.Range(KindDef.minTimeBeforeDeteriorate, KindDef.maxTimeBeforeDeteriorate);
                    // damage every x minutes, half as fast in standby

                Deteriorate();
            }
        }

        private void Deteriorate()
        {
            // Powered? No deterioriation
            if (InStandbyPowered) return;

            IEnumerable<BodyPartRecord> source = GetDeterioratingParts().ToArray();
            if (source.Any())
            {
                BodyPartRecord bodyPartRecord = source.RandomElement();
                HediffDef hediffDef = hediffDeterioration;

                // Brain lasts longest
                bool isBrain = bodyPartRecord.def.Activities.Any(a => a.First == PawnCapacityDefOf.Consciousness);
                bool isInside = bodyPartRecord.depth!=BodyPartDepth.Outside;
                float damageFactor;
                if (isBrain) damageFactor = 1/4f;
                else if (isInside) damageFactor = 1/2f;
                else damageFactor = 1f;

                float maxDamage = damageFactor*bodyPartRecord.def.hitPoints*KindDef.maxDeteriorationFactor;
                float amount = Rand.Range(0, maxDamage);

                var hediffInjury = (Hediff_Injury) HediffMaker.MakeHediff(hediffDef, this);
                hediffInjury.Severity = amount;
                health.AddHediff(hediffInjury, bodyPartRecord, null);
                UpdateWorkCapacity();
            }
        }

        private IEnumerable<BodyPartRecord> GetDeterioratingParts()
        {
            return health.hediffSet.GetNotMissingParts(null, null);
        }

        private void CrashCheck()
        {
            if (Dead || Crashed || InStandby) return;

            if (Rand.Value < GetCrashChance())
            {
                Crash();
            }
        }

        private void Crash()
        {
            if (Crashed) return;

            lastCrashTime = Find.TickManager.TicksGame;

            if (mindState.breaker.TryDoMentalBreak(Defs.CrashedDef))
            {
                return;
            }
            //if (jobs == null) return;

            // Recover insanity?
            if (BrokenStateDef != null && Rand.Value < 0.35f)
            {
                MoteThrower.ThrowStatic(Position, ThingDefOf.Mote_ShotFlash, Rand.Range(5f, 10f));
                BrokenState.RecoverFromState();
            }

            //if (jobs != null)
            //{
            //    if (jobs.curJob != null)
            //    {
            //        if (!InStandby)
            //        {
            //            jobs.StopAll();
            //        }
            //    }
            //    //jobs.StartJob(new Job(rebootJobDef, Position));
            //}


            //MoteThrower.ThrowMetaPuffs(Position);
            //for (int i = 0; i < Rand.Range(1, 4); i++)
            //{
            //    MoteThrower.ThrowStatic(Position, ThingDefOf.Mote_ShotHit_Spark, Rand.Range(0.5f, 2.5f));
            //}
        }

        /*
        private void ForAssimilated()
        {
            if (RaceProps.hasStory)
            {
                story.skinColor = PawnSkinColors.RandomSkinColor();
                story.crownType = ((Rand.Value >= 0.5f) ? CrownType.Narrow : CrownType.Average);
                story.headGraphicPath =
                    GraphicDatabaseHeadRecords.GetHeadRandom(Gender.Female, story.skinColor, story.crownType)
                        .GraphicPath;
           //     story.hairColor = PawnHairColors.RandomHairColor(story.skinColor);
                story.hairDef = null; //PawnHairChooser.RandomHairDefFor(this, Faction.def);
            }
            string graphicPathBody = "Things/Pawn/Mechanoid/Bodies/Robot/";
            string graphicPathHead = "Things/Pawn/Mechanoid/Bodies/RobotHead/";
           //drawer.renderer.graphics.nakedGraphic = GraphicDatabase.Get_Multi(graphicPathBody, ShaderDatabase.CutoutSkin,
           //    false, Color.white);
           //drawer.renderer.graphics.headGraphic = GraphicDatabase.Get_Multi(graphicPathHead, ShaderDatabase.CutoutSkin,
           //    false, Color.white);
        }
         */

        public void OnFullRepairComplete()
        {
            nextDamageTicks = nextDamageTicks = Rand.Range(60*60*5, 60*60*10); // 5-10 minutes
        }
    }
}
