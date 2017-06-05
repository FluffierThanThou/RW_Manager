﻿// Karel Kroeze
// Window_TriggerThresholdDetails.cs
// 2016-12-09

using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FluffyManager
{
    public class WindowTriggerThresholdDetails : Window
    {
        #region Fields

        public Vector2 FilterScrollPosition = Vector2.zero;
        public string Input;
        public Trigger_Threshold Trigger;
        private ThingFilterUI filterUI = new ThingFilterUI();

        #endregion Fields



        #region Properties

        public override Vector2 InitialSize => new Vector2( 300f, 500 );

        #endregion Properties



        #region Methods

        public override void DoWindowContents( Rect inRect )
        {
            // set up rects
            var filterRect = new Rect( inRect.ContractedBy( 6f ) );
            filterRect.height -= 2 * ( Utilities.ListEntryHeight + Utilities.Margin );
            var zoneRect = new Rect( filterRect.xMin, filterRect.yMax + Utilities.Margin, filterRect.width,
                                     Utilities.ListEntryHeight );
            var buttonRect = new Rect( filterRect.xMin, zoneRect.yMax + Utilities.Margin,
                                       ( filterRect.width - Utilities.Margin ) / 2f, Utilities.ListEntryHeight );

            // draw thingfilter
            filterUI.DoThingFilterConfigWindow( filterRect, ref FilterScrollPosition, Trigger.ThresholdFilter );

            // draw zone selector
            StockpileGUI.DoStockpileSelectors( zoneRect, ref Trigger.stockpile, Trigger.manager );

            // draw operator button
            if ( Widgets.ButtonText( buttonRect, Trigger.OpString ) )
            {
                var list = new List<FloatMenuOption>
                           {
                               new FloatMenuOption( "Lower than",
                                                    delegate { Trigger.Op = Trigger_Threshold.Ops.LowerThan; } ),
                               new FloatMenuOption( "Equal to", delegate { Trigger.Op = Trigger_Threshold.Ops.Equals; } ),
                               new FloatMenuOption( "Greater than",
                                                    delegate { Trigger.Op = Trigger_Threshold.Ops.HigherThan; } )
                           };
                Find.WindowStack.Add( new FloatMenu( list ) );
            }

            // move operator button canvas for count input
            buttonRect.x = buttonRect.xMax + Utilities.Margin;

            // if current input is invalid color the element red
            Color oldColor = GUI.color;
            if ( !Input.IsInt() )
            {
                GUI.color = new Color( 1f, 0f, 0f );
            }
            else
            {
                Trigger.Count = int.Parse( Input );
                if ( Trigger.Count > Trigger.MaxUpperThreshold )
                {
                    Trigger.MaxUpperThreshold = Trigger.Count;
                }
            }

            // draw the input field
            Input = Widgets.TextField( buttonRect, Input );
            GUI.color = oldColor;

            // close on enter
            if ( Event.current.type == EventType.KeyDown &&
                 Event.current.keyCode == KeyCode.Return )
            {
                Event.current.Use();
                Find.WindowStack.TryRemove( this );
            }
        }

        public override void PreOpen()
        {
            base.PreOpen();
            Input = Trigger.Count.ToString();
        }

        #endregion Methods
    }
}
