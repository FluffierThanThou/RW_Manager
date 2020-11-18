// ManagerTab_Livestock.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static FluffyManager.Constants;
using static FluffyManager.Utilities;
using static FluffyManager.Widgets_Labels;

namespace FluffyManager
{
    public class ManagerTab_Livestock : ManagerTab
    {
        private List<PawnKindDef>          _availablePawnKinds;
        private List<ManagerJob_Livestock> _currentJobs;

        // init with 5's if new job.
        private Dictionary<AgeAndSex, string> _newCounts =
            Utilities_Livestock.AgeSexArray.ToDictionary( k => k, v => "5" );

        private bool                 _onCurrentTab;
        private Vector2              _scrollPosition = Vector2.zero;
        private PawnKindDef          _selectedAvailable;
        private ManagerJob_Livestock _selectedCurrent;
        private float                optionsHeight = 1;

        private Vector2 optionsScrollPosition = Vector2.zero;

        public ManagerTab_Livestock( Manager manager ) : base( manager )
        {
        }

        public override Texture2D Icon => Resources.IconLivestock;

        // public override Texture2D Icon {                       get; }
        public override IconAreas IconArea => IconAreas.Middle;

        public override string Label => "FML.Livestock".Translate();

        public override ManagerJob Selected
        {
            get => _selectedCurrent;
            set
            {
                // set tab to current if we're moving to an actual job.
                // in either case, available selection can be cleared.
                _onCurrentTab      = value != null;
                _selectedAvailable = null;
                _selectedCurrent   = (ManagerJob_Livestock) value;
                _newCounts =
                    _selectedCurrent?.Trigger?.CountTargets.ToDictionary( k => k.Key, v => v.Value.ToString() );
            }
        }

        private List<MasterMode> GetMasterModes => Enum.GetValues( typeof( MasterMode ) ).Cast<MasterMode>().ToList();

        public override void DoWindowContents( Rect canvas )
        {
            var leftRow = new Rect( 0f, 31f, DefaultLeftRowSize, canvas.height - 31f );
            var contentCanvas = new Rect( leftRow.xMax                 + Margin, 0f,
                                          canvas.width - leftRow.width - Margin, canvas.height );

            DoLeftRow( leftRow );
            DoContent( contentCanvas );
        }

        public void DrawTrainingSelector( Rect rect )
        {
            var cellCount = _selectedCurrent.Training.Count;
            var cellWidth = ( rect.width - Margin * ( cellCount - 1 ) ) / cellCount;
            var keys      = _selectedCurrent.Training.Defs;

            GUI.BeginGroup( rect );
            for ( var i = 0; i < _selectedCurrent.Training.Count; i++ )
            {
                var  cell = new Rect( i * ( cellWidth + Margin ), 0f, cellWidth, rect.height );
                bool visible;
                var  report = _selectedCurrent.CanBeTrained( _selectedCurrent.Trigger.pawnKind, keys[i], out visible );
                if ( visible && report.Accepted )
                {
                    var checkOn = _selectedCurrent.Training[keys[i]];
                    DrawToggle( cell, keys[i].LabelCap, keys[i].description, ref checkOn, size: 16f,
                                font: GameFont.Tiny, wrap: false );
                    _selectedCurrent.Training[keys[i]] = checkOn;
                }
                else if ( visible )
                {
                    Label( cell, keys[i].LabelCap, report.Reason, TextAnchor.MiddleCenter, GameFont.Tiny, Color.grey );
                }
            }

            GUI.EndGroup();
        }

        public string GetMasterLabel()
        {
            switch ( _selectedCurrent.Masters )
            {
                case MasterMode.Specific:
                    return _selectedCurrent.Master?.LabelShort ?? "FM.None".Translate();
                default:
                    return $"FM.Livestock.MasterMode.{_selectedCurrent.Masters}".Translate();
            }
        }

        public string GetTrainerLabel()
        {
            switch ( _selectedCurrent.Trainers )
            {
                case MasterMode.Specific:
                    return _selectedCurrent.Trainer.LabelShort;
                default:
                    return $"FM.Livestock.MasterMode.{_selectedCurrent.Trainers}".Translate();
            }
        }

        public override void PreOpen()
        {
            Refresh();
        }

