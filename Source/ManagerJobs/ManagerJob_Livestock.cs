// ManagerJob_Livestock.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using static FluffyManager.Constants;

namespace FluffyManager
{
    public class ManagerJob_Livestock : ManagerJob
    {
        private static readonly MethodInfo       SetWanted_MI;
        public                  bool             ButcherBonded;
        public                  bool             ButcherExcess;
        public                  bool             ButcherPregnant;
        public                  bool             ButcherTrained;
        public                  bool             FollowDrafted;
        public                  bool             FollowFieldwork;
        public                  bool             FollowTraining;
        public                  Pawn             Master;
        public                  MasterMode       Masters;
        public                  Area             MilkArea;
        public                  bool             RespectBonds = true;
        public                  List<Area>       RestrictArea;
        public                  bool             RestrictToArea;
        public                  bool             SendToMilkingArea;
        public                  bool             SendToShearingArea;
        public                  bool             SendToSlaughterArea;
        public                  bool             SendToHungryArea;
        public                  bool             SendToTrainingArea;
        public                  bool             SetFollow;
        public                  Area             ShearArea;
        public                  Area             SlaughterArea;
        public                  Area             TameArea;
        public                  Area             HungryArea;
        public                  Pawn             Trainer;
        public                  MasterMode       Trainers;
        public                  TrainingTracker  Training;
        public                  Area             TrainingArea;
        public new              Trigger_PawnKind Trigger;
        public                  bool             TryTameMore;

        private Utilities.CachedValue<string> _cachedLabel;
        private List<Designation>             _designations;
        private History                       _history;

        static ManagerJob_Livestock()
        {
            SetWanted_MI =
                typeof( Pawn_TrainingTracker ).GetMethod( "SetWanted", BindingFlags.NonPublic | BindingFlags.Instance );
            if ( SetWanted_MI == null )
                throw new NullReferenceException( "Could not find Pawn_TrainingTracker.SetWanted()" );
        }

        public ManagerJob_Livestock( Manager manager ) : base( manager )
        {
            // init designations
            _designations = new List<Designation>();

            // start history tracker
            _history = new History( Utilities_Livestock.AgeSexArray.Select( ageSex => ageSex.ToString() ).ToArray() );

            // set up the trigger, set all target counts to 5
            Trigger = new Trigger_PawnKind( this.manager );

            // set all training to false
            Training = new TrainingTracker();

            // set areas for restriction and taming to unrestricted
            TameArea       = null;
            RestrictToArea = false;
            RestrictArea   = Utilities_Livestock.AgeSexArray.Select( k => (Area) null ).ToList();

            // set up sending animals designated for slaughter to an area (freezer)
            SendToSlaughterArea = false;
            SlaughterArea       = null;

            // set up milking area
            SendToMilkingArea = false;
            MilkArea          = null;

            // set up shearing area
            SendToShearingArea = false;
            ShearArea          = null;

            // set up training area
            SendToTrainingArea = false;
            TrainingArea       = null;

            // set up hungry area
            SendToHungryArea = false;
            HungryArea       = null;

            // taming
            TryTameMore = false;
            TameArea    = null;

            // set defaults for butchering
            ButcherExcess   = true;
            ButcherTrained  = false;
            ButcherPregnant = false;
            ButcherBonded   = false;

            // following
            SetFollow       = true;
            FollowDrafted   = true;
            FollowFieldwork = true;
            FollowTraining  = false;
            Masters         = MasterMode.Default;
            Master          = null;
            Trainers        = MasterMode.Default;
            Trainer         = null;
        }

        public ManagerJob_Livestock( PawnKindDef pawnKindDef, Manager manager ) : this( manager ) // set defaults
        {
            // set pawnkind and get list of current colonist pawns of that def.
            Trigger.pawnKind = pawnKindDef;
        }

        public override bool Completed => Trigger.State;

        public List<Designation> Designations => new List<Designation>( _designations );

