﻿// Karel Kroeze
// ManagerJob_Livestock.cs
// 2016-12-09

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FluffyManager
{
    public class ManagerJob_Livestock : ManagerJob
    {
        private History _history;
        public bool ButcherExcess;
        public bool ButcherTrained;
        public bool ButcherPregnant;
        public bool ButcherBonded;
        private List<Designation> Designations;
        public List<Area> RestrictArea;
        public bool RestrictToArea;
        public Area TameArea;
        public bool SendToSlaughterArea;
        public Area SlaughterArea;
        public TrainingTracker Training;
        public new Trigger_PawnKind Trigger;
        public bool TryTameMore;

        public ManagerJob_Livestock( Manager manager ) : base( manager )
        {
            // init designations
            Designations = new List<Designation>();

            // start history tracker
            _history = new History( Utilities_Livestock.AgeSexArray.Select( ageSex => ageSex.ToString() ).ToArray() );

            // set up the trigger, set all target counts to 5
            Trigger = new Trigger_PawnKind( this.manager );

            // set all training to false
            Training = new TrainingTracker();

            // set areas for restriction and taming to unrestricted
            TameArea = null;
            RestrictToArea = false;
            RestrictArea = Utilities_Livestock.AgeSexArray.Select( k => (Area)null ).ToList();

            // set up sending animals designated for slaughter to an area (freezer)
            SendToSlaughterArea = false;
            SlaughterArea = null;

            // set defaults for boolean options
            TryTameMore = false;
            ButcherExcess = true;
            ButcherTrained = false;
            ButcherPregnant = false;
            ButcherBonded = false;
        }

        public ManagerJob_Livestock( PawnKindDef pawnKindDef, Manager manager ) : this( manager ) // set defaults
        {
            // set pawnkind and get list of current colonist pawns of that def.
            Trigger.pawnKind = pawnKindDef;
        }

        public override string Label => Trigger.pawnKind.LabelCap;

        public override bool Completed
        {
            get
            {
                // state for lifestock trigger includes counts as well as training targets.
                return Trigger.State;
            }
        }

        public override ManagerTab Tab
        {
            get { return Manager.For( manager ).ManagerTabs.OfType<ManagerTab_Livestock>().First(); }
        }

        public override string[] Targets
        {
            get
            {
                return Utilities_Livestock.AgeSexArray
                                          .Select(
                                                  ageSex =>
                                                  ( "FMP." + ageSex.ToString() + "Count" ).Translate(
                                                                                                     Trigger.pawnKind
                                                                                                            .GetTame(
                                                                                                                     manager,
                                                                                                                     ageSex )
                                                                                                            .Count,
                                                                                                     Trigger
                                                                                                         .CountTargets[
                                                                                                                       ageSex
                                                                                                         ] ) )
                                          .ToArray();
            }
        }

        public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Handling;

        private bool TrainingRequired
        {
            get
            {
                // if nothing is selected, the answer is simple
                if ( !Training.Any )
                    return false;

                // otherwise, do a 'dry run' of the training assignment - the logic is entirely the same.
                var actionTaken = false;
                DoTrainingJobs( ref actionTaken, false );
                return actionTaken;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // settings, references first!
            Scribe_References.Look( ref TameArea, "TameArea" );
            Scribe_References.Look( ref SlaughterArea, "SlaughterArea" );
            Scribe_Collections.Look( ref RestrictArea, "AreaRestrictions", LookMode.Reference );
            Scribe_Deep.Look( ref Trigger, "trigger", manager );
            Scribe_Deep.Look( ref Training, "Training" );
            Scribe_Values.Look( ref ButcherExcess, "ButcherExcess", true );
            Scribe_Values.Look(ref ButcherTrained, "ButcherTrained", false);
            Scribe_Values.Look(ref ButcherPregnant, "ButcherPregnant", false);
            Scribe_Values.Look(ref ButcherBonded, "ButcherBonded", false);
            Scribe_Values.Look( ref RestrictToArea, "RestrictToArea", false );
            Scribe_Values.Look( ref SendToSlaughterArea, "SendToSlaughterArea", false );
            Scribe_Values.Look( ref TryTameMore, "TryTameMore", false );

            // our current designations
            if ( Scribe.mode == LoadSaveMode.PostLoadInit )
            {
                // populate with all designations.
                Designations.AddRange(
                                      manager.map.designationManager.SpawnedDesignationsOfDef( DesignationDefOf.Slaughter )
                                             .Where( des => ( (Pawn)des.target.Thing ).kindDef == Trigger.pawnKind ) );
                Designations.AddRange(
                                      manager.map.designationManager.SpawnedDesignationsOfDef( DesignationDefOf.Tame )
                                             .Where( des => ( (Pawn)des.target.Thing ).kindDef == Trigger.pawnKind ) );
            }

            // this is an array of strings as the first (and only) parameter - make sure it doesn't get cast to array of objects for multiple parameters.
            Scribe_Deep.Look( ref _history, "History" );
        }

        public override bool TryDoJob()
        {
            // work done?
            var actionTaken = false;

#if DEBUG_LIFESTOCK
            Log.Message( "Doing livestock (" + Trigger.pawnKind.LabelCap + ") job" );
#endif

            // update changes in game designations in our managed list
            // intersect filters our list down to designations that exist both in our list and in the game state.
            // This should handle manual cancellations and natural completions.
            // it deliberately won't add new designations made manually.
            Designations = Designations.Intersect( manager.map.designationManager.allDesignations ).ToList();

            // handle butchery
            DoButcherJobs( ref actionTaken );

            // area restrictions
            DoAreaRestrictions( ref actionTaken );

            // handle training
            DoTrainingJobs( ref actionTaken );

            // handle taming
            DoTamingJobs( ref actionTaken );

            return actionTaken;
        }

        private void DoAreaRestrictions( ref bool actionTaken )
        {
            if ( RestrictToArea )
            {
                for ( var i = 0; i < Utilities_Livestock.AgeSexArray.Length; i++ )
                {
                    foreach ( Pawn p in Trigger.pawnKind.GetTame( manager, Utilities_Livestock.AgeSexArray[i] ) )
                    {
                        if ( p.playerSettings.AreaRestriction != RestrictArea[i] &&
                            ( !SendToSlaughterArea || manager.map.designationManager.DesignationOn( p, DesignationDefOf.Slaughter ) == null ) )
                        {
                            actionTaken = true;
                            p.playerSettings.AreaRestriction = RestrictArea[i];
                        }
                    }
                }
            }
        }

        public List<Designation> DesignationsOfOn( DesignationDef def, Utilities_Livestock.AgeAndSex ageSex )
        {
            return Designations.Where( des => des.def == def
                                              && des.target.HasThing
                                              && des.target.Thing is Pawn
                                              && ( (Pawn)des.target.Thing ).PawnIsOfAgeSex( ageSex ) )
                               .ToList();
        }

        private bool TryRemoveDesignation( Utilities_Livestock.AgeAndSex ageSex, DesignationDef def )
        {
            // get current designations
            List<Designation> currentDesignations = DesignationsOfOn( def, ageSex );

            // if none, return false
            if ( currentDesignations.Count == 0 )
            {
                return false;
            }

            // else, remove one from the game as well as our managed list. (delete last - this should be the youngest/oldest).
            var designation = currentDesignations.Last();
            Designations.Remove(designation);
            designation.Delete();
            return true;
        }

        public void AddDesignation( Pawn p, DesignationDef def )
        {
            // create and add designation to the game and our managed list.
            var des = new Designation( p, def );
            Designations.Add( des );
            manager.map.designationManager.AddDesignation( des );
        }



        internal void DoTrainingJobs( ref bool actionTaken, bool assign = true )
        {
            actionTaken = false;

            foreach ( Utilities_Livestock.AgeAndSex ageSex in Utilities_Livestock.AgeSexArray )
            {
                // skip juveniles if TrainYoung is not enabled.
                if ( ageSex.Juvenile() && !Training.TrainYoung )
                    continue;

                foreach ( Pawn animal in Trigger.pawnKind.GetTame( manager, ageSex ) )
                {
                    foreach ( TrainableDef def in Training.Defs )
                    {
                        bool dump;
                        if ( !animal.training.IsCompleted( def ) &&

                             // only train if allowed.
                             animal.training.CanAssignToTrain( def, out dump ).Accepted &&

                             // only ever assign training, never de-asign.
                             animal.training.GetWanted( def ) != Training[def] &&
                             Training[def] )
                        {
                            if ( assign )
                                animal.training.SetWanted( def, Training[def] );
                            actionTaken = true;
                        }
                    }
                }
            }
        }

        private void DoTamingJobs( ref bool actionTaken )
        {
            if ( !TryTameMore )
            {
                return;
            }

            foreach ( Utilities_Livestock.AgeAndSex ageSex in Utilities_Livestock.AgeSexArray )
            {
                // not enough animals?
                int deficit = Trigger.CountTargets[ageSex]
                              - Trigger.pawnKind.GetTame( manager, ageSex ).Count
                              - DesignationsOfOn( DesignationDefOf.Tame, ageSex ).Count;

#if DEBUG_LIFESTOCK
                Log.Message( "Taming " + ageSex + ", deficit: " + deficit );
#endif

                if ( deficit > 0 )
                {
                    // get the 'home' position
                    IntVec3 position = manager.map.GetBaseCenter();

                    // get list of animals in sorted by youngest weighted to distance.
                    List<Pawn> animals = Trigger.pawnKind.GetWild( manager, ageSex )
                                                .Where( p => p != null && p.Spawned &&
                                                             manager.map.designationManager.DesignationOn( p ) == null &&
                                                             ( TameArea == null ||
                                                               TameArea.ActiveCells.Contains( p.Position ) ) ).ToList();

                    // skip if no animals available.
                    if ( animals.Count == 0 )
                        continue;

                    animals =
                        animals.OrderBy(
                                        p =>
                                        p.ageTracker.AgeBiologicalTicks /
                                        ( p.Position.DistanceToSquared( position ) * 2 ) ).ToList();

#if DEBUG_LIFESTOCK
                    Log.Message( "Wild: " + animals.Count );
#endif

                    for ( var i = 0; i < deficit && i < animals.Count; i++ )
                    {
#if DEBUG_LIFESTOCK
                        Log.Message( "Adding taming designation: " + animals[i].GetUniqueLoadID() );
#endif
                        AddDesignation( animals[i], DesignationDefOf.Tame );
                    }
                }

                // remove extra designations
                while ( deficit < 0 )
                {
                    if ( TryRemoveDesignation( ageSex, DesignationDefOf.Tame ) )
                    {
#if DEBUG_LIFESTOCK
                        Log.Message( "Removed extra taming designation" );
#endif
                        actionTaken = true;
                        deficit++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private void DoButcherJobs( ref bool actionTaken )
        {
            if ( !ButcherExcess )
            {
                return;
            }

#if DEBUG_LIFESTOCK
            Log.Message( "Doing butchery: " + Trigger.pawnKind.LabelCap );
#endif

            foreach ( Utilities_Livestock.AgeAndSex ageSex in Utilities_Livestock.AgeSexArray )
            {
                // too many animals?
                int surplus = Trigger.pawnKind.GetTame( manager, ageSex ).Count
                              - DesignationsOfOn( DesignationDefOf.Slaughter, ageSex ).Count
                              - Trigger.CountTargets[ageSex];

#if DEBUG_LIFESTOCK
                Log.Message( "Butchering " + ageSex + ", surplus" + surplus );
#endif

                if ( surplus > 0 )
                {
                    // should slaughter oldest adults, youngest juveniles.
                    bool oldestFirst = ageSex == Utilities_Livestock.AgeAndSex.AdultFemale ||
                                       ageSex == Utilities_Livestock.AgeAndSex.AdultMale;

                    // get list of animals in correct sort order.
                    List<Pawn> animals = Trigger.pawnKind.GetTame( manager, ageSex )
                                                .Where(
                                                       p => manager.map.designationManager.DesignationOn( p, DesignationDefOf.Slaughter ) == null
                                                       && ( ButcherTrained || !p.training.IsCompleted( TrainableDefOf.Obedience ) )
                                                       && ( ButcherPregnant || !p.VisiblyPregnant() )
                                                       && ( ButcherBonded || !p.BondedWithColonist() ) )
                                                .OrderBy(
                                                         p => ( oldestFirst ? -1 : 1 ) * p.ageTracker.AgeBiologicalTicks )
                                                .ToList();

#if DEBUG_LIFESTOCK
                    Log.Message( "Tame animals: " + animals.Count );
#endif

                    for ( var i = 0; i < surplus && i < animals.Count; i++ )
                    {
#if DEBUG_LIFESTOCK
                        Log.Message( "Butchering " + animals[i].GetUniqueLoadID() );
#endif
                        AddDesignation( animals[i], DesignationDefOf.Slaughter );

                        // if needed, restrict to SlaughterArea.
                        if ( SendToSlaughterArea && SlaughterArea != null )
                            animals[i].playerSettings.AreaRestriction = SlaughterArea;
                    }
                }

                // remove extra designations
                while ( surplus < 0) { 
                    if ( TryRemoveDesignation( ageSex, DesignationDefOf.Slaughter ) )
                    {
#if DEBUG_LIFESTOCK
                        Log.Message( "Removed extra butchery designation" );
#endif
                        actionTaken = true;
                        surplus++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        public override void CleanUp()
        {
            foreach ( Designation des in Designations )
            {
                des.Delete();
            }

            Designations.Clear();
        }

        public override void DrawListEntry( Rect rect, bool overview = true, bool active = true )
        {
            // (detailButton) | name | (bar | last update)/(stamp) -> handled in Utilities.DrawStatusForListEntry

            // set up rects
            Rect labelRect = new Rect( Utilities.Margin, Utilities.Margin, rect.width -
                                                                           ( active
                                                                                 ? StatusRectWidth +
                                                                                   4 * Utilities.Margin
                                                                                 : 2 * Utilities.Margin ),
                                       rect.height - 2 * Utilities.Margin ),
                 statusRect = new Rect( labelRect.xMax + Utilities.Margin, Utilities.Margin, StatusRectWidth,
                                        rect.height - 2 * Utilities.Margin );

            // create label string
            string text = Label + "\n<i>";
            foreach ( Utilities_Livestock.AgeAndSex ageSex in Utilities_Livestock.AgeSexArray )
            {
                text += Trigger.pawnKind.GetTame( manager, ageSex ).Count + "/" + Trigger.CountTargets[ageSex] + ", ";
            }

            text += Trigger.pawnKind.GetWild( manager ).Count + "</i>";
            string tooltip = Trigger.StatusTooltip;

            // do the drawing
            GUI.BeginGroup( rect );

            // draw label
            Utilities.Label( labelRect, text, tooltip );

            // if the bill has a manager job, give some more info.
            if ( active )
            {
                this.DrawStatusForListEntry( statusRect, Trigger );
            }
            GUI.EndGroup();
        }

        public override void DrawOverviewDetails( Rect rect )
        {
            _history.DrawPlot( rect );
        }

        public override void Tick()
        {
            if( _history.IsRelevantTick )
                _history.Update( Trigger.Counts );
        }

        public AcceptanceReport CanBeTrained( PawnKindDef pawnKind, TrainableDef td, out bool visible )
        {
            if ( pawnKind.RaceProps.untrainableTags != null )
            {
                for ( var index = 0; index < pawnKind.RaceProps.untrainableTags.Count; ++index )
                {
                    if ( td.MatchesTag( pawnKind.RaceProps.untrainableTags[index] ) )
                    {
                        visible = false;
                        return false;
                    }
                }
            }
            if ( pawnKind.RaceProps.trainableTags != null )
            {
                for ( var index = 0; index < pawnKind.RaceProps.trainableTags.Count; ++index )
                {
                    if ( td.MatchesTag( pawnKind.RaceProps.trainableTags[index] ) )
                    {
                        if ( pawnKind.RaceProps.baseBodySize < (double)td.minBodySize )
                        {
                            visible = true;
                            return new AcceptanceReport( "CannotTrainTooSmall".Translate( (object)pawnKind.LabelCap ) );
                        }

                        visible = true;
                        return true;
                    }
                }
            }

            if ( !td.defaultTrainable )
            {
                visible = false;
                return false;
            }

            if ( pawnKind.RaceProps.baseBodySize < (double)td.minBodySize )
            {
                visible = true;
                return new AcceptanceReport( "CannotTrainTooSmall".Translate( (object)pawnKind.LabelCap ) );
            }

            if ( pawnKind.RaceProps.TrainableIntelligence.intelligenceOrder < td.requiredTrainableIntelligence.intelligenceOrder )
            {
                visible = true;
                return
                    new AcceptanceReport(
                        "CannotTrainNotSmartEnough".Translate( (object)td.requiredTrainableIntelligence ) );
            }

            visible = true;
            return true;
        }

        public void DrawTrainingSelector( Rect rect, float lrMargin = 0f )
        {
            if ( lrMargin > 0 )
            {
                rect.xMin += lrMargin;
                rect.width -= 2 * lrMargin;
            }

            float width = rect.width / Training.Count;
            List<TrainableDef> keys = Training.Defs;

            GUI.BeginGroup( rect );
            for ( var i = 0; i < Training.Count; i++ )
            {
                var cell = new Rect( i * width, 0f, width, rect.height );
                bool visible;
                AcceptanceReport report = CanBeTrained( Trigger.pawnKind, keys[i], out visible );
                if ( visible && report.Accepted )
                {
                    bool checkOn = Training[keys[i]];
                    Utilities.DrawToggle( cell, keys[i].LabelCap, ref checkOn, 16f, 0f, GameFont.Tiny );
                    Training[keys[i]] = checkOn;
                }
                else if ( visible )
                {
                    Utilities.Label( cell, keys[i].LabelCap, report.Reason, font: GameFont.Tiny, color: Color.grey );
                }
            }

            GUI.EndGroup();
        }

        public class TrainingTracker : IExposable
        {
            public DefMap<TrainableDef, bool> TrainingTargets = new DefMap<TrainableDef, bool>();
            public bool TrainYoung;

            public bool this[TrainableDef index]
            {
                get { return TrainingTargets[index]; }
                set { SetWantedRecursive( index, value ); }
            }

            public bool Any
            {
                get
                {
                    foreach ( TrainableDef def in Defs )
                    {
                        if ( TrainingTargets[def] )
                            return true;
                    }

                    return false;
                }
            }

            public int Count
            {
                get { return TrainingTargets.Count; }
            }

            public List<TrainableDef> Defs
            {
                get { return DefDatabase<TrainableDef>.AllDefsListForReading; }
            }

            public void ExposeData()
            {
                Scribe_Values.Look( ref TrainYoung, "TrainYoung", false );
                Scribe_Deep.Look( ref TrainingTargets, "TrainingTargets" );
            }

            private void SetWantedRecursive( TrainableDef td, bool wanted )
            {
                // cop out if nothing changed
                if ( TrainingTargets[td] == wanted )
                    return;

                // make changes
                TrainingTargets[td] = wanted;
                if ( wanted )
                {
                    SoundDefOf.CheckboxTurnedOn.PlayOneShotOnCamera();
                    if ( td.prerequisites != null )
                    {
                        foreach ( TrainableDef trainable in td.prerequisites )
                        {
                            SetWantedRecursive( trainable, true );
                        }
                    }
                }
                else
                {
                    SoundDefOf.CheckboxTurnedOff.PlayOneShotOnCamera();
                    IEnumerable<TrainableDef> enumerable = from t in DefDatabase<TrainableDef>.AllDefsListForReading
                                                           where
                                                               t.prerequisites != null && t.prerequisites.Contains( td )
                                                           select t;
                    foreach ( TrainableDef current in enumerable )
                    {
                        SetWantedRecursive( current, false );
                    }
                }
            }
        }
    }
}