        private void DoContent( Rect rect )
        {
            // background
            Widgets.DrawMenuSection( rect );

            // cop out if nothing is selected.
            if ( _selectedCurrent == null )
            {
                Label( rect, "FM.Livestock.SelectPawnKind".Translate(), TextAnchor.MiddleCenter );
                return;
            }

            // rects
            var optionsColumnRect = new Rect(
                rect.xMin,
                rect.yMin,
                rect.width * 3 / 5f,
                rect.height - Margin - ButtonSize.y );
            var animalsColumnRect = new Rect(
                optionsColumnRect.xMax,
                rect.yMin,
                rect.width * 2 / 5f,
                rect.height - Margin - ButtonSize.y );
            var buttonRect = new Rect(
                rect.xMax    - ButtonSize.x,
                rect.yMax    - ButtonSize.y,
                ButtonSize.x - Margin,
                ButtonSize.y - Margin );

            Vector2 position;
            float   width;
            Widgets_Section.BeginSectionColumn( optionsColumnRect, "Livestock.Options", out position, out width );

            Widgets_Section.Section( ref position, width, DrawTargetCountsSection,
                                     "FM.Livestock.TargetCountsHeader".Translate() );
            Widgets_Section.Section( ref position, width, DrawTamingSection, "FM.Livestock.TamingHeader".Translate() );
            Widgets_Section.Section( ref position, width, DrawButcherSection,
                                     "FM.Livestock.ButcherHeader".Translate() );
            Widgets_Section.Section( ref position, width, DrawTrainingSection,
                                     "FM.Livestock.TrainingHeader".Translate() );
            Widgets_Section.Section( ref position, width, DrawAreaRestrictionsSection,
                                     "FM.Livestock.AreaRestrictionsHeader".Translate() );
            Widgets_Section.Section( ref position, width, DrawFollowSection, "FM.Livestock.FollowHeader".Translate() );

            Widgets_Section.EndSectionColumn( "Livestock.Options", position );

            // Start animals list
            // get our pawnkind
            Widgets_Section.BeginSectionColumn( animalsColumnRect, "Livestock.Animals", out position, out width );

            Widgets_Section.Section( ref position, width, DrawTamedAnimalSection,
                                     "FM.Livestock.AnimalsHeader"
                                        .Translate( "FML.Tame".Translate(),
                                                    _selectedCurrent.Trigger.pawnKind.GetLabelPlural() )
                                        .CapitalizeFirst() );
            Widgets_Section.Section( ref position, width, DrawWildAnimalSection,
                                     "FM.Livestock.AnimalsHeader"
                                        .Translate( "FML.Wild".Translate(),
                                                    _selectedCurrent.Trigger.pawnKind.GetLabelPlural() )
                                        .CapitalizeFirst() );

            Widgets_Section.EndSectionColumn( "Livestock.Animals", position );


            // add / remove to the stack
            if ( _selectedCurrent.Managed )
            {
                if ( Widgets.ButtonText( buttonRect, "FM.Delete".Translate() ) )
                {
                    _selectedCurrent.Delete();
                    _selectedCurrent = null;
                    _onCurrentTab    = false;
                    Refresh();
                    return; // just skip to the next tick to avoid null reference errors.
                }

                TooltipHandler.TipRegion( buttonRect, "FMP.DeleteBillTooltip".Translate() );
            }
            else
            {
                if ( Widgets.ButtonText( buttonRect, "FM.Manage".Translate() ) )
                {
                    _selectedCurrent.Managed = true;
                    _onCurrentTab            = true;
                    Manager.For( manager ).JobStack.Add( _selectedCurrent );
                    Refresh();
                }

                TooltipHandler.TipRegion( buttonRect, "FMP.ManageBillTooltip".Translate() );
            }
        }

        private void DoCountField( Rect rect, AgeAndSex ageSex )
        {
            if ( _newCounts == null || _newCounts[ageSex] == null )
                _newCounts =
                    _selectedCurrent?.Trigger?.CountTargets.ToDictionary( k => k.Key, v => v.Value.ToString() );

            if ( !_newCounts[ageSex].IsInt() )
                GUI.color = Color.red;
            else
                _selectedCurrent.Trigger.CountTargets[ageSex] = int.Parse( _newCounts[ageSex] );
            _newCounts[ageSex] = Widgets.TextField( rect.ContractedBy( 1f ), _newCounts[ageSex] );
            GUI.color          = Color.white;
        }

        private void DoLeftRow( Rect rect )
        {
            // background (minus top line so we can draw tabs.)
            Widgets.DrawMenuSection( rect );

            // tabs
            var tabs = new List<TabRecord>();
            var availableTabRecord = new TabRecord( "FMP.Available".Translate(), delegate
            {
                _onCurrentTab = false;
                Refresh();
            }, !_onCurrentTab );
            tabs.Add( availableTabRecord );
            var currentTabRecord = new TabRecord( "FMP.Current".Translate(), delegate
            {
                _onCurrentTab = true;
                Refresh();
            }, _onCurrentTab );
            tabs.Add( currentTabRecord );

            TabDrawer.DrawTabs( rect, tabs );

            var outRect  = rect;
            var viewRect = outRect.AtZero();

            if ( _onCurrentTab )
                DrawCurrentJobList( outRect, viewRect );
            else
                DrawAvailableJobList( outRect, viewRect );
        }