        public string FullLabel
        {
            get
            {
                if ( _cachedLabel != null && _cachedLabel.TryGetValue( out var label ) )
                    return label;

                Func<string> labelGetter = () =>
                {
                    var text = Label + "\n<i>";
                    foreach ( var ageSex in Utilities_Livestock.AgeSexArray )
                        text += Trigger.pawnKind.GetTame( manager, ageSex ).Count() + "/" +
                                Trigger.CountTargets[ageSex]                        +
                                ", ";

                    text += Trigger.pawnKind.GetWild( manager ).Count() + "</i>";
                    return text;
                };
                _cachedLabel = new Utilities.CachedValue<string>( labelGetter(), 250, labelGetter );
                return labelGetter();
            }
        }

        public override bool IsValid => base.IsValid && _history != null && Training != null && Trigger != null;

        public override string Label => Trigger.pawnKind.LabelCap;

        public override ManagerTab Tab => manager.Tabs.OfType<ManagerTab_Livestock>().First();

        public override string[] Targets
        {
            get
            {
                return Utilities_Livestock.AgeSexArray
                                          .Select( ageSex =>
                                                       ( "FMP." + ageSex.ToString() + "Count" ).Translate(
                                                                                                    Trigger
                                                                                                       .pawnKind
                                                                                                       .GetTame(
                                                                                                            manager,
                                                                                                            ageSex )
                                                                                                       .Count(),
                                                                                                    Trigger.CountTargets
                                                                                                        [ageSex] )
                                                                                               .Resolve() )
                                          .ToArray();
            }
        }

        public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Handling;

        public void AddDesignation( Pawn p, DesignationDef def )
        {
            // create and add designation to the game and our managed list.
            var des = new Designation( p, def );
            _designations.Add( des );
            manager.map.designationManager.AddDesignation( des );
        }

        public AcceptanceReport CanBeTrained( PawnKindDef pawnKind, TrainableDef td, out bool visible )
        {
            if ( pawnKind.RaceProps.untrainableTags != null )
                for ( var index = 0; index < pawnKind.RaceProps.untrainableTags.Count; ++index )
                    if ( td.MatchesTag( pawnKind.RaceProps.untrainableTags[index] ) )
                    {
                        visible = false;
                        return false;
                    }

            if ( pawnKind.RaceProps.trainableTags != null )
                for ( var index = 0; index < pawnKind.RaceProps.trainableTags.Count; ++index )
                    if ( td.MatchesTag( pawnKind.RaceProps.trainableTags[index] ) )
                    {
                        if ( pawnKind.RaceProps.baseBodySize < (double) td.minBodySize )
                        {
                            visible = true;
                            return new AcceptanceReport(
                                "CannotTrainTooSmall".Translate( (object) pawnKind.LabelCap ) );
                        }

                        visible = true;
                        return true;
                    }

            if ( !td.defaultTrainable )
            {
                visible = false;
                return false;
            }

            if ( pawnKind.RaceProps.baseBodySize < (double) td.minBodySize )
            {
                visible = true;
                return new AcceptanceReport(
                    "FM.Livestock.CannotTrainTooSmall".Translate( (object) pawnKind.GetLabelPlural() ) );
            }

            if ( pawnKind.RaceProps.trainability.intelligenceOrder < td.requiredTrainability.intelligenceOrder )
            {
                visible = true;
                return
                    new AcceptanceReport( "CannotTrainNotSmartEnough".Translate( (object) td.requiredTrainability ) );
            }

            visible = true;
            return true;
        }

        public override void CleanUp()
        {
            foreach ( var des in _designations ) des.Delete();

            _designations.Clear();
        }

        public List<Designation> DesignationsOfOn( DesignationDef def, AgeAndSex ageSex )
        {
            return _designations.Where( des => des.def == def
                                            && des.target.HasThing
                                            && des.target.Thing is Pawn
                                            && ( (Pawn) des.target.Thing ).PawnIsOfAgeSex( ageSex ) )
                                .ToList();
        }

