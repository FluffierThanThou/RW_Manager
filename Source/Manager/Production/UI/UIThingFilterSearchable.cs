﻿using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using RimWorld;
using UnityEngine;
using Verse;

namespace FM
{
    [SuppressMessage( "ReSharper", "InconsistentNaming" )]
    public class ThingFilterUiSearchable
    {
        /*
        private const float ExtraViewHeight = 90f;
*/

        /*
        private const float RangeLabelTab = 10f;
*/

        /*
        private const float RangeLabelHeight = 19f;
*/

        /*
        private const float SliderHeight = 26f;
*/

        /*
        private const float SliderTab = 20f;
*/

        private static float viewHeight;

        public void DoThingFilterConfigWindow( Rect rect, ref Vector2 scrollPosition, ThingFilter filter,
                                               ThingFilter parentFilter = null, int openMask = 1 )
        {
            Widgets.DrawMenuSection( rect );
            Text.Font = GameFont.Tiny;
            float num = rect.width - 2f;
            Rect rect2 = new Rect( rect.x + 1f, rect.y + 1f, num / 2f, 24f );
            if ( Widgets.TextButton( rect2, "ClearAll".Translate() ) )
            {
                filter.SetDisallowAll();
            }
            Rect rect3 = new Rect( rect2.xMax + 1f, rect2.y, num / 2f, 24f );
            if ( Widgets.TextButton( rect3, "AllowAll".Translate() ) )
            {
                filter.SetAllowAll( parentFilter );
            }
            Text.Font = GameFont.Small;
            rect.yMin = rect2.yMax;
            Rect viewRect = new Rect( 0f, 0f, rect.width - 16f, viewHeight );
            Widgets.BeginScrollView( rect, ref scrollPosition, viewRect );
            float num2 = 0f;
            num2 += 2f;
            DrawHitPointsFilterConfig( ref num2, viewRect.width, filter );
            DrawQualityFilterConfig( ref num2, viewRect.width, filter );
            float num3 = num2;
            Rect rect4 = new Rect( 0f, num2, 9999f, 9999f );
            Listing_TreeThingFilter listingTreeThingFilter = new Listing_TreeThingFilter( rect4, filter, parentFilter,
                                                                                          210f, true );
            TreeNode_ThingCategory node = ThingCategoryNodeDatabase.RootNode;
            if ( parentFilter != null )
            {
                if ( parentFilter.DisplayRootCategory == null )
                {
                    parentFilter.RecalculateDisplayRootCategory();
                }
                node = parentFilter.DisplayRootCategory;
            }
            listingTreeThingFilter.DoCategoryChildren( node, 0, openMask, true );
            listingTreeThingFilter.End();
            viewHeight = num3 + listingTreeThingFilter.CurHeight + 90f;
            Log.Message( viewHeight.ToString( CultureInfo.InvariantCulture ) );
            Widgets.EndScrollView();
        }

        private static void DrawHitPointsFilterConfig( ref float y, float width, ThingFilter filter )
        {
            if ( !filter.allowedHitPointsConfigurable )
            {
                return;
            }
            Rect rect = new Rect( 20f, y, width - 20f, 26f );
            FloatRange allowedHitPointsPercents = filter.AllowedHitPointsPercents;
            Widgets.FloatRange( rect, 1, ref allowedHitPointsPercents, 0f, 1f, ToStringStyle.PercentZero, "HitPoints" );
            filter.AllowedHitPointsPercents = allowedHitPointsPercents;
            y += 26f;
            y += 5f;
            Text.Font = GameFont.Small;
        }

        private static void DrawQualityFilterConfig( ref float y, float width, ThingFilter filter )
        {
            if ( !filter.allowedQualitiesConfigurable )
            {
                return;
            }
            Rect rect = new Rect( 20f, y, width - 20f, 26f );
            QualityRange allowedQualityLevels = filter.AllowedQualityLevels;
            Widgets.QualityRange( rect, 2, ref allowedQualityLevels );
            filter.AllowedQualityLevels = allowedQualityLevels;
            y += 26f;
            y += 5f;
            Text.Font = GameFont.Small;
        }
    }
}