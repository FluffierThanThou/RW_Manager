﻿// Karel Kroeze
// Trigger_Threshold.cs
// 2016-12-09

using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace FluffyManager
{
    public class Trigger_Threshold : Trigger
    {
        #region Fields

        public static int DefaultCount = 500;

        public static int DefaultMaxUpperThreshold = 3000;

        public int Count;

        public int MaxUpperThreshold;

        public Ops Op;

        public Zone_Stockpile stockpile;

        public ThingFilter ThresholdFilter;

        #endregion Fields

        private string _stockpile_scribe;

        #region Constructors

        public Trigger_Threshold( Manager manager ) : base( manager ) { }
        
        public Trigger_Threshold( ManagerJob_Hunting job ) : base( job.manager )
        {
            Op = Ops.LowerThan;
            MaxUpperThreshold = DefaultMaxUpperThreshold;
            Count = DefaultCount;
            ThresholdFilter = new ThingFilter();
            ThresholdFilter.SetDisallowAll();
            ThresholdFilter.SetAllow( Utilities_Hunting.RawMeat, true );
        }

        public Trigger_Threshold( ManagerJob_Forestry job ) : base( job.manager )
        {
            Op = Ops.LowerThan;
            MaxUpperThreshold = DefaultMaxUpperThreshold;
            Count = DefaultCount;
            ThresholdFilter = new ThingFilter();
            ThresholdFilter.SetDisallowAll();
            ThresholdFilter.SetAllow( Utilities_Forestry.Wood, true );
        }

        public Trigger_Threshold( ManagerJob_Foraging job ) : base( job.manager )
        {
            Op = Ops.LowerThan;
            MaxUpperThreshold = DefaultMaxUpperThreshold;
            Count = DefaultCount;
            ThresholdFilter = new ThingFilter();
            ThresholdFilter.SetDisallowAll();
        }

        #endregion Constructors



        #region Enums

        public enum Ops
        {
            LowerThan,
            Equals,
            HigherThan
        }

        #endregion Enums

        public int CurCount => manager.map.CountProducts( ThresholdFilter, stockpile );

        public WindowTriggerThresholdDetails DetailsWindow
        {
            get
            {
                var window = new WindowTriggerThresholdDetails
                {
                    Trigger = this,
                    closeOnClickedOutside = true,
                    draggable = true
                };
                return window;
            }
        }

        public bool IsValid
        {
            get { return ThresholdFilter.AllowedDefCount > 0; }
        }

        public virtual string OpString
        {
            get
            {
                switch ( Op )
                {
                    case Ops.LowerThan:
                        return " < ";

                    case Ops.Equals:
                        return " = ";

                    case Ops.HigherThan:
                        return " > ";

                    default:
                        return " ? ";
                }
            }
        }

        public override bool State
        {
            get
            {
                switch ( Op )
                {
                    case Ops.LowerThan:
                        return CurCount < Count;

                    case Ops.Equals:
                        return CurCount == Count;

                    case Ops.HigherThan:
                        return CurCount > Count;

                    default:
                        Log.Warning( "Trigger_ThingThreshold was defined without a correct operator" );
                        return true;
                }
            }
        }

        public override string StatusTooltip
        {
            get { return "FMP.ThresholdCount".Translate( CurCount, Count ); }
        }

        public override void DrawProgressBar( Rect rect, bool active )
        {
            // bar always goes a little beyond the actual target
            int max = Math.Max( (int)( Count * 1.2f ), CurCount );

            // draw a box for the bar
            GUI.color = Color.gray;
            Widgets.DrawBox( rect.ContractedBy( 1f ) );
            GUI.color = Color.white;

            // get the bar rect
            Rect barRect = rect.ContractedBy( 2f );
            float unit = barRect.height / max;
            float markHeight = barRect.yMin + ( max - Count ) * unit;
            barRect.yMin += ( max - CurCount ) * unit;

            // draw the bar
            // if the job is active and pending, make the bar blueish green - otherwise white.
            Texture2D barTex = active
                                   ? Resources.BarBackgroundActiveTexture
                                   : Resources.BarBackgroundInactiveTexture;
            GUI.DrawTexture( barRect, barTex );

            // draw a mark at the treshold
            Widgets.DrawLineHorizontal( rect.xMin, markHeight, rect.width );

            TooltipHandler.TipRegion( rect, StatusTooltip );
        }

        public override void DrawTriggerConfig( ref Vector2 cur, float width, float entryHeight, bool alt = false,
                                                string label = null, string tooltip = null )
        {
            // target threshold
            var thresholdLabelRect = new Rect( cur.x, cur.y, width, entryHeight );
            if ( alt )
            {
                Widgets.DrawAltRect( thresholdLabelRect );
            }
            Widgets.DrawHighlightIfMouseover( thresholdLabelRect );
            if ( label.NullOrEmpty() )
            {
                label = "FMP.ThresholdCount".Translate( CurCount, Count ) + ":";
            }
            if ( tooltip.NullOrEmpty() )
            {
                // TODO: Re-implement filter summary method.
                tooltip = "FMP.ThresholdCountTooltip".Translate( CurCount, Count );
            }

            Utilities.Label( thresholdLabelRect, label, tooltip );

            // add a little icon to mark interactivity
            var searchIconRect = new Rect( thresholdLabelRect.xMax - Utilities.Margin - entryHeight, cur.y, entryHeight,
                                           entryHeight );
            if ( searchIconRect.height > Utilities.SmallIconSize )
            {
                // center it.
                searchIconRect = searchIconRect.ContractedBy( ( searchIconRect.height - Utilities.SmallIconSize ) / 2 );
            }
            GUI.DrawTexture( searchIconRect, Resources.Search );

            cur.y += entryHeight;
            if ( Widgets.ButtonInvisible( thresholdLabelRect ) )
            {
                Find.WindowStack.Add( DetailsWindow );
            }

            var thresholdRect = new Rect( cur.x, cur.y, width, Utilities.SliderHeight );
            if ( alt )
            {
                Widgets.DrawAltRect( thresholdRect );
            }
            Count = (int)GUI.HorizontalSlider( thresholdRect, Count, 0, MaxUpperThreshold );
            cur.y += Utilities.SliderHeight;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look( ref Count, "Count" );
            Scribe_Values.Look( ref MaxUpperThreshold, "MaxUpperThreshold" );
            Scribe_Values.Look( ref Op, "Operator" );
            Scribe_Deep.Look( ref ThresholdFilter, "ThresholdFilter" );

            // stockpile needs special treatment - is not referenceable.
            if ( Scribe.mode == LoadSaveMode.Saving )
            {
                _stockpile_scribe = stockpile?.ToString() ?? "null";
            }
            Scribe_Values.Look( ref _stockpile_scribe, "Stockpile", "null" );
            if ( Scribe.mode == LoadSaveMode.PostLoadInit )
            {
                stockpile =
                    manager.map.zoneManager.AllZones.FirstOrDefault(
                                                                    z =>
                                                                    z is Zone_Stockpile && z.label == _stockpile_scribe )
                    as Zone_Stockpile;
            }
        }

        public override string ToString()
        {
            // TODO: Implement Trigger_Threshold.ToString()
            return "Trigger_Threshold.ToString() not implemented";
        }
    }
}