        public void DoFollowSettings( ref bool actionTaken )
        {
            foreach ( var animal in Trigger.pawnKind.GetTame( manager ).ToList() )
            {
                // training
                Logger.Follow( animal.LabelShort );
                if ( FollowTraining && animal.training.NextTrainableToTrain() != null )
                {
                    Logger.Follow( "\ttraining" );
                    if ( Trainers != MasterMode.Default )
                    {
                        SetMaster( animal, Trainers, Trainer, ref actionTaken );
                        SetFollowing( animal, false, true, ref actionTaken );
                    }
                }

                // default 
                else
                {
                    if ( Masters != MasterMode.Default ) SetMaster( animal, Masters, Master, ref actionTaken );
                    if ( SetFollow ) SetFollowing( animal, FollowDrafted, FollowFieldwork, ref actionTaken );
                }
            }
        }

        public override void DrawListEntry( Rect rect, bool overview = true, bool active = true )
        {
            // (detailButton) | name | (bar | last update)/(stamp) -> handled in Utilities.DrawStatusForListEntry

            // set up rects
            Rect labelRect = new Rect(
                     Margin, Margin, rect.width - ( active ? StatusRectWidth + 4 * Margin : 2 * Margin ),
                     rect.height                - 2 * Margin ),
                 statusRect = new Rect( labelRect.xMax + Margin, Margin, StatusRectWidth,
                                        rect.height    - 2 * Margin );


            // do the drawing
            GUI.BeginGroup( rect );

            // draw label
            Widgets.Label( labelRect, FullLabel );
            TooltipHandler.TipRegion( labelRect, () => Trigger.StatusTooltip, GetHashCode() );

            // if the bill has a manager job, give some more info.
            if ( active ) this.DrawStatusForListEntry( statusRect, Trigger );
            GUI.EndGroup();
        }

        public override void DrawOverviewDetails( Rect rect )
        {
            _history.DrawPlot( rect );
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // settings, references first!
            Scribe_References.Look( ref TameArea, "TameArea" );
            Scribe_References.Look( ref SlaughterArea, "SlaughterArea" );
            Scribe_References.Look( ref MilkArea, "MilkArea" );
            Scribe_References.Look( ref ShearArea, "ShearArea" );
            Scribe_References.Look( ref TrainingArea, "TrainingArea" );
            Scribe_References.Look( ref HungryArea, "HungryArea" );
            Scribe_References.Look( ref Master, "Master" );
            Scribe_References.Look( ref Trainer, "Trainer" );
            Scribe_Collections.Look( ref RestrictArea, "AreaRestrictions", LookMode.Reference );
            Scribe_Deep.Look( ref Trigger, "trigger", manager );
            Scribe_Deep.Look( ref Training, "Training" );
            Scribe_Deep.Look( ref _history, "History" );
            Scribe_Values.Look( ref ButcherExcess, "ButcherExcess", true );
            Scribe_Values.Look( ref ButcherTrained, "ButcherTrained" );
            Scribe_Values.Look( ref ButcherPregnant, "ButcherPregnant" );
            Scribe_Values.Look( ref ButcherBonded, "ButcherBonded" );
            Scribe_Values.Look( ref RestrictToArea, "RestrictToArea" );
            Scribe_Values.Look( ref SendToSlaughterArea, "SendToSlaughterArea" );
            Scribe_Values.Look( ref SendToMilkingArea, "SendToMilkingArea" );
            Scribe_Values.Look( ref SendToShearingArea, "SendToShearingArea" );
            Scribe_Values.Look( ref SendToTrainingArea, "SendToTrainingArea" );
            Scribe_Values.Look( ref SendToHungryArea, "SendToHungryArea");
            Scribe_Values.Look( ref TryTameMore, "TryTameMore" );
            Scribe_Values.Look( ref SetFollow, "SetFollow", true );
            Scribe_Values.Look( ref FollowDrafted, "FollowDrafted", true );
            Scribe_Values.Look( ref FollowFieldwork, "FollowFieldwork", true );
            Scribe_Values.Look( ref FollowTraining, "FollowTraining" );
            Scribe_Values.Look( ref Masters, "Masters" );
            Scribe_Values.Look( ref Trainers, "Trainers" );
            Scribe_Values.Look( ref RespectBonds, "RespectBonds", true );

            // our current designations
            if ( Scribe.mode == LoadSaveMode.PostLoadInit )
            {
                // populate with all designations.
                _designations.AddRange(
                    manager.map.designationManager.SpawnedDesignationsOfDef( DesignationDefOf.Slaughter )
                           .Where( des => ( (Pawn) des.target.Thing ).kindDef == Trigger.pawnKind ) );
                _designations.AddRange(
                    manager.map.designationManager.SpawnedDesignationsOfDef( DesignationDefOf.Tame )
                           .Where( des => ( (Pawn) des.target.Thing ).kindDef == Trigger.pawnKind ) );
            }
        }