        private void DrawAnimalListheader( ref Vector2 pos, Vector2 size, PawnKindDef pawnKind )
        {
            var start = pos;

            // use a third of available screenspace for labels
            pos.x += size.x / 3f;

            // gender, lifestage, current meat (and if applicable, milking + shearing)
            var cols = 3;

            // extra columns?
            var milk = pawnKind.Milkable();
            var wool = pawnKind.Shearable();
            if ( milk )
                cols++;
            if ( wool )
                cols++;
            var colwidth = size.x * 2 / 3 / cols;

            // gender header
            var genderRect = new Rect( pos.x, pos.y, colwidth, size.y );
            var genderMale =
                new Rect( 0f, 0f, SmallIconSize, SmallIconSize ).CenteredIn( genderRect, -SmallIconSize / 2 );
            var genderFemale =
                new Rect( 0f, 0f, SmallIconSize, SmallIconSize ).CenteredIn( genderRect, SmallIconSize / 2 );
            GUI.DrawTexture( genderMale, Resources.MaleIcon );
            GUI.DrawTexture( genderFemale, Resources.FemaleIcon );
            TooltipHandler.TipRegion( genderRect, "FML.GenderHeader".Translate() );
            pos.x += colwidth;

            // lifestage header
            var ageRect  = new Rect( pos.x, pos.y, colwidth, size.y );
            var ageRectC = new Rect( 0f, 0f, SmallIconSize, SmallIconSize ).CenteredIn( ageRect, SmallIconSize / 2 );
            var ageRectB = new Rect( 0f, 0f, SmallIconSize, SmallIconSize ).CenteredIn( ageRect );
            var ageRectA = new Rect( 0f, 0f, SmallIconSize, SmallIconSize ).CenteredIn( ageRect, -SmallIconSize / 2 );
            GUI.DrawTexture( ageRectC, Resources.LifeStages( 2 ) );
            GUI.DrawTexture( ageRectB, Resources.LifeStages( 1 ) );
            GUI.DrawTexture( ageRectA, Resources.LifeStages( 0 ) );
            TooltipHandler.TipRegion( ageRect, "FML.AgeHeader".Translate() );
            pos.x += colwidth;

            // meat header
            var meatRect = new Rect( pos.x, pos.y, colwidth, size.y );
            var meatIconRect =
                new Rect( 0f, 0f, SmallIconSize, SmallIconSize ).CenteredIn( meatRect );
            GUI.DrawTexture( meatIconRect, Resources.MeatIcon );
            TooltipHandler.TipRegion( meatRect, "FML.MeatHeader".Translate() );
            pos.x += colwidth;

            // milk header
            if ( milk )
            {
                var milkRect = new Rect( pos.x, pos.y, colwidth, size.y );
                var milkIconRect =
                    new Rect( 0f, 0f, SmallIconSize, SmallIconSize ).CenteredIn( milkRect );
                GUI.DrawTexture( milkIconRect, Resources.MilkIcon );
                TooltipHandler.TipRegion( milkRect, "FML.MilkHeader".Translate() );
                pos.x += colwidth;
            }

            // wool header
            if ( wool )
            {
                var woolRect = new Rect( pos.x, pos.y, colwidth, size.y );
                var woolIconRect =
                    new Rect( 0f, 0f, MediumIconSize, MediumIconSize ).CenteredIn( woolRect );
                GUI.DrawTexture( woolIconRect, Resources.WoolIcon );
                TooltipHandler.TipRegion( woolRect, "FML.WoolHeader".Translate() );
                pos.x += colwidth;
            }

            // start next row
            pos.x =  start.x;
            pos.y += size.y;
        }

