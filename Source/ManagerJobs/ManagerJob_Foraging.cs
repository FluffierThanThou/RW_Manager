﻿// Karel Kroeze
// ManagerJob_Foraging.cs
// 2016-12-09

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FluffyManager
{
    public class ManagerJob_Foraging : ManagerJob
    {
        #region Fields

        public Dictionary<ThingDef, bool> AllowedPlants = new Dictionary<ThingDef, bool>();
        public Area ForagingArea;
        public bool ForceFullyMature;
        public History History;
        public new Trigger_Threshold Trigger;
        private Utilities.CachedValue<int> _cachedCurrentDesignatedCount = new Utilities.CachedValue<int>( 0, 250 );

        private float _margin = Utilities.Margin;

        private List<Designation> Designations = new List<Designation>();

        #endregion Fields

        #region Constructors

        public ManagerJob_Foraging( Manager manager ) : base( manager )
        {
            // populate the trigger field, count all harvested thingdefs from the allowed plant list
            Trigger = new Trigger_Threshold( this );

            // create History tracker
            History = new History( new[] { "stock", "designated" }, new[] { Color.white, Color.grey } );

            // init stuff if we're not loading
            // todo: please, please refactor this into something less clumsy!
            if (Scribe.mode == LoadSaveMode.Inactive)
                RefreshAllowedPlants();
        }

        #endregion Constructors



        #region Properties

        public override bool Completed => !Trigger.State;

        public int CurrentDesignatedCount
        {
            get
            {
                var count = 0;

                // see if we have a cached count
                if ( _cachedCurrentDesignatedCount.TryGetValue( out count ) )
                    return count;

                // fetch count
                foreach ( Designation des in Designations )
                {
                    if ( !des.target.HasThing )
                        continue;

                    var plant = des.target.Thing as Plant;

                    if ( plant == null )
                        continue;

                    count += plant.YieldNow();
                }

                _cachedCurrentDesignatedCount.Update( count );
                return count;
            }
        }

        public override string Label => "FMG.Foraging".Translate();

        public override ManagerTab Tab => Manager.For( manager ).ManagerTabs.Find( tab => tab is ManagerTab_Foraging );

        public override string[] Targets
            => AllowedPlants.Keys.Where( key => AllowedPlants[key] ).Select( plant => plant.LabelCap ).ToArray();

        public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Growing;

        #endregion Properties



        #region Methods

        public void AddRelevantGameDesignations()
        {
            // get list of game designations not managed by this job that could have been assigned by this job.
            foreach (
                Designation des in manager.map.designationManager.SpawnedDesignationsOfDef( DesignationDefOf.HarvestPlant )
                                          .Except( Designations )
                                          .Where( des => IsValidForagingTarget( des.target ) ) )
            {
                AddDesignation( des );
            }
        }

        /// <summary>
        /// Remove designations in our managed list that are not in the game's designation manager.
        /// </summary>
        public void CleanDeadDesignations()
        {
            IEnumerable<Designation> _gameDesignations =
                manager.map.designationManager.SpawnedDesignationsOfDef( DesignationDefOf.HarvestPlant );
            Designations = Designations.Intersect( _gameDesignations ).ToList();
        }

        /// <summary>
        /// Clean up all outstanding designations
        /// </summary>
        public override void CleanUp()
        {
            CleanDeadDesignations();
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
            Rect labelRect = new Rect( _margin, _margin, rect.width -
                                                         ( active ? StatusRectWidth + 4 * _margin : 2 * _margin ),
                                       rect.height - 2 * _margin ),
                 statusRect = new Rect( labelRect.xMax + _margin, _margin, StatusRectWidth, rect.height - 2 * _margin );

            // create label string
            string text = Label + "\n";
            string subtext = string.Join( ", ", Targets );
            if ( subtext.Fits( labelRect ) )
                text += subtext.Italic();
            else
                text += "multiple".Translate().Italic();

            // do the drawing
            GUI.BeginGroup( rect );

            // draw label
            Utilities.Label( labelRect, text, subtext, TextAnchor.MiddleLeft, _margin );

            // if the bill has a manager job, give some more info.
            if ( active )
            {
                this.DrawStatusForListEntry( statusRect, Trigger );
            }
            GUI.EndGroup();
        }

        public override void DrawOverviewDetails( Rect rect )
        {
            History.DrawPlot( rect, Trigger.Count );
        }

        public override void ExposeData()
        {
            // scribe base things
            base.ExposeData();

            // settings, references first!
            Scribe_References.Look( ref ForagingArea, "ForagingArea" );
            Scribe_Deep.Look( ref Trigger, "trigger", manager );
            Scribe_Collections.Look( ref AllowedPlants, "AllowedPlants", LookMode.Def, LookMode.Value );
            Scribe_Values.Look( ref ForceFullyMature, "ForceFullyMature", false );

            if ( Manager.LoadSaveMode == Manager.Modes.Normal )
            {
                // scribe history
                Scribe_Deep.Look( ref History, "History" );
            }
        }

        public override void Tick()
        {
            History.Update( Trigger.CurCount, CurrentDesignatedCount );
        }

        public override bool TryDoJob()
        {
            // keep track of work done
            var workDone = false;

            // clean up designations that were completed.
            CleanDeadDesignations();

            // clean up designations that are (now) in the wrong area.
            CleanAreaDesignations();

            // add designations in the game that could have been handled by this job
            AddRelevantGameDesignations();

            // designate plants until trigger is met.
            int count = Trigger.CurCount + CurrentDesignatedCount;
            if ( count < Trigger.Count )
            {
                List<Plant> targets = GetValidForagingTargetsSorted();

                for ( var i = 0; i < targets.Count && count < Trigger.Count; i++ )
                {
                    var des = new Designation( targets[i], DesignationDefOf.HarvestPlant );
                    count += targets[i].YieldNow();
                    AddDesignation( des );
                    workDone = true;
                }
            }

            return workDone;
        }

        private void AddDesignation( Designation des )
        {
            // add to game
            manager.map.designationManager.AddDesignation( des );

            // add to internal list
            Designations.Add( des );
        }

        private void CleanAreaDesignations()
        {
            foreach ( Designation des in Designations )
            {
                if ( !des.target.HasThing )
                {
                    des.Delete();
                }

                // if area is not null and does not contain designate location, remove designation.
                else if ( !ForagingArea?.ActiveCells.Contains( des.target.Thing.Position ) ?? false )
                {
                    des.Delete();
                }
            }
        }

        private List<Plant> GetValidForagingTargetsSorted()
        {
            IntVec3 position = manager.map.GetBaseCenter();

            return manager.map.listerThings.AllThings
                          .Where( IsValidForagingTarget )

                          // OrderBy defaults to ascending, switch sign on current yield to get descending
                          .Select( p => p as Plant )
                          .OrderBy( p => -p.YieldNow() / ( Math.Sqrt( position.DistanceToSquared( p.Position ) ) * 2 ) )
                          .ToList();
        }

        private bool IsValidForagingTarget( LocalTargetInfo t )
        {
            return t.HasThing
                   && IsValidForagingTarget( t.Thing );
        }

        private bool IsValidForagingTarget( Thing t )
        {
            return t is Plant
                   && IsValidForagingTarget( (Plant)t );
        }

        private bool IsValidForagingTarget( Plant p )
        {
            // should be a plant, and be on the same map as this job
            return p.def.plant != null
                   && p.Map == manager.map

                   // non-biome plants won't be on the list, also filters non-yield or wood plants
                   && AllowedPlants.ContainsKey( p.def )
                   && AllowedPlants[p.def]
                   && p.Spawned
                   && manager.map.designationManager.DesignationOn( p ) == null

                   // cut only mature plants, or non-mature that yield something right now.
                   && ( ( !ForceFullyMature && p.YieldNow() > 1 )
                        || p.LifeStage == PlantLifeStage.Mature )

                   // limit to area of interest
                   && ( ForagingArea == null
                        || ForagingArea.ActiveCells.Contains( p.Position ) )

                   // can we reach it? ( can it reach us? )
                   && manager.map.reachability.CanReachColony( p.Position );
        }



        #endregion Methods

        public void RefreshAllowedPlants( bool firstTime = false )
        {
            // all plants that yield something, and it isn't wood.
            var options = manager.map.Biome.AllWildPlants
                                 .Where( plant => plant.plant.harvestYield > 0 &&
                                                  plant.plant.harvestedThingDef != null &&
                                                  plant.plant.harvestTag != "Wood" )
                                // Caveworld Fauna
                                 .Concat( DefDatabase<ThingDef>
                                              .AllDefsListForReading
                                              .Where( def => def.plant?.sowTags.Contains( "Fungiponics" ) ?? false ) )
                                 .Distinct();

            foreach ( ThingDef plant in options )
            {
                if (!AllowedPlants.ContainsKey( plant) )
                    AllowedPlants.Add( plant, false );
            }

            if ( firstTime )
            {
                Trigger.ThresholdFilter.SetDisallowAll();
                foreach ( ThingDef plant in AllowedPlants.Keys )
                {
                    Trigger.ThresholdFilter.SetAllow( plant.plant.harvestedThingDef, true );
                }
            }
        }
    }
}