        public Pawn GetMaster( Pawn animal, MasterMode mode )
        {
            var master  = animal.playerSettings.Master;
            var options = animal.kindDef.GetMasterOptions( manager, mode );

            // if the animal is bonded, and we care about bonds, there's no discussion
            if ( RespectBonds )
            {
                var bondee = animal.relations.GetFirstDirectRelationPawn( PawnRelationDefOf.Bond, p => !p.Dead );
                if ( bondee != null && TrainableUtility.CanBeMaster( bondee, animal ) )
                    return bondee;
            }

            Logger.Follow(
                $"Getting master for {animal.LabelShort}:\n\tcurrent: {master?.LabelShort ?? "None"}\n\toptions:\n" );
#if DEBUG_FOLLOW
            foreach ( var option in options )
            {
                Logger.Follow( $"\t\t{option.LabelShort}\n" );
            }
#endif

            // cop out if no options
            if ( options.NullOrEmpty() )
                return null;

            // if we currently have a master, our current master is a valid option, 
            // and all the options have roughly equal amounts of pets following them, we don't need to take action
            if ( master != null && options.Contains( master ) && RoughlyEquallyDistributed( options ) )
                return master;

            // otherwise, assign a master that has the least amount of current followers.
            return options.MinBy( p => p.GetFollowers().Count );
        }

        public void SetFollowing( Pawn animal, bool drafted, bool fieldwork, ref bool actionTaken )
        {
            if ( animal?.playerSettings == null )
            {
                Log.Warning( "NULL!" );
                return;
            }

            Logger.Follow(
                $"Current: {animal.playerSettings.followDrafted} | {animal.playerSettings.followFieldwork}, {drafted} | {fieldwork}" );
            if ( animal.playerSettings.followDrafted != drafted )
            {
                animal.playerSettings.followDrafted = drafted;
                actionTaken                         = true;
            }

            if ( animal.playerSettings.followFieldwork != fieldwork )
            {
                animal.playerSettings.followFieldwork = fieldwork;
                actionTaken                           = true;
            }
        }

        public void SetMaster( Pawn animal, MasterMode mode, Pawn specificMaster, ref bool actionTaken )
        {
            switch ( mode )
            {
                case MasterMode.Default:
                    break;
                case MasterMode.Specific:
                    SetMaster( animal, specificMaster, ref actionTaken );
                    break;
                default:
                    var master = GetMaster( animal, mode );
                    SetMaster( animal, master, ref actionTaken );
                    break;
            }
        }

        public void SetMaster( Pawn animal, Pawn master, ref bool actionTaken )
        {
            Logger.Follow( $"Current: {master?.LabelShort ?? "None"}, New: {master?.LabelShort ?? "None"}" );
            if ( animal.playerSettings.Master != master )
            {
                animal.playerSettings.Master = master;
                actionTaken                  = true;
            }
        }

        public override void Tick()
        {
            if ( _history.IsRelevantTick )
                _history.Update( Trigger.Counts );
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
            // Note that this also has the unfortunate side-effect of not re-adding designations after loading a game.
            _designations = _designations.Intersect( manager.map.designationManager.allDesignations ).ToList();

            // handle butchery
            DoButcherJobs( ref actionTaken );

            // handle training
            DoTrainingJobs( ref actionTaken );

            // area restrictions
            DoAreaRestrictions( ref actionTaken );

            // handle taming
            DoTamingJobs( ref actionTaken );

            // follow settings
            DoFollowSettings( ref actionTaken );

            return actionTaken;
        }