        private void DrawAnimalRow( ref Vector2 pos, Vector2 size, Pawn p )
        {
            var start = pos;

            // highlights and interactivity.
            var row = new Rect( pos.x, pos.y, size.x, size.y );
            Widgets.DrawHighlightIfMouseover( row );
            if ( Widgets.ButtonInvisible( row ) )
            {
                // move camera and select
                Find.MainTabsRoot.EscapeCurrentTab();
                CameraJumper.TryJump( p.PositionHeld, p.Map );
                Find.Selector.ClearSelection();
                if ( p.Spawned )
                    Find.Selector.Select( p );
            }

            // use a third of available screenspace for labels
            var nameRect = new Rect( pos.x, pos.y, size.x / 3f, size.y );
            Label( nameRect, p.LabelCap, TextAnchor.MiddleCenter, GameFont.Tiny );
            pos.x += size.x / 3f;

            // gender, lifestage, current meat (and if applicable, milking + shearing)
            var cols = 3;

            // extra columns?
            if ( p.kindDef.Milkable() )
                cols++;
            if ( p.kindDef.Shearable() )
                cols++;

            var colwidth = size.x * 2 / 3 / cols;

            // gender column
            var genderRect = new Rect( pos.x, pos.y, colwidth, size.y );
            var genderIconRect =
                new Rect( 0f, 0f, SmallIconSize, SmallIconSize ).CenteredIn( genderRect );
            switch ( p.gender )
            {
                case Gender.Female:
                    GUI.DrawTexture( genderIconRect, Resources.FemaleIcon );
                    break;

                case Gender.Male:
                    GUI.DrawTexture( genderIconRect, Resources.MaleIcon );
                    break;

                case Gender.None:
                    GUI.DrawTexture( genderIconRect, Resources.UnkownIcon );
                    break;
            }

            TooltipHandler.TipRegion( genderRect, p.gender.GetLabel() );
            pos.x += colwidth;

            // lifestage column
            var ageRect     = new Rect( pos.x, pos.y, colwidth, size.y );
            var ageIconRect = new Rect( 0f, 0f, SmallIconSize, SmallIconSize ).CenteredIn( ageRect );
            GUI.DrawTexture( ageIconRect, Resources.LifeStages( p.ageTracker.CurLifeStageIndex ) );
            TooltipHandler.TipRegion( ageRect, p.ageTracker.AgeTooltipString );
            pos.x += colwidth;

            // meat column
            var meatRect = new Rect( pos.x, pos.y, colwidth, size.y );
            // NOTE: When splitting tabs into separate mods; estimated meat count is defined in the Hunting helper.
            Label( meatRect, p.EstimatedMeatCount().ToString(),
                   "FML.Yields".Translate( p.RaceProps.meatDef.LabelCap, p.EstimatedMeatCount() ),
                   TextAnchor.MiddleCenter, GameFont.Tiny );
            pos.x += colwidth;

            // milk column
            if ( p.Milkable() )
            {
                var milkRect = new Rect( pos.x, pos.y, colwidth, size.y );
                var comp     = p.TryGetComp<CompMilkable>();
                Label( milkRect, comp.Fullness.ToString( "0%" ),
                       "FML.Yields".Translate( comp.Props.milkDef.LabelCap, comp.Props.milkAmount ),
                       TextAnchor.MiddleCenter, GameFont.Tiny );
            }

            if ( p.kindDef.Milkable() )
                pos.x += colwidth;

            // wool column
            if ( p.Shearable() )
            {
                var woolRect = new Rect( pos.x, pos.y, colwidth, size.y );
                var comp     = p.TryGetComp<CompShearable>();
                Label( woolRect, comp.Fullness.ToString( "0%" ),
                       "FML.Yields".Translate( comp.Props.woolDef.LabelCap, comp.Props.woolAmount ),
                       TextAnchor.MiddleCenter, GameFont.Tiny );
            }

            if ( p.kindDef.Milkable() )
                pos.x += colwidth;

            // do the carriage return on ref pos
            pos.x =  start.x;
            pos.y += size.y;
        }

        private float DrawAnimalSection( ref Vector2 pos, float width, string type, PawnKindDef pawnKind,
                                         IEnumerable<Pawn> animals )
        {
            if ( animals == null )
                return 0;

            var start = pos;
            DrawAnimalListheader( ref pos, new Vector2( width, ListEntryHeight / 3 * 2 ), pawnKind );

            if ( !animals.Any() )
            {
                Label( new Rect( pos.x, pos.y, width, ListEntryHeight ),
                       "FML.NoAnimals".Translate( type, pawnKind.GetLabelPlural() ),
                       TextAnchor.MiddleCenter, color: Color.grey );
                pos.y += ListEntryHeight;
            }

            foreach ( var animal in animals )
                DrawAnimalRow( ref pos, new Vector2( width, ListEntryHeight ), animal );

            return pos.y - start.y;
        }

