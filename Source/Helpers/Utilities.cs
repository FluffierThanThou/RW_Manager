﻿// Karel Kroeze
// Utilities.cs
// 2016-12-09

using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace FluffyManager
{
    public static class Utilities
    {
        public const float LargeListEntryHeight = 50f;
        public const float Margin = 6f;
        public const float SliderHeight = 20f;
        public static float BottomButtonHeight = 50f;
        public static Vector2 ButtonSize = new Vector2( 200f, 40f );

        public static Dictionary<MapStockpileFilter, FilterCountCache> CountCache =
            new Dictionary<MapStockpileFilter, FilterCountCache>();

        public static Dictionary<string, int> updateIntervalOptions = new Dictionary<string, int>();
        public static float LargeIconSize = 32f;
        public static float ListEntryHeight = 30f;
        public static float MediumIconSize = 24f;
        public static float SmallIconSize = 16f;
        public static float TitleHeight = 50f;
        public static float TopAreaHeight = 30f;
        public static WorkTypeDef WorkTypeDefOf_Managing = DefDatabase<WorkTypeDef>.GetNamed( "Managing" );

        static Utilities()
        {
            updateIntervalOptions.Add( "FM.Hourly".Translate(), GenDate.TicksPerHour );
            updateIntervalOptions.Add( "FM.MultiHourly".Translate( 2 ), GenDate.TicksPerHour * 2 );
            updateIntervalOptions.Add( "FM.MultiHourly".Translate( 4 ), GenDate.TicksPerHour * 4 );
            updateIntervalOptions.Add( "FM.MultiHourly".Translate( 8 ), GenDate.TicksPerHour * 8 );
            updateIntervalOptions.Add( "FM.Daily".Translate(), GenDate.TicksPerDay );
            updateIntervalOptions.Add( "FM.Monthly".Translate(), GenDate.TicksPerTwelfth );
            updateIntervalOptions.Add( "FM.Yearly".Translate(), GenDate.TicksPerYear );
        }

        public static Rect CenteredIn( this Rect inner, Rect outer, float x = 0f, float y = 0f )
        {
            inner = inner.CenteredOnXIn( outer ).CenteredOnYIn( outer );
            inner.x += x;
            inner.y += y;
            return inner;
        }

        public static bool HasCompOrChildCompOf( this ThingDef def, Type compType )
        {
            for ( var index = 0; index < def.comps.Count; ++index )
            {
                if ( compType.IsAssignableFrom( def.comps[index].compClass ) )
                    return true;
            }

            return false;
        }

        public static IntVec3 GetBaseCenter( this Map map )
        {
            // we need to define a 'base' position to calculate distances.
            // Try to find a managerstation (in all non-debug cases this method will only fire if there is such a station).
            IntVec3 position = IntVec3.Zero;
            Building managerStation =
                map.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>().FirstOrDefault();
            if ( managerStation != null )
            {
                position = managerStation.Position;
            }

            // otherwise, use the average of the home area. Not ideal, but it'll do.
            else
            {
                List<IntVec3> homeCells = map.areaManager.Get<Area_Home>().ActiveCells.ToList();
                for ( var i = 0; i < homeCells.Count; i++ )
                {
                    position += homeCells[i];
                }

                position.x /= homeCells.Count;
                position.y /= homeCells.Count;
                position.z /= homeCells.Count;
            }

            return position;
        }

        //public static string Summary( this ThingFilter filter )
        //{
        //    string label = filter._mainSummary();

        //    if ( filter.allowedHitPointsConfigurable &&
        //         ( filter.AllowedHitPointsPercents.TrueMax != 1 ||
        //           filter.AllowedHitPointsPercents.TrueMin != 0 ) )
        //    {
        //        label += " (" + "FM.FilterDescriptionHitPoints".Translate( filter.AllowedHitPointsPercents.TrueMin,
        //                                                                   filter.AllowedHitPointsPercents.TrueMax )
        //                 + ")";
        //    }

        //    if ( filter.allowedQualitiesConfigurable &&
        //         ( filter.AllowedQualityLevels.min != QualityCategory.Awful ||
        //           filter.AllowedQualityLevels.max != QualityCategory.Legendary ) )
        //    {
        //        label += " (" +
        //                 "FM.FilterDescriptionQuality".Translate( filter.AllowedQualityLevels.min.ToString(),
        //                                                          filter.AllowedQualityLevels.max.ToString() ) +
        //                 ")";
        //    }

        //    return label;
        //}

        //private static string _mainSummary( this ThingFilter filter )
        //{
        //    // special cases: 1 category (almost) fully allowed (only works if filter is actually limited to that category?)
        //    if ( filter.categories?.Count == 1 &&
        //         filter.exceptedThingDefs.Count < 2 )
        //    {
        //        string label = filter.categories.First();
        //        if ( filter.exceptedThingDefs.Count == 1 )
        //        {
        //            label += ", " + "FM.FilterDescriptionException".Translate() +
        //                     filter.exceptedThingDefs.First().LabelCap;
        //        }
        //        return label;
        //    }

        //    // special cases: 1-3 thingdefs allowed;
        //    if ( filter.AllowedThingDefs?.Count() < 4 )
        //    {
        //        return string.Join( ", ", filter.AllowedThingDefs.Select( td => td.LabelCap ).ToArray() );
        //    }

        //    // TODO: main routine
        //    // NOTE: when I can be arsed to care enough.
        //    // get list of allowed thingdefs.
        //    // get list of categories.
        //    // from the top category downwards, enqueue all categories.
        //    // for each category, check if all thingdefs are allowed, if so, add category - delete thingdefs.
        //    // if not, if only one is not allowed, add category with exception clause - delete thingdefs
        //    // else, add thingdefs

        //    TreeNode_ThingCategory root = filter.DisplayRootCategory;
        //    return string.Empty;
        //}

        public static void Label( Rect rect, string label, string tooltip = null,
                                  TextAnchor anchor = TextAnchor.MiddleLeft, float lrMargin = Margin,
                                  float tbMargin = 0f, GameFont font = GameFont.Small, Color? color = null )
        {
            // apply margins
            var labelRect = new Rect( rect.xMin + lrMargin, rect.yMin + tbMargin, rect.width - 2 * lrMargin,
                                      rect.height - 2 * tbMargin );

            // draw label with anchor - reset anchor
            Text.Anchor = anchor;
            Text.Font = font;
            GUI.color = color ?? Color.white;
            Widgets.Label( labelRect, label );
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // if set, draw tooltip
            if ( tooltip != null )
            {
                TooltipHandler.TipRegion( rect, tooltip );
            }
        }

        public static void Label( ref Vector2 cur, float width, float height, string label, string tooltip = null,
                                  TextAnchor anchor = TextAnchor.MiddleLeft, float lrMargin = Margin,
                                  float tbMargin = 0f, bool alt = false,
                                  GameFont font = GameFont.Small, Color? color = null )
        {
            var rect = new Rect( cur.x, cur.y, width, height );
            if ( alt )
            {
                Widgets.DrawAltRect( rect );
            }
            Label( rect, label, tooltip, anchor, lrMargin, tbMargin, font, color );
            cur.y += height;
        }

        private static bool TryGetCached( MapStockpileFilter mapStockpileFilter, out int count )
        {
            if ( CountCache.ContainsKey( mapStockpileFilter ) )
            {
                FilterCountCache filterCountCache = CountCache[mapStockpileFilter];
                if ( Find.TickManager.TicksGame - filterCountCache.TimeSet < 250 && // less than 250 ticks ago
                     Find.TickManager.TicksGame > filterCountCache.TimeSet )
                // cache is not from future (switching games without restarting could cause this).
                {
                    count = filterCountCache.Cache;
                    return true;
                }
            }
#if DEBUG_COUNTS
            Log.Message("not cached");
#endif
            count = 0;
            return false;
        }

        public static string TimeString( this int ticks )
        {
            int days = ticks / GenDate.TicksPerDay,
                hours = ticks % GenDate.TicksPerDay / GenDate.TicksPerHour;

            string s = string.Empty;

            if ( days > 0 )
            {
                s += days + "LetterDay".Translate() + " ";
            }
            s += hours + "LetterHour".Translate();

            return s;
        }

        public static int CountProducts( this Map map, ThingFilter filter, Zone_Stockpile stockpile = null )
        {
            var count = 0;

            // copout if filter is null
            if ( filter == null )
            {
                return count;
            }

            var key = new MapStockpileFilter( map, filter, stockpile );
            if ( TryGetCached( key, out count ) )
            {
                return count;
            }

#if DEBUG_COUNTS
            Log.Message("Obtaining new count");
#endif

            foreach ( ThingDef td in filter.AllowedThingDefs )
            {
                // if it counts as a resource and we're not limited to a single stockpile, use the ingame counter (e.g. only steel in stockpiles.)
                if ( td.CountAsResource &&
                     stockpile == null )
                {
#if DEBUG_COUNTS
                        Log.Message(td.LabelCap + ", " + Find.ResourceCounter.GetCount(td));
#endif

                    // we don't need to bother with quality / hitpoints as these are non-existant/irrelevant for resources.
                    count += map.resourceCounter.GetCount( td );
                }
                else
                {
                    // otherwise, go look for stuff that matches our filters.
                    List<Thing> thingList = map.listerThings.ThingsOfDef( td );

                    // if filtered by stockpile, filter the thinglist accordingly.
                    if ( stockpile != null )
                    {
                        SlotGroup areaSlotGroup = stockpile.slotGroup;
                        thingList = thingList.Where( t => t.Position.GetSlotGroup( map ) == areaSlotGroup ).ToList();
                    }
                    foreach ( Thing t in thingList )
                    {
                        QualityCategory quality;
                        if ( t.TryGetQuality( out quality ) )
                        {
                            if ( !filter.AllowedQualityLevels.Includes( quality ) )
                            {
                                continue;
                            }
                        }

                        if ( filter.AllowedHitPointsPercents.IncludesEpsilon( t.HitPoints ) )
                        {
                            continue;
                        }

#if DEBUG_COUNTS
                            Log.Message(t.LabelCap + ": " + CountProducts(t));
#endif

                        count += t.stackCount;
                    }
                }

                // update cache if exists.
                if ( CountCache.ContainsKey( key ) )
                {
                    CountCache[key].Cache = count;
                    CountCache[key].TimeSet = Find.TickManager.TicksGame;
                }
                else
                {
                    CountCache.Add( key, new FilterCountCache( count ) );
                }
            }

            return count;
        }

        public static bool IsInt( this string text )
        {
            int num;
            return int.TryParse( text, out num );
        }

        public static void DrawStatusForListEntry<T>( this T job, Rect rect, Trigger trigger ) where T : ManagerJob
        {
            if ( job.Completed ||
                 job.Suspended )
            {
                // put a stamp on it
                var stampRect = new Rect( 0f, 0f, MediumIconSize, MediumIconSize );

                // center stamp in available space
                stampRect = stampRect.CenteredOnXIn( rect ).CenteredOnYIn( rect );

                // draw it.
                if ( job.Completed )
                {
                    GUI.DrawTexture( stampRect, Resources.StampCompleted );
                    TooltipHandler.TipRegion( stampRect, "FM.JobCompletedTooltip".Translate() );
                    return;
                }

                if ( job.Suspended )
                {
                    // allow activating the job from here.
                    if ( !Mouse.IsOver( stampRect ) )
                    {
                        GUI.DrawTexture( stampRect, Resources.StampSuspended );
                    }
                    else
                    {
                        if ( Widgets.ButtonImage( stampRect, Resources.StampStart ) )
                        {
                            job.Suspended = false;
                        }
                        TooltipHandler.TipRegion( stampRect, "FM.JobSuspendedTooltip".Translate() );
                    }
                    return;
                }
            }

            if ( trigger == null )
            {
                Log.Message( "Trigger NULL" );
                return;
            }

            // set up rects
            Rect progressRect = new Rect( Margin, 0f, ManagerJob.ProgressRectWidth, rect.height ),
                 lastUpdateRect = new Rect( progressRect.xMax + Margin, 0f, ManagerJob.LastUpdateRectWidth, rect.height );

            // set drawing canvas
            GUI.BeginGroup( rect );

            // draw progress bar
            trigger.DrawProgressBar( progressRect, true );

            // draw time since last action
            Text.Anchor = TextAnchor.MiddleCenter;
            var lastUpdate = (Find.TickManager.TicksGame - job.LastAction);

            // set color by how timely we've been
            if ( lastUpdate < job.ActionInterval )
                GUI.color = Color.green;
            if ( lastUpdate > job.ActionInterval )
                GUI.color = Color.white;
            if ( lastUpdate > job.ActionInterval * 2 )
                GUI.color = Color.red;
            
            Widgets.Label( lastUpdateRect, lastUpdate.TimeString() );
            GUI.color = Color.white;

            // set tooltips
            TooltipHandler.TipRegion( progressRect, trigger.StatusTooltip );
            TooltipHandler.TipRegion( lastUpdateRect,
                                      "FM.LastUpdateTooltip".Translate( 
                                          lastUpdate.TimeString(),
                                          job.ActionInterval.TimeString() ) );

            Widgets.DrawHighlightIfMouseover( lastUpdateRect );
            if ( Widgets.ButtonInvisible( lastUpdateRect ) )
            {
                var options = new List<FloatMenuOption>();
                foreach ( KeyValuePair<string, int> period in updateIntervalOptions )
                {
                    var label = period.Key;
                    var time = period.Value;
                    options.Add( new FloatMenuOption( label, delegate
                                                                      {
                                                                          job.ActionInterval = time;
                                                                      }
                                                    ) );
                }

                Find.WindowStack.Add( new FloatMenu( options ) );
            }

            GUI.EndGroup();
        }

        public static void DrawToggle( Rect rect, string label, ref bool checkOn, float size = 24f,
                                       float margin = Margin, GameFont font = GameFont.Small )
        {
            // set up rects
            Rect labelRect = rect;
            var checkRect = new Rect( rect.xMax - size - margin * 2, 0f, size, size );

            // finetune rects
            checkRect = checkRect.CenteredOnYIn( labelRect );

            // draw label
            Label( rect, label, null, TextAnchor.MiddleLeft, margin, font: font );

            // draw check
            if ( checkOn )
            {
                GUI.DrawTexture( checkRect, Widgets.CheckboxOnTex );
            }
            else
            {
                GUI.DrawTexture( checkRect, Widgets.CheckboxOffTex );
            }

            // interactivity
            Widgets.DrawHighlightIfMouseover( rect );
            if ( Widgets.ButtonInvisible( rect ) )
            {
                checkOn = !checkOn;
            }
        }

        public static void DrawToggle( Rect rect, string label, bool checkOn, Action on, Action off, float size = 24f,
                                       float margin = Margin )
        {
            // set up rects
            Rect labelRect = rect;
            var checkRect = new Rect( rect.xMax - size - margin * 2, 0f, size, size );

            // finetune rects
            checkRect = checkRect.CenteredOnYIn( labelRect );

            // draw label
            Label( rect, label, null, TextAnchor.MiddleLeft, margin );

            // draw check
            if ( checkOn )
            {
                GUI.DrawTexture( checkRect, Widgets.CheckboxOnTex );
            }
            else
            {
                GUI.DrawTexture( checkRect, Widgets.CheckboxOffTex );
            }

            // interactivity
            Widgets.DrawHighlightIfMouseover( rect );
            if ( Widgets.ButtonInvisible( rect ) )
            {
                if ( checkOn )
                {
                    off();
                }
                else
                {
                    on();
                }
            }
        }

        public static void DrawToggle( Rect rect, string label, bool checkOn, Action toggle, float size = 24f,
                                       float margin = Margin )
        {
            DrawToggle( rect, label, checkOn, toggle, toggle, size );
        }

        public static bool TryGetPrivateField( Type type, object instance, string fieldName, out object value,
                                               BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance )
        {
            FieldInfo field = type.GetField( fieldName, flags );
            value = field?.GetValue( instance );
            return value != null;
        }

        public static bool TrySetPrivateField( Type type, object instance, string fieldName, object value,
                                               BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance )
        {
            // get field info
            FieldInfo field = type.GetField( fieldName, flags );

            // failed?
            if ( field == null )
            {
                return false;
            }

            // try setting it.
            field.SetValue( instance, value );

            // test by fetching the field again. (this is highly, stupidly inefficient, but ok).
            object test;
            if ( !TryGetPrivateField( type, instance, fieldName, out test, flags ) )
            {
                return false;
            }

            return test == value;
        }

        public static object GetPrivatePropertyValue( this object src, string propName,
                                                      BindingFlags flags =
                                                          BindingFlags.Instance | BindingFlags.NonPublic )
        {
            return src.GetType().GetProperty( propName, flags ).GetValue( src, null );
        }

        public static void LabelOutline( Rect icon, string label, string tooltip, TextAnchor anchor, float lrMargin,
                                         float tbMargin, GameFont font, Color textColour, Color outlineColour )
        {
            // horribly inefficient way of getting an outline to show - draw 4 background coloured labels with a 1px offset, then draw the foreground on top.
            int[] offsets = { -1, 0, 1 };

            foreach ( int xOffset in offsets )
                foreach ( int yOffset in offsets )
                {
                    Rect offsetIcon = icon;
                    offsetIcon.x += xOffset;
                    offsetIcon.y += yOffset;
                    Label( offsetIcon, label, null, anchor, lrMargin, tbMargin, font, outlineColour );
                }

            Label( icon, label, tooltip, anchor, lrMargin, tbMargin, font, textColour );
        }

        public static void Scribe_IntArray( ref List<int> values, string label )
        {
            string text = null;
            if ( Scribe.mode == LoadSaveMode.Saving )
            {
                text = String.Join( ":", values.ConvertAll( i => i.ToString() ).ToArray() );
            }
            Scribe_Values.Look( ref text, label );
            if ( Scribe.mode == LoadSaveMode.LoadingVars )
            {
                values = text.Split( ":".ToCharArray() ).ToList().ConvertAll( int.Parse );
            }
        }

        public struct MapStockpileFilter
        {
            private ThingFilter filter;
            private Zone_Stockpile stockpile;
            private Map map;

            public MapStockpileFilter( Map map, ThingFilter filter, Zone_Stockpile stockpile )
            {
                this.map = map;
                this.filter = filter;
                this.stockpile = stockpile;
            }
        }

        public class CachedValue<T>
        {
            private T _cached;
            private T _default;
            public int timeSet;
            public int updateInterval;

            public CachedValue( T value = default( T ), int updateInterval = 250 )
            {
                this.updateInterval = updateInterval;
                _cached = _default = value;
                timeSet = Find.TickManager.TicksGame;
            }

            public bool TryGetValue( out T value )
            {
                if ( Find.TickManager.TicksGame - timeSet <= updateInterval )
                {
                    value = _cached;
                    return true;
                }

                value = _default;
                return false;
            }

            public void Update( T value )
            {
                _cached = value;
                timeSet = Find.TickManager.TicksGame;
            }
        }

        // count cache for multiple products
        public class FilterCountCache
        {
            public int Cache;
            public int TimeSet;

            public FilterCountCache( int count )
            {
                Cache = count;
                TimeSet = Find.TickManager.TicksGame;
            }
        }
    }
}