        internal void DoTrainingJobs( ref bool actionTaken, bool assign = true )
        {
            actionTaken = false;

            foreach ( var ageSex in Utilities_Livestock.AgeSexArray )
            {
                // skip juveniles if TrainYoung is not enabled.
                if ( ageSex.Juvenile() && !Training.TrainYoung )
                    continue;

                foreach ( var animal in Trigger.pawnKind.GetTame( manager, ageSex ) )
                {
                    foreach ( var def in Training.Defs )
                        if ( // only train if allowed.
                            animal.training.CanAssignToTrain( def, out _ ).Accepted &&

                            // only assign training, unless unassign is ticked.
                            animal.training.GetWanted( def ) != Training[def] &&
                            ( Training[def] || Training.UnassignTraining ) )
                        {
                            if ( assign ) SetWanted_MI.Invoke( animal.training, new object[] {def, Training[def]} );
                            actionTaken = true;
                        }
                }
            }
        }

        private void DoAreaRestrictions( ref bool actionTaken )
        {
            for ( var i = 0; i < Utilities_Livestock.AgeSexArray.Length; i++ )
                foreach ( var p in Trigger.pawnKind.GetTame( manager, Utilities_Livestock.AgeSexArray[i] ) )
                    // slaughter
                    if ( SendToSlaughterArea &&
                         manager.map.designationManager.DesignationOn( p, DesignationDefOf.Slaughter ) != null )
                    {
                        actionTaken                      = p.playerSettings.AreaRestriction != SlaughterArea;
                        p.playerSettings.AreaRestriction = SlaughterArea;
                    }

                    // hungry
                    else if ( SendToHungryArea && 
                              p.TicksUntilStarving() < UpdateInterval.ticks )
                    {
                        if (p.playerSettings.AreaRestriction != HungryArea)
                        {
                            actionTaken = true;
                            p.playerSettings.AreaRestriction = HungryArea;
                        }
                    }

                    // milking
                    else if ( SendToMilkingArea                                        &&
                              p.GetComp<CompMilkable>()                        != null &&
                              p.GetComp<CompMilkable>().TicksTillHarvestable() < UpdateInterval.ticks )
                    {
                        if ( p.playerSettings.AreaRestriction != MilkArea )
                        {
                            actionTaken                      = true;
                            p.playerSettings.AreaRestriction = MilkArea;
                        }
                    }

                    // shearing
                    else if ( SendToShearingArea                                        &&
                              p.GetComp<CompShearable>()                        != null &&
                              p.GetComp<CompShearable>().TicksTillHarvestable() < UpdateInterval.ticks )
                    {
                        if ( p.playerSettings.AreaRestriction != ShearArea )
                        {
                            actionTaken                      = true;
                            p.playerSettings.AreaRestriction = ShearArea;
                        }
                    }

                    // training
                    else if ( SendToTrainingArea && p.training.NextTrainableToTrain() != null )
                    {
                        if ( p.playerSettings.AreaRestriction != TrainingArea )
                        {
                            actionTaken                      = true;
                            p.playerSettings.AreaRestriction = TrainingArea;
                        }
                    }

                    // all
                    else if ( RestrictToArea && p.playerSettings.AreaRestriction != RestrictArea[i] )
                    {
                        actionTaken                      = true;
                        p.playerSettings.AreaRestriction = RestrictArea[i];
                    }
        }