        private float DrawAreaRestrictionsSection( Vector2 pos, float width )
        {
            var start = pos;
            // restrict to area
            var restrictAreaRect = new Rect( pos.x, pos.y, width, ListEntryHeight );

            DrawToggle( restrictAreaRect,
                        "FML.RestrictToArea".Translate(),
                        "FML.RestrictToArea.Tip".Translate(),
                        ref _selectedCurrent.RestrictToArea );
            pos.y += ListEntryHeight;

            if ( _selectedCurrent.RestrictToArea )
            {
                // area selectors table
                // set up a 3x3 table of rects
                var     cols    = 3;
                var     fifth   = width / 5;
                float[] widths  = {fifth, fifth        * 2, fifth * 2};
                float[] heights = {ListEntryHeight * 2 / 3, ListEntryHeight, ListEntryHeight};

                var areaRects = new Rect[cols, cols];
                for ( var x = 0; x < cols; x++ )
                    for ( var y = 0; y < cols; y++ )
                        areaRects[x, y] = new Rect(
                            widths.Take( x ).Sum(),
                            pos.y + heights.Take( y ).Sum(),
                            widths[x],
                            heights[y] );

                // headers
                Label( areaRects[1, 0], Gender.Female.ToString(), TextAnchor.LowerCenter, GameFont.Tiny );
                Label( areaRects[2, 0], Gender.Male.ToString(), TextAnchor.LowerCenter, GameFont.Tiny );
                Label( areaRects[0, 1], "FML.Adult".Translate(), TextAnchor.MiddleRight, GameFont.Tiny );
                Label( areaRects[0, 2], "FML.Juvenile".Translate(), TextAnchor.MiddleRight, GameFont.Tiny );

                // do the selectors
                _selectedCurrent.RestrictArea[0] = AreaAllowedGUI.DoAllowedAreaSelectors( areaRects[1, 1],
                                                                                          _selectedCurrent.RestrictArea[
                                                                                              0], manager, Margin );
                _selectedCurrent.RestrictArea[1] = AreaAllowedGUI.DoAllowedAreaSelectors( areaRects[2, 1],
                                                                                          _selectedCurrent.RestrictArea[
                                                                                              1], manager, Margin );
                _selectedCurrent.RestrictArea[2] = AreaAllowedGUI.DoAllowedAreaSelectors( areaRects[1, 2],
                                                                                          _selectedCurrent.RestrictArea[
                                                                                              2], manager, Margin );
                _selectedCurrent.RestrictArea[3] = AreaAllowedGUI.DoAllowedAreaSelectors( areaRects[2, 2],
                                                                                          _selectedCurrent.RestrictArea[
                                                                                              3], manager, Margin );

                Text.Anchor =  TextAnchor.UpperLeft; // DoAllowedAreaMode leaves the anchor in an incorrect state.
                pos.y       += 3 * ListEntryHeight;
            }

            var sendToSlaughterAreaRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
            pos.y += ListEntryHeight;
            if ( _selectedCurrent.ButcherExcess )
            {
                DrawToggle( sendToSlaughterAreaRect,
                            "FML.SendToSlaughterArea".Translate(),
                            "FML.SendToSlaughterArea.Tip".Translate(),
                            ref _selectedCurrent.SendToSlaughterArea );

                if ( _selectedCurrent.SendToSlaughterArea )
                {
                    var slaughterAreaRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
                    AreaAllowedGUI.DoAllowedAreaSelectors( slaughterAreaRect, ref _selectedCurrent.SlaughterArea,
                                                           manager );
                    pos.y += ListEntryHeight;
                }
            }
            else
            {
                sendToSlaughterAreaRect.xMin += Margin;
                Label( sendToSlaughterAreaRect, "FML.SendToSlaughterArea".Translate(),
                       "FM.Livestock.DisabledBecauseSlaughterExcessDisabled".Translate(), TextAnchor.MiddleLeft,
                       color: Color.grey );
            }

            if (_selectedCurrent.Trigger.pawnKind.RaceProps.EatsFood)
            {
                var sendToHungryAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                pos.y += ListEntryHeight;
                DrawToggle(sendToHungryAreaRect,
                            "FML.SendToHungryArea".Translate(),
                            "FML.SendToHungryArea.Tip".Translate(),
                            ref _selectedCurrent.SendToHungryArea);

                if (_selectedCurrent.SendToHungryArea)
                {
                    var hungryAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                    AreaAllowedGUI.DoAllowedAreaSelectors(hungryAreaRect, ref _selectedCurrent.HungryArea,
                                                           manager);
                    pos.y += ListEntryHeight;
                }
            }

            if ( _selectedCurrent.Trigger.pawnKind.Milkable() )
            {
                var sendToMilkingAreaRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
                pos.y += ListEntryHeight;
                DrawToggle( sendToMilkingAreaRect,
                            "FML.SendToMilkingArea".Translate(),
                            "FML.SendToMilkingArea.Tip".Translate(),
                            ref _selectedCurrent.SendToMilkingArea );

                if ( _selectedCurrent.SendToMilkingArea )
                {
                    var milkingAreaRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
                    AreaAllowedGUI.DoAllowedAreaSelectors( milkingAreaRect, ref _selectedCurrent.MilkArea,
                                                           manager );
                    pos.y += ListEntryHeight;
                }
            }

            if ( _selectedCurrent.Trigger.pawnKind.Shearable() )
            {
                var sendToShearingAreaRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
                pos.y += ListEntryHeight;
                DrawToggle( sendToShearingAreaRect,
                            "FML.SendToShearingArea".Translate(),
                            "FML.SendToShearingArea.Tip".Translate(),
                            ref _selectedCurrent.SendToShearingArea );

                if ( _selectedCurrent.SendToShearingArea )
                {
                    var shearingAreaRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
                    AreaAllowedGUI.DoAllowedAreaSelectors( shearingAreaRect, ref _selectedCurrent.ShearArea,
                                                           manager );
                    pos.y += ListEntryHeight;
                }
            }

            var sendToTrainingAreaRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
            pos.y += ListEntryHeight;
            if ( _selectedCurrent.Training.Any )
            {
                DrawToggle( sendToTrainingAreaRect,
                            "FML.SendToTrainingArea".Translate(),
                            "FML.SendToTrainingArea.Tip".Translate(),
                            ref _selectedCurrent.SendToTrainingArea );

                if ( _selectedCurrent.SendToTrainingArea )
                {
                    var trainingAreaRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
                    AreaAllowedGUI.DoAllowedAreaSelectors( trainingAreaRect, ref _selectedCurrent.TrainingArea,
                                                           manager );
                    pos.y += ListEntryHeight;
                }
            }
            else
            {
                sendToTrainingAreaRect.xMin += Margin;
                Label( sendToTrainingAreaRect, "FML.SendToTrainingArea".Translate(),
                       "FM.Livestock.DisabledBecauseNoTrainingSet".Translate(), TextAnchor.MiddleLeft,
                       color: Color.grey );
            }

            return pos.y - start.y;
        }

