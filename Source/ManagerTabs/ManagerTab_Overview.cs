﻿// Karel Kroeze
// ManagerTab_Overview.cs
// 2016-12-09

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FluffyManager
{
    internal class ManagerTab_Overview : ManagerTab
    {
        #region Fields

        public const float Margin = Utilities.Margin,
                           OverviewWidthRatio = .6f,
                           RowHeight = Utilities.LargeListEntryHeight,
                           RowHeightPawnOverview = 30f,
                           IconSize = 30f;

        public float OverviewHeight = 9999f;
        private Vector2 _overviewScrollPosition = Vector2.zero;
        private ManagerJob _selectedJob;
        private SkillDef _skillDef;
        private Vector2 _workersScrollPosition = Vector2.zero;
        private WorkTypeDef _workType;
        private List<Pawn> Workers = new List<Pawn>();

        #endregion Fields

        #region Constructors

        public ManagerTab_Overview( Manager manager ) : base( manager )
        {
        }

        #endregion Constructors



        #region Properties

        public override Texture2D Icon
        {
            get { return Resources.IconOverview; }
        }

        public override IconAreas IconArea
        {
            get { return IconAreas.Left; }
        }

        public List<ManagerJob> Jobs
        {
            get { return Manager.For( manager ).JobStack.FullStack(); }
        }

        public override string Label { get; } = "FM.Overview".Translate();

        public override ManagerJob Selected
        {
            get
            {
                return _selectedJob;
            }
            set
            {
                _selectedJob = value;
                WorkTypeDef = _selectedJob?.WorkTypeDef ?? Utilities.WorkTypeDefOf_Managing;
                SkillDef = _selectedJob?.SkillDef;
            }
        }

        private SkillDef SkillDef
        {
            get { return _skillDef; }
            set { _skillDef = value; }
        }

        private WorkTypeDef WorkTypeDef
        {
            get
            {
                if ( _workType == null )
                {
                    _workType = Utilities.WorkTypeDefOf_Managing;
                }
                return _workType;
            }
            set
            {
                _workType = value;
                RefreshWorkers();
            }
        }

        #endregion Properties



        #region Methods

        /// <summary>
        /// Draw a square group of ordering buttons for a job in rect.
        /// This is an LOCAL method that within the specified job type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rect"></param>
        /// <param name="job"></param>
        /// <returns></returns>
        public static bool DrawOrderButtons<T>( Rect rect, Manager manager, T job ) where T : ManagerJob
        {
            var ret = false;
            JobStack jobStack = manager.JobStack;

            float width = rect.width / 2,
                  height = rect.height / 2;

            Rect upRect = new Rect( rect.xMin, rect.yMin, width, height ).ContractedBy( 1f ),
                 downRect = new Rect( rect.xMin, rect.yMin + height, width, height ).ContractedBy( 1f ),
                 topRect = new Rect( rect.xMin + width, rect.yMin, width, height ).ContractedBy( 1f ),
                 bottomRect = new Rect( rect.xMin + width, rect.yMin + height, width, height ).ContractedBy( 1f );

            List<T> jobsOfType = jobStack.FullStack<T>();

            bool top = jobsOfType.IndexOf( job ) == 0,
                 bottom = jobsOfType.IndexOf( job ) == jobsOfType.Count - 1;

            if ( !top )
            {
                DrawOrderTooltips( upRect, topRect );
                if ( Widgets.ButtonImage( topRect, Resources.ArrowTop ) )
                {
                    jobStack.TopPriority( job );
                    ret = true;
                }

                if ( Widgets.ButtonImage( upRect, Resources.ArrowUp ) )
                {
                    jobStack.IncreasePriority( job );
                    ret = true;
                }
            }

            if ( !bottom )
            {
                DrawOrderTooltips( downRect, bottomRect, false );
                if ( Widgets.ButtonImage( downRect, Resources.ArrowDown ) )
                {
                    jobStack.DecreasePriority( job );
                    ret = true;
                }

                if ( Widgets.ButtonImage( bottomRect, Resources.ArrowBottom ) )
                {
                    jobStack.BottomPriority( job );
                    ret = true;
                }
            }
            return ret;
        }

        public static void DrawOrderTooltips( Rect step, Rect max, bool up = true )
        {
            if ( up )
            {
                TooltipHandler.TipRegion( step, "FM.OrderUp".Translate() );
                TooltipHandler.TipRegion( max, "FM.OrderTop".Translate() );
            }
            else
            {
                TooltipHandler.TipRegion( step, "FM.OrderDown".Translate() );
                TooltipHandler.TipRegion( max, "FM.OrderBottom".Translate() );
            }
        }

        public override void DoWindowContents( Rect canvas )
        {
            var overviewRect = new Rect( 0f, 0f, OverviewWidthRatio * canvas.width, canvas.height );
            var sideRectUpper = new Rect( overviewRect.xMax + Margin, 0f,
                                          ( 1 - OverviewWidthRatio ) * canvas.width - Margin,
                                          ( canvas.height - Margin ) / 2 );
            var sideRectLower = new Rect( overviewRect.xMax + Margin, sideRectUpper.yMax + Margin, sideRectUpper.width,
                                          sideRectUpper.height - 1 );

            // draw the listing of current jobs.
            Widgets.DrawMenuSection( overviewRect );
            DrawOverview( overviewRect );

            // draw the selected job's details
            Widgets.DrawMenuSection( sideRectUpper );
            if ( _selectedJob != null )
            {
                _selectedJob.DrawOverviewDetails( sideRectUpper );
            }

            // overview of managers & pawns (capable of) doing this job.
            Widgets.DrawMenuSection( sideRectLower );
            DrawPawnOverview( sideRectLower );
        }

        public void DrawOverview( Rect rect )
        {
            if ( Jobs.NullOrEmpty() )
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.grey;
                Widgets.Label( rect, "FM.NoJobs".Translate() );
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            else
            {
                Rect viewRect = rect;
                Rect contentRect = viewRect.AtZero();
                contentRect.height = OverviewHeight;
                if ( OverviewHeight > viewRect.height )
                {
                    contentRect.width -= 16f;
                }

                GUI.BeginGroup( viewRect );
                Widgets.BeginScrollView( viewRect, ref _overviewScrollPosition, contentRect );

                Vector2 cur = Vector2.zero;

                for ( var i = 0; i < Jobs.Count; i++ )
                {
                    var row = new Rect( cur.x, cur.y, contentRect.width, 50f );

                    // highlights
                    if ( i % 2 == 1 )
                    {
                        Widgets.DrawAltRect( row );
                    }
                    if ( Jobs[i] == Selected )
                    {
                        Widgets.DrawHighlightSelected( row );
                    }

                    // go to job icon
                    var iconRect = new Rect( Margin, row.yMin + ( RowHeight - IconSize ) / 2, IconSize, IconSize );
                    if ( Widgets.ButtonImage( iconRect, Jobs[i].Tab.Icon ) )
                    {
                        MainTabWindow_Manager.GoTo( Jobs[i].Tab, Jobs[i] );
                    }

                    // order buttons
                    DrawOrderButtons( new Rect( row.xMax - 50f, row.yMin, 50f, 50f ), Manager.For( manager ), Jobs[i] );

                    // job specific overview.
                    Rect jobRect = row;
                    jobRect.width -= RowHeight + IconSize + 2 * Margin; // - (a + b)?
                    jobRect.x += IconSize + 2 * Margin;
                    Jobs[i].DrawListEntry( jobRect, true, true );
                    Widgets.DrawHighlightIfMouseover( row );
                    if ( Widgets.ButtonInvisible( jobRect ) )
                    {
                        Selected = Jobs[i];
                    }

                    cur.y += 50f;
                }

                GUI.EndScrollView();
                GUI.EndGroup();

                OverviewHeight = cur.y;
            }
        }

        public void DrawPawnOverview( Rect rect )
        {
            // table body viewport
            var tableOutRect = new Rect( 0f, RowHeightPawnOverview, rect.width, rect.height - RowHeightPawnOverview );
            var tableViewRect = new Rect( 0f, RowHeightPawnOverview, rect.width, Workers.Count * RowHeightPawnOverview );
            if ( tableViewRect.height > tableOutRect.height )
            {
                tableViewRect.width -= 16f;
            }

            // column width
            float colWidth = tableViewRect.width / 4 - Margin;

            // column headers
            var nameColumnHeaderRect = new Rect( colWidth * 0, 0f, colWidth, RowHeightPawnOverview );
            var activityColumnHeaderRect = new Rect( colWidth * 1, 0f, colWidth * 2.5f, RowHeightPawnOverview );
            var priorityColumnHeaderRect = new Rect( colWidth * 3.5f, 0f, colWidth * .5f, RowHeightPawnOverview );

            // label for priority column
            string workLabel = Find.PlaySettings.useWorkPriorities
                                   ? "FM.Priority".Translate()
                                   : "FM.Enabled".Translate();

            // begin drawing
            GUI.BeginGroup( rect );

            // draw labels
            Utilities.Label( nameColumnHeaderRect, WorkTypeDef.pawnLabel + "FM.PluralSuffix".Translate(), null,
                             TextAnchor.LowerCenter );
            Utilities.Label( activityColumnHeaderRect, "FM.Activity".Translate(), null, TextAnchor.LowerCenter );
            Utilities.Label( priorityColumnHeaderRect, workLabel, null, TextAnchor.LowerCenter );

            // begin scrolling area
            Widgets.BeginScrollView( tableOutRect, ref _workersScrollPosition, tableViewRect );
            GUI.BeginGroup( tableViewRect );

            // draw pawn rows
            Vector2 cur = Vector2.zero;
            for ( var i = 0; i < Workers.Count; i++ )
            {
                var row = new Rect( cur.x, cur.y, tableViewRect.width, RowHeightPawnOverview );
                if ( i % 2 == 0 )
                {
                    Widgets.DrawAltRect( row );
                }
                try
                {
                    DrawPawnOverviewRow( Workers[i], row );
                }
                catch // pawn death, etc.
                {
                    // rehresh the list and skip drawing untill the next GUI tick.
                    RefreshWorkers();
                    return;
                }

                cur.y += RowHeightPawnOverview;
            }

            // end scrolling area
            GUI.EndGroup();
            Widgets.EndScrollView();

            // done!
            GUI.EndGroup();
        }

        public override void PreOpen()
        {
            RefreshWorkers();
        }

        private void DrawPawnOverviewRow( Pawn pawn, Rect rect )
        {
            // column width
            float colWidth = rect.width / 4 - Margin;

            // cell rects
            var nameRect = new Rect( colWidth * 0, rect.yMin, colWidth, RowHeightPawnOverview );
            var activityRect = new Rect( colWidth * 1, rect.yMin, colWidth * 2.5f, RowHeightPawnOverview );
            var priorityRect = new Rect( colWidth * 3.5f, rect.yMin, colWidth * .5f, RowHeightPawnOverview );

            // name
            Widgets.DrawHighlightIfMouseover( nameRect );

            // on click select and jump to location
            if ( Widgets.ButtonInvisible( nameRect ) )
            {
                Find.MainTabsRoot.EscapeCurrentTab();
                CameraJumper.TryJump( pawn.PositionHeld, pawn.Map );
                Find.Selector.ClearSelection();
                if ( pawn.Spawned )
                {
                    Find.Selector.Select( pawn );
                }
            }
            Utilities.Label( nameRect, pawn.NameStringShort, "FM.ClickToJumpTo".Translate( pawn.LabelCap ),
                             TextAnchor.MiddleLeft, Margin );

            // current activity (if curDriver != null)
            string activityString = pawn.jobs.curDriver?.GetReport() ?? "FM.NoCurJob".Translate();
            Utilities.Label( activityRect, activityString, pawn.jobs.curDriver?.GetReport(), TextAnchor.MiddleCenter,
                             Margin, font: GameFont.Tiny );

            // priority button
            Rect priorityPosition = new Rect( 0f, 0f, 24f, 24f ).CenteredOnXIn( priorityRect )
                                                                .CenteredOnYIn( priorityRect );
            Text.Font = GameFont.Medium;
            WidgetsWork.DrawWorkBoxFor( priorityPosition.xMin, priorityPosition.yMin, pawn, WorkTypeDef, false );
            Text.Font = GameFont.Small;
        }

        private void RefreshWorkers()
        {
            IEnumerable<Pawn> temp =
                manager.map.mapPawns.FreeColonistsSpawned.Where( pawn => !pawn.story.WorkTypeIsDisabled( WorkTypeDef ) );

            // sort by either specific skill def or average over job - depending on which is known.
            temp = SkillDef != null
                       ? temp.OrderByDescending( pawn => pawn.skills.GetSkill( SkillDef ).Level )
                       : temp.OrderByDescending( pawn => pawn.skills.AverageOfRelevantSkillsFor( WorkTypeDef ) );

            Workers = temp.ToList();
        }

        #endregion Methods
    }
}