        private void DoButcherJobs( ref bool actionTaken )
        {
            if ( !ButcherExcess ) return;

#if DEBUG_LIFESTOCK
            Log.Message( "Doing butchery: " + Trigger.pawnKind.LabelCap );
#endif

            foreach ( var ageSex in Utilities_Livestock.AgeSexArray )
            {
                // too many animals?
                var surplus = Trigger.pawnKind.GetTame( manager, ageSex ).Count()
                            - DesignationsOfOn( DesignationDefOf.Slaughter, ageSex ).Count
                            - Trigger.CountTargets[ageSex];

#if DEBUG_LIFESTOCK
                Log.Message( "Butchering " + ageSex + ", surplus" + surplus );
#endif

                if ( surplus > 0 )
                {
                    // should slaughter oldest adults, youngest juveniles.
                    var oldestFirst = ageSex == AgeAndSex.AdultFemale ||
                                      ageSex == AgeAndSex.AdultMale;

                    // get list of animals in correct sort order.
                    var animals = Trigger.pawnKind.GetTame( manager, ageSex )
                                         .Where(
                                              p => manager.map.designationManager.DesignationOn(
                                                       p, DesignationDefOf.Slaughter ) == null
                                                && ( ButcherTrained ||
                                                     !p.training.HasLearned( TrainableDefOf.Obedience ) )
                                                && ( ButcherPregnant || !p.VisiblyPregnant() )
                                                && ( ButcherBonded   || !p.BondedWithColonist() ) )
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
                    }
                }

                // remove extra designations
                while ( surplus < 0 )
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

        private void DoTamingJobs( ref bool actionTaken )
        {
            if ( !TryTameMore ) return;

            foreach ( var ageSex in Utilities_Livestock.AgeSexArray )
            {
                // not enough animals?
                var deficit = Trigger.CountTargets[ageSex]
                            - Trigger.pawnKind.GetTame( manager, ageSex ).Count()
                            - DesignationsOfOn( DesignationDefOf.Tame, ageSex ).Count;

#if DEBUG_LIFESTOCK
                Log.Message( "Taming " + ageSex + ", deficit: " + deficit );
#endif

                if ( deficit > 0 )
                {
                    // get the 'home' position
                    var position = manager.map.GetBaseCenter();

                    // get list of animals in sorted by youngest weighted to distance.
                    var animals = Trigger.pawnKind.GetWild( manager, ageSex )
                                         .Where( p => p != null                                                 &&
                                                      p.Spawned                                                 &&
                                                      manager.map.designationManager.DesignationOn( p ) == null &&
                                                      ( TameArea == null ||
                                                        TameArea.ActiveCells.Contains( p.Position ) ) &&
                                                      IsReachable( p ) ).ToList();

                    // skip if no animals available.
                    if ( animals.Count == 0 )
                        continue;

                    animals =
                        animals.OrderBy( p => p.ageTracker.AgeBiologicalTicks / Distance( p, position ) ).ToList();

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

        private bool RoughlyEquallyDistributed( List<Pawn> masters )
        {
            var followerCounts = masters.Select( p => p.GetFollowers( Trigger.pawnKind ).Count );
            return followerCounts.Max() - followerCounts.Min() <= 1;
        }

        private bool TryRemoveDesignation( AgeAndSex ageSex, DesignationDef def )
        {
            // get current designations
            var currentDesignations = DesignationsOfOn( def, ageSex );

            // if none, return false
            if ( currentDesignations.Count == 0 ) return false;

            // else, remove one from the game as well as our managed list. (delete last - this should be the youngest/oldest).
            var designation = currentDesignations.Last();
            _designations.Remove( designation );
            designation.Delete();
            return true;
        }


        public class TrainingTracker : IExposable
        {
            public DefMap<TrainableDef, bool> TrainingTargets = new DefMap<TrainableDef, bool>();
            public bool                       TrainYoung;
            public bool                       UnassignTraining;

            public bool Any
            {
                get
                {
                    foreach ( var def in Defs )
                        if ( TrainingTargets[def] )
                            return true;

                    return false;
                }
            }

            public int Count => TrainingTargets.Count;

            public List<TrainableDef> Defs => DefDatabase<TrainableDef>.AllDefsListForReading;

            public bool this[ TrainableDef index ]
            {
                get => TrainingTargets[index];
                set => SetWantedRecursive( index, value );
            }

            public void ExposeData()
            {
                Scribe_Values.Look( ref TrainYoung, "TrainYoung" );
                Scribe_Values.Look( ref UnassignTraining, "UnassignTraining" );
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
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                    if ( td.prerequisites != null )
                        foreach ( var trainable in td.prerequisites )
                            SetWantedRecursive( trainable, true );
                }
                else
                {
                    SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                    var enumerable = from t in DefDatabase<TrainableDef>.AllDefsListForReading
                                     where
                                         t.prerequisites != null && t.prerequisites.Contains( td )
                                     select t;
                    foreach ( var current in enumerable ) SetWantedRecursive( current, false );
                }
            }
        }
    }
}