        private void DrawAvailableJobList( Rect outRect, Rect viewRect )
        {
            // set sizes
            viewRect.height = _availablePawnKinds.Count * LargeListEntryHeight;
            if ( viewRect.height > outRect.height )
                viewRect.width -= ScrollbarWidth;

            Widgets.BeginScrollView( outRect, ref _scrollPosition, viewRect );
            GUI.BeginGroup( viewRect );

            for ( var i = 0; i < _availablePawnKinds.Count; i++ )
            {
                // set up rect
                var row = new Rect( 0f, LargeListEntryHeight * i, viewRect.width, LargeListEntryHeight );

                // highlights
                Widgets.DrawHighlightIfMouseover( row );
                if ( i % 2                  == 0 ) Widgets.DrawAltRect( row );
                if ( _availablePawnKinds[i] == _selectedAvailable ) Widgets.DrawHighlightSelected( row );

                // draw label
                var label = _availablePawnKinds[i].LabelCap + "\n<i>" +
                            "FML.TameWildCount".Translate(
                                _availablePawnKinds[i].GetTame( manager ).Count(),
                                _availablePawnKinds[i].GetWild( manager ).Count() ) + "</i>";
                Label( row, label, TextAnchor.MiddleLeft, margin: Margin * 2 );

                // button
                if ( Widgets.ButtonInvisible( row ) )
                {
                    _selectedAvailable =
                        _availablePawnKinds[i];                                             // for highlighting to work
                    Selected = new ManagerJob_Livestock( _availablePawnKinds[i], manager ); // for details
                }
            }

            GUI.EndGroup();
            Widgets.EndScrollView();
        }

        private float DrawButcherSection( Vector2 pos, float width )
        {
            var start = pos;

            // butchery stuff
            var butcherExcessRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
            DrawToggle( butcherExcessRect,
                        "FML.ButcherExcess".Translate(),
                        "FML.ButcherExcess.Tip".Translate(),
                        ref _selectedCurrent.ButcherExcess );
            pos.y += ListEntryHeight;

            if ( _selectedCurrent.ButcherExcess )
            {
                var cellWidth         = ( width - Margin * 2 ) / 3f;
                var butcherOptionRect = new Rect( pos.x, pos.y, cellWidth, ListEntryHeight );

                DrawToggle( butcherOptionRect,
                            "FML.ButcherTrained".Translate(),
                            "FML.ButcherTrained.Tip".Translate(),
                            ref _selectedCurrent.ButcherTrained, font: GameFont.Tiny, wrap: false );
                butcherOptionRect.x += cellWidth + Margin;

                DrawToggle( butcherOptionRect,
                            "FML.ButcherPregnant".Translate(),
                            "FML.ButcherPregnant.Tip".Translate(),
                            ref _selectedCurrent.ButcherPregnant, font: GameFont.Tiny, wrap: false );
                butcherOptionRect.x += cellWidth + Margin;

                DrawToggle( butcherOptionRect,
                            "FML.ButcherBonded".Translate(),
                            "FML.ButcherBonded.Tip".Translate(),
                            ref _selectedCurrent.ButcherBonded, font: GameFont.Tiny, wrap: false );

                pos.y += ListEntryHeight;
            }

            return pos.y - start.y;
        }

        private void DrawCurrentJobList( Rect outRect, Rect viewRect )
        {
            // set sizes
            viewRect.height = _currentJobs.Count * LargeListEntryHeight;
            if ( viewRect.height > outRect.height )
                viewRect.width -= ScrollbarWidth;

            Widgets.BeginScrollView( outRect, ref _scrollPosition, viewRect );
            GUI.BeginGroup( viewRect );

            for ( var i = 0; i < _currentJobs.Count; i++ )
            {
                // set up rect
                var row = new Rect( 0f, LargeListEntryHeight * i, viewRect.width, LargeListEntryHeight );

                // highlights
                Widgets.DrawHighlightIfMouseover( row );
                if ( i % 2           == 0 ) Widgets.DrawAltRect( row );
                if ( _currentJobs[i] == _selectedCurrent ) Widgets.DrawHighlightSelected( row );

                // draw label
                _currentJobs[i].DrawListEntry( row, false );

                // button
                if ( Widgets.ButtonInvisible( row ) ) Selected = _currentJobs[i];
            }

            GUI.EndGroup();
            Widgets.EndScrollView();
        }

