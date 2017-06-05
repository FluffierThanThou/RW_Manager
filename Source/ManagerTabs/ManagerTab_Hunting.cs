﻿// Karel Kroeze
// ManagerTab_Hunting.cs
// 2016-12-09

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FluffyManager
{
    internal class ManagerTab_Hunting : ManagerTab
    {
        private static float _entryHeight = 30f;
        private Vector2 _animalsScrollPosition = Vector2.zero;
        private Vector2 _button = new Vector2( 200f, 40f );
        private float _leftRowHeight = 9999f;
        private float _margin = Utilities.Margin;
        private Vector2 _scrollPosition = Vector2.zero;
        private ManagerJob_Hunting _selected;
        private float _topAreaHeight = 30f;

        public List<ManagerJob_Hunting> Jobs;

        public ManagerTab_Hunting( Manager manager ) : base( manager )
        {
            _selected = new ManagerJob_Hunting( manager );
        }

        public override Texture2D Icon
        {
            get { return Resources.IconHunting; }
        }

        public override IconAreas IconArea
        {
            get { return IconAreas.Middle; }
        }

        public override string Label
        {
            get { return "FMH.Hunting".Translate(); }
        }

        public override ManagerJob Selected
        {
            get { return _selected; }
            set { _selected = (ManagerJob_Hunting)value; }
        }

        public void DoContent( Rect rect )
        {
            // layout: settings | animals
            // draw background
            Widgets.DrawMenuSection( rect );

            // some variables
            float width = rect.width;
            float height = rect.height - _topAreaHeight - _button.y - _margin;
            var cols = 2;
            float colWidth = width / cols - _margin;
            var colRects = new List<Rect>();
            var colTitleRects = new List<Rect>();
            var buttonRect = new Rect( rect.width - _button.x, rect.height - _button.y, _button.x - _margin,
                                       _button.y - _margin );

            // set up rects
            for ( var j = 0; j < cols; j++ )
            {
                colRects.Add( new Rect( j * colWidth + j * _margin + _margin / 2, _topAreaHeight, colWidth, height ) );
                colTitleRects.Add( new Rect( j * colWidth + j * _margin + _margin / 2, 0f, colWidth, _topAreaHeight ) );
            }

            // keep track of location
            Vector2 cur = Vector2.zero;

            // begin window
            GUI.BeginGroup( rect );

            // settings.
            Utilities.Label( colTitleRects[0], "FMH.Options".Translate(),
                             anchor: TextAnchor.LowerLeft, lrMargin: _margin * 2, font: GameFont.Tiny );

            GUI.DrawTexture( colRects[0], Resources.SlightlyDarkBackground );
            GUI.BeginGroup( colRects[0] );

            // target count (1)
            int currentCount = _selected.Trigger.CurCount;
            int corpseCount = _selected.GetMeatInCorpses();
            int designatedCount = _selected.GetMeatInDesignations();
            int targetCount = _selected.Trigger.Count;

            _selected.Trigger.DrawTriggerConfig( ref cur, colWidth, _entryHeight, true,
                                                 "FMH.TargetCount".Translate( currentCount, corpseCount, designatedCount,
                                                                              targetCount ),
                                                 "FMH.TargetCountTooltip".Translate( currentCount, corpseCount,
                                                                                     designatedCount, targetCount ) );

            // allow human meat (2)
            var humanMeatRect = new Rect( cur.x, cur.y, colWidth, _entryHeight );
            Utilities.DrawToggle( humanMeatRect, "FMH.AllowHumanMeat".Translate(),
                                  _selected.Trigger.ThresholdFilter.Allows( Utilities_Hunting.HumanMeat ),
                                  delegate
                                  { _selected.AllowHumanLikeMeat( true ); },
                                  delegate
                                  { _selected.AllowHumanLikeMeat( false ); } );
            cur.y += _entryHeight;

            // unforbid corpses (3)
            var ufCorpseRect = new Rect( cur.x, cur.y, colWidth, _entryHeight );
            Widgets.DrawAltRect( ufCorpseRect );
            Utilities.DrawToggle( ufCorpseRect, "FMH.UnforbidCorpses".Translate(), ref _selected.UnforbidCorpses );
            cur.y += _entryHeight;

            // hunting grounds (4)
            var huntingGroundsTitleRect = new Rect( cur.x, cur.y, colWidth - 2 * _margin, _entryHeight );
            Utilities.Label( huntingGroundsTitleRect, "FMH.HuntingGrounds".Translate(), anchor: TextAnchor.MiddleLeft,
                             lrMargin: _margin );
            cur.y += _entryHeight;

            var huntingGroundsRect = new Rect( cur.x + _margin, cur.y, colWidth - 2 * _margin, _entryHeight );
            AreaAllowedGUI.DoAllowedAreaSelectors( huntingGroundsRect, ref _selected.HuntingGrounds, manager,
                                                   AllowedAreaMode.Humanlike );
            cur.y += _entryHeight;

            GUI.EndGroup();

            // animals.
            Utilities.Label( colTitleRects[1], "FMH.Animals".Translate(),
                             anchor: TextAnchor.LowerLeft, lrMargin: _margin * 2, font: GameFont.Tiny );

            GUI.DrawTexture( colRects[1], Resources.SlightlyDarkBackground );
            GUI.BeginGroup( colRects[1] );
            cur = Vector2.zero;

            Rect outRect = colRects[1].AtZero().ContractedBy( 1f );
            var viewRect = new Rect( 0f, 0f, outRect.width, _selected.AllowedAnimals.Count * _entryHeight );
            if ( viewRect.height > outRect.height )
            {
                viewRect.width -= 16f;
            }

            // start scrolling view
            Widgets.BeginScrollView( outRect, ref _animalsScrollPosition, viewRect );

            // list of keys in allowed animals list (all animals in biome, static)
            var PawnKinds = new List<PawnKindDef>( _selected.AllowedAnimals.Keys );

            // toggle all
            var toggleAllRect = new Rect( cur.x, cur.y, colWidth, _entryHeight );
            Widgets.DrawAltRect( toggleAllRect );
            Dictionary<PawnKindDef, bool>.ValueCollection test = _selected.AllowedAnimals.Values;
            Utilities.DrawToggle( toggleAllRect, "<i>" + "FM.All".Translate() + "</i>",
                                  _selected.AllowedAnimals.Values.All( v => v ), delegate
                                                                                     {
                                                                                         foreach (
                                                                                             PawnKindDef def in
                                                                                                 PawnKinds )
                                                                                         {
                                                                                             _selected.AllowedAnimals[
                                                                                                                      def
                                                                                                 ] = true;
                                                                                         }
                                                                                     }, delegate
                                                                                            {
                                                                                                foreach (
                                                                                                    PawnKindDef def in
                                                                                                        PawnKinds )
                                                                                                {
                                                                                                    _selected
                                                                                                        .AllowedAnimals[
                                                                                                                        def
                                                                                                        ] = false;
                                                                                                }
                                                                                            } );

            cur.y += _entryHeight;

            // toggle for each animal
            var i = 1;

            foreach ( PawnKindDef kind in PawnKinds )
            {
                var toggleRect = new Rect( cur.x, cur.y, colWidth, _entryHeight );

                // highlight alternate rows
                if ( i++ % 2 == 0 )
                {
                    Widgets.DrawAltRect( toggleRect );
                }

                // draw the toggle
                Utilities.DrawToggle( toggleRect, kind.LabelCap, _selected.AllowedAnimals[kind],
                                      delegate
                                      { _selected.AllowedAnimals[kind] = !_selected.AllowedAnimals[kind]; } );

                // update current position
                cur.y += _entryHeight;
            }

            // close scrolling view
            Widgets.EndScrollView();

            // close animal list
            GUI.EndGroup();

            // do the button
            if ( !_selected.Managed )
            {
                if ( Widgets.ButtonText( buttonRect, "FM.Manage".Translate() ) )
                {
                    // activate job, add it to the stack
                    _selected.Managed = true;
                    Manager.For( manager ).JobStack.Add( _selected );

                    // refresh source list
                    Refresh();
                }
            }
            else
            {
                if ( Widgets.ButtonText( buttonRect, "FM.Delete".Translate() ) )
                {
                    // inactivate job, remove from the stack.
                    Manager.For( manager ).JobStack.Delete( _selected );

                    // remove content from UI
                    _selected = null;

                    // refresh source list
                    Refresh();
                }
            }

            // close window
            GUI.EndGroup();
        }

        public void DoLeftRow( Rect rect )
        {
            Widgets.DrawMenuSection( rect );

            // content
            float height = _leftRowHeight;
            var scrollView = new Rect( 0f, 0f, rect.width, height );
            if ( height > rect.height )
            {
                scrollView.width -= 16f;
            }

            Widgets.BeginScrollView( rect, ref _scrollPosition, scrollView );
            Rect scrollContent = scrollView;

            GUI.BeginGroup( scrollContent );
            Vector2 cur = Vector2.zero;
            var i = 0;

            foreach ( ManagerJob_Hunting job in Jobs )
            {
                var row = new Rect( 0f, cur.y, scrollContent.width, Utilities.LargeListEntryHeight );
                Widgets.DrawHighlightIfMouseover( row );
                if ( _selected == job )
                {
                    Widgets.DrawHighlightSelected( row );
                }

                if ( i++ % 2 == 1 )
                {
                    Widgets.DrawAltRect( row );
                }

                Rect jobRect = row;

                if ( ManagerTab_Overview.DrawOrderButtons( new Rect( row.xMax - 50f, row.yMin, 50f, 50f ), manager, job ) )
                {
                    Refresh();
                }
                jobRect.width -= 50f;

                job.DrawListEntry( jobRect, false, true );
                if ( Widgets.ButtonInvisible( jobRect ) )
                {
                    _selected = job;
                }

                cur.y += Utilities.LargeListEntryHeight;
            }

            // row for new job.
            var newRect = new Rect( 0f, cur.y, scrollContent.width, Utilities.LargeListEntryHeight );
            Widgets.DrawHighlightIfMouseover( newRect );

            if ( i++ % 2 == 1 )
            {
                Widgets.DrawAltRect( newRect );
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label( newRect, "<" + "FMH.NewHuntingJob".Translate() + ">" );
            Text.Anchor = TextAnchor.UpperLeft;

            if ( Widgets.ButtonInvisible( newRect ) )
            {
                Selected = new ManagerJob_Hunting( manager );
            }

            TooltipHandler.TipRegion( newRect, "FMH.NewHuntingJobTooltip".Translate() );

            cur.y += Utilities.LargeListEntryHeight;

            _leftRowHeight = cur.y;
            GUI.EndGroup();
            Widgets.EndScrollView();
        }

        public override void DoWindowContents( Rect canvas )
        {
            // set up rects
            var leftRow = new Rect( 0f, 0f, DefaultLeftRowSize, canvas.height );
            var contentCanvas = new Rect( leftRow.xMax + _margin, 0f, canvas.width - leftRow.width - _margin,
                                          canvas.height );

            // draw overview row
            DoLeftRow( leftRow );

            // draw job interface if something is selected.
            if ( Selected != null )
            {
                DoContent( contentCanvas );
            }
        }

        public override void PreOpen()
        {
            Refresh();
        }

        public void Refresh()
        {
            Jobs = Manager.For( manager ).JobStack.FullStack<ManagerJob_Hunting>();

            // update pawnkind options
            foreach ( ManagerJob_Hunting job in Jobs )
                job.RefreshAllowedAnimals();
            _selected?.RefreshAllowedAnimals();
        }
    }
}