        private float DrawFollowSection( Vector2 pos, float width )
        {
            var start   = pos;
            var rowRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
            var buttonRect = new Rect(
                    rowRect.xMax * 3 / 4,
                    0f,
                    width           * 1 / 4,
                    ListEntryHeight * 2 / 3 )
               .CenteredOnYIn( rowRect );

            // master selection
            Label( rowRect, "FM.Livestock.MasterDefault".Translate(), "FM.Livestock.MasterDefault.Tip".Translate(),
                   TextAnchor.MiddleLeft, margin: Margin );
            if ( Widgets.ButtonText( buttonRect, GetMasterLabel() ) )
            {
                var options = new List<FloatMenuOption>();

                // modes
                foreach ( var _mode in GetMasterModes.Where( mm => ( mm & MasterMode.All ) == mm ) )
                    options.Add( new FloatMenuOption( $"FM.Livestock.MasterMode.{_mode}".Translate(),
                                                      () => _selectedCurrent.Masters = _mode ) );

                // specific pawns
                foreach ( var pawn in _selectedCurrent.Trigger.pawnKind.GetMasterOptions( manager, MasterMode.All ) )
                    options.Add( new FloatMenuOption(
                                     "FM.Livestock.Master".Translate( pawn.LabelShort,
                                                                      pawn.skills.AverageOfRelevantSkillsFor(
                                                                          WorkTypeDefOf.Handling ) ),
                                     () =>
                                     {
                                         _selectedCurrent.Master  = pawn;
                                         _selectedCurrent.Masters = MasterMode.Specific;
                                     } ) );

                Find.WindowStack.Add( new FloatMenu( options ) );
            }

            // respect bonds?
            rowRect.y += ListEntryHeight;
            if ( _selectedCurrent.Masters != MasterMode.Default && _selectedCurrent.Masters != MasterMode.Specific )
                DrawToggle( rowRect,
                            "FM.Livestock.RespectBonds".Translate(),
                            "FM.Livestock.RespectBonds.Tip".Translate(),
                            ref _selectedCurrent.RespectBonds );
            else
                Label( rowRect,
                       "FM.Livestock.RespectBonds".Translate(),
                       "FM.Livestock.RespectBonds.DisabledBecauseMastersNotSet".Translate(),
                       color: Color.grey, margin: Margin );

            // default follow
            rowRect.y += ListEntryHeight;
            DrawToggle( rowRect,
                        "FM.Livestock.Follow".Translate(),
                        "FM.Livestock.Follow.Tip".Translate(),
                        ref _selectedCurrent.SetFollow );

            if ( _selectedCurrent.SetFollow )
            {
                rowRect.y += ListEntryHeight;
                var followRect = rowRect;
                followRect.width /= 2f;
                DrawToggle( followRect,
                            "FM.Livestock.FollowDrafted".Translate(),
                            "FM.Livestock.FollowDrafted.Tip".Translate(),
                            ref _selectedCurrent.FollowDrafted,
                            font: GameFont.Tiny );
                followRect.x += followRect.width;
                DrawToggle( followRect,
                            "FM.Livestock.FollowFieldwork".Translate(),
                            "FM.Livestock.FollowFieldwork.Tip".Translate(),
                            ref _selectedCurrent.FollowFieldwork,
                            font: GameFont.Tiny );
            }

            // follow when training
            rowRect.y += ListEntryHeight;
            TooltipHandler.TipRegion( rowRect, "FM.Livestock.FollowTraining.Tip".Translate() );
            DrawToggle( rowRect,
                        "FM.Livestock.FollowTraining".Translate(),
                        "FM.Livestock.FollowTraining.Tip".Translate(),
                        ref _selectedCurrent.FollowTraining );

            // master selection
            if ( _selectedCurrent.FollowTraining )
            {
                rowRect.y += ListEntryHeight;
                Label( rowRect, "FM.Livestock.MasterTraining".Translate(),
                       "FM.Livestock.MasterTraining.Tip".Translate(),
                       TextAnchor.MiddleLeft, margin: Margin );

                buttonRect = buttonRect.CenteredOnYIn( rowRect );
                if ( Widgets.ButtonText( buttonRect, GetTrainerLabel() ) )
                {
                    var options = new List<FloatMenuOption>();

                    // modes
                    foreach ( var _mode in GetMasterModes.Where( mm => ( mm & MasterMode.Trainers ) == mm ) )
                        options.Add( new FloatMenuOption( $"FM.Livestock.MasterMode.{_mode}".Translate(),
                                                          () => _selectedCurrent.Trainers = _mode ) );

                    // specific pawns
                    foreach ( var pawn in _selectedCurrent.Trigger.pawnKind.GetTrainers( manager, MasterMode.Trainers )
                    )
                        options.Add( new FloatMenuOption(
                                         "FM.Livestock.Master".Translate( pawn.LabelShort,
                                                                          pawn.skills.AverageOfRelevantSkillsFor(
                                                                              WorkTypeDefOf.Handling ) ),
                                         () =>
                                         {
                                             _selectedCurrent.Trainer  = pawn;
                                             _selectedCurrent.Trainers = MasterMode.Specific;
                                         } ) );

                    Find.WindowStack.Add( new FloatMenu( options ) );
                }
            }

            return rowRect.yMax - start.y;
        }

        private float DrawTamedAnimalSection( Vector2 pos, float width )
        {
            var pawnKind = _selectedCurrent.Trigger.pawnKind;
            var animals  = pawnKind?.GetTame( manager );
            return DrawAnimalSection( ref pos, width, "FML.Tame".Translate(), pawnKind, animals );
        }

        private float DrawTamingSection( Vector2 pos, float width )
        {
            var start = pos;
            DrawToggle( ref pos, width,
                        "FML.TameMore".Translate(),
                        "FML.TameMore.Tip".Translate(),
                        ref _selectedCurrent.TryTameMore );

            // area to tame from (if taming more);
            if ( _selectedCurrent.TryTameMore )
            {
                AreaAllowedGUI.DoAllowedAreaSelectors( ref pos, width, ref _selectedCurrent.TameArea, manager );
                DrawReachabilityToggle( ref pos, width, ref _selectedCurrent.CheckReachable );
                DrawToggle( ref pos, width,
                            "FM.PathBasedDistance".Translate(),
                            "FM.PathBasedDistance.Tip".Translate(),
                            ref _selectedCurrent.PathBasedDistance, true );
            }

            return pos.y - start.y;
        }

        private float DrawTargetCountsSection( Vector2 pos, float width )
        {
            // counts table
            var     cols    = 3;
            var     rows    = 3;
            var     fifth   = width / 5;
            float[] widths  = {fifth, fifth        * 2, fifth * 2};
            float[] heights = {ListEntryHeight * 2 / 3, ListEntryHeight, ListEntryHeight};

            // set up a 3x3 table of rects
            var countRects = new Rect[rows, cols];
            for ( var x = 0; x < cols; x++ )
            {
                for ( var y = 0; y < rows; y++ )
                    // kindof overkill for a 3x3 table, but ok.
                    countRects[y, x] = new Rect(
                        pos.x + widths.Take( x ).Sum(),
                        pos.y + heights.Take( y ).Sum(),
                        widths[x],
                        heights[y] );
            }

            // headers
            Label( countRects[0, 1], Gender.Female.ToString(), TextAnchor.LowerCenter, GameFont.Tiny );
            Label( countRects[0, 2], Gender.Male.ToString(), TextAnchor.LowerCenter, GameFont.Tiny );
            Label( countRects[1, 0], "FML.Adult".Translate(), TextAnchor.MiddleRight, GameFont.Tiny );
            Label( countRects[2, 0], "FML.Juvenile".Translate(), TextAnchor.MiddleRight, GameFont.Tiny );

            // fields
            DoCountField( countRects[1, 1], AgeAndSex.AdultFemale );
            DoCountField( countRects[1, 2], AgeAndSex.AdultMale );
            DoCountField( countRects[2, 1], AgeAndSex.JuvenileFemale );
            DoCountField( countRects[2, 2], AgeAndSex.JuvenileMale );

            return 3 * ListEntryHeight;
        }

        private float DrawTrainingSection( Vector2 pos, float width )
        {
            var trainingRect = new Rect( pos.x, pos.y, width, ListEntryHeight );
            DrawTrainingSelector( trainingRect );
            var height = ListEntryHeight;

            var unassignTrainingRect = new Rect( pos.x, pos.y + height, width, ListEntryHeight );
            DrawToggle( unassignTrainingRect,
                        "FML.UnassignTraining".Translate(),
                        "FML.UnassignTraining.Tip".Translate(),
                        ref _selectedCurrent.Training.UnassignTraining );
            height += ListEntryHeight;

            if ( _selectedCurrent.Training.Any )
            {
                var trainYoungRect = new Rect( pos.x, pos.y + height, width, ListEntryHeight );
                DrawToggle( trainYoungRect,
                            "FML.TrainYoung".Translate(),
                            "FML.TrainYoung.Tip".Translate(),
                            ref _selectedCurrent.Training.TrainYoung );
                height += ListEntryHeight;
            }

            return height;
        }

        private float DrawWildAnimalSection( Vector2 pos, float width )
        {
            var pawnKind = _selectedCurrent.Trigger.pawnKind;
            var animals  = pawnKind?.GetWild( manager );
            return DrawAnimalSection( ref pos, width, "FML.Wild".Translate(), pawnKind, animals );
        }

        private void Refresh()
        {
            // currently managed
            _currentJobs = Manager.For( manager ).JobStack.FullStack<ManagerJob_Livestock>();

            // concatenate lists of animals on biome and animals in colony.
            _availablePawnKinds = manager.map.Biome.AllWildAnimals.ToList();
            _availablePawnKinds.AddRange(
                manager.map.mapPawns.AllPawns
                       .Where( p => p.RaceProps.Animal )
                       .Select( p => p.kindDef ) );
            _availablePawnKinds = _availablePawnKinds

                                  // get distinct pawnkinds from the merges
                                 .Distinct()

                                  // remove already managed pawnkinds
                                 .Where( pk => !_currentJobs.Select( job => job.Trigger.pawnKind ).Contains( pk ) )

                                  // order by label
                                 .OrderBy( def => def.LabelCap.RawText )
                                 .ToList();
        }
    }
}