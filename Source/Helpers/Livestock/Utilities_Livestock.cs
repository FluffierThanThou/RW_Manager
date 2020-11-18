// Utilities_Livestock.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyManager
{
    public enum AgeAndSex
    {
        AdultFemale    = 0,
        AdultMale      = 1,
        JuvenileFemale = 2,
        JuvenileMale   = 3
    }

    [Flags]
    public enum MasterMode
    {
        Default    = 0,
        Hunters    = 1,
        Trainers   = 2,
        Melee      = 4,
        Ranged     = 8,
        Violent    = 16,
        NonViolent = 32,
        All        = Hunters | Trainers | Melee | Ranged | Violent | NonViolent,
        Specific   = 64
    }

    public static class Utilities_Livestock
    {
        public static AgeAndSex[] AgeSexArray = (AgeAndSex[]) Enum.GetValues( typeof( AgeAndSex ) );

        private static readonly Utilities.CachedValues<Pair<PawnKindDef, Map>, IEnumerable<Pawn>> AllCache =
            new Utilities.CachedValues<Pair<PawnKindDef, Map>, IEnumerable<Pawn>>( 5 );

        private static readonly Utilities.CachedValues<Triplet<PawnKindDef, Map, AgeAndSex>, IEnumerable<Pawn>>
            AllSexedCache = new Utilities.CachedValues<Triplet<PawnKindDef, Map, AgeAndSex>, IEnumerable<Pawn>>( 5 );

        private static readonly Dictionary<Pawn, Utilities.CachedValue<IEnumerable<Pawn>>> FollowerCache =
            new Dictionary<Pawn, Utilities.CachedValue<IEnumerable<Pawn>>>();

        private static readonly
            Dictionary<Triplet<PawnKindDef, Map, MasterMode>, Utilities.CachedValue<IEnumerable<Pawn>>> MasterCache =
                new Dictionary<Triplet<PawnKindDef, Map, MasterMode>, Utilities.CachedValue<IEnumerable<Pawn>>>();

        private static readonly Dictionary<Pawn, Utilities.CachedValue<bool>> MilkablePawn =
            new Dictionary<Pawn, Utilities.CachedValue<bool>>();

        private static readonly Dictionary<PawnKindDef, Utilities.CachedValue<bool>> MilkablePawnkind =
            new Dictionary<PawnKindDef, Utilities.CachedValue<bool>>();

        private static readonly Dictionary<Pawn, Utilities.CachedValue<bool>> ShearablePawn =
            new Dictionary<Pawn, Utilities.CachedValue<bool>>();

        private static readonly Dictionary<PawnKindDef, Utilities.CachedValue<bool>> ShearablePawnkind =
            new Dictionary<PawnKindDef, Utilities.CachedValue<bool>>();

        private static readonly Utilities.CachedValues<Pair<PawnKindDef, Map>, IEnumerable<Pawn>> TameCache =
            new Utilities.CachedValues<Pair<PawnKindDef, Map>, IEnumerable<Pawn>>( 5 );

        private static readonly Utilities.CachedValues<Triplet<PawnKindDef, Map, AgeAndSex>, IEnumerable<Pawn>>
            TameSexedCache = new Utilities.CachedValues<Triplet<PawnKindDef, Map, AgeAndSex>, IEnumerable<Pawn>>( 5 );

        private static readonly Utilities.CachedValues<Pair<PawnKindDef, Map>, IEnumerable<Pawn>> WildCache =
            new Utilities.CachedValues<Pair<PawnKindDef, Map>, IEnumerable<Pawn>>( 5 );

        private static readonly Utilities.CachedValues<Triplet<PawnKindDef, Map, AgeAndSex>, IEnumerable<Pawn>>
            WildSexedCache = new Utilities.CachedValues<Triplet<PawnKindDef, Map, AgeAndSex>, IEnumerable<Pawn>>( 5 );

        public static bool BondedWithColonist( this Pawn pawn )
        {
            return pawn?.relations?.GetFirstDirectRelationPawn( PawnRelationDefOf.Bond, p => p.IsColonist ) != null;
        }

        public static IEnumerable<Pawn> GetAll( this PawnKindDef pawnKind, Map map )
        {
            // check if we have a cached version
            var key = new Pair<PawnKindDef, Map>( pawnKind, map );
            if ( AllCache.TryGetValue( key, out var pawns ) )
                return pawns;

            // if not, set up a cache
            Func<IEnumerable<Pawn>> getter = () => map.mapPawns.AllPawns
                                                      .Where( p => p.RaceProps.Animal    // is animal
                                                                && !p.Dead               // is alive
                                                                && p.kindDef == pawnKind // is our managed pawnkind
                                                                && !( p.Faction == Faction.OfPlayer &&
                                                                      p.HasExtraHomeFaction()
                                                                       ) ); // was not borrowed to us

            AllCache.Add( key, getter );
            return getter();
        }


        public static IEnumerable<Pawn> GetAll( this PawnKindDef pawnKind, Map map, AgeAndSex ageSex )
        {
            var key = new Triplet<PawnKindDef, Map, AgeAndSex>( pawnKind, map, ageSex );
            if ( AllSexedCache.TryGetValue( key, out var pawns ) )
                return pawns;

            Func<IEnumerable<Pawn>>
                getter = () =>
                    pawnKind.GetAll( map ).Where( p => PawnIsOfAgeSex( p, ageSex ) ); // is of age and sex we want
            AllSexedCache.Add( key, getter );
            return getter();
        }

        public static List<Pawn> GetFollowers( this Pawn pawn )
        {
            // check if we have a cached version
            IEnumerable<Pawn> cached;

            // does it exist at all?
            var cacheExists = FollowerCache.ContainsKey( pawn );

            // is it up to date?
            if ( cacheExists && FollowerCache[pawn].TryGetValue( out cached ) && cached != null )
                return cached.ToList();

            // if not, get a new list.
            cached = pawn.MapHeld.mapPawns.PawnsInFaction( pawn.Faction )
                         .Where( p => !p.Dead            &&
                                      p.RaceProps.Animal &&
                                      p.playerSettings.Master == pawn
                          );

            // update if key exists
            if ( cacheExists )
                FollowerCache[pawn].Update( cached );

            // else add it
            else
                // severely limit cache to only apply for one cycle (one job)
                FollowerCache.Add( pawn, new Utilities.CachedValue<IEnumerable<Pawn>>( cached, 2 ) );
            return cached.ToList();
        }

        public static List<Pawn> GetFollowers( this Pawn pawn, PawnKindDef pawnKind )
        {
            return GetFollowers( pawn ).Where( f => f.kindDef == pawnKind ).ToList();
        }

        public static MasterMode GetMasterMode( this Pawn pawn )
        {
            var mode = MasterMode.Default;

            if ( pawn.workSettings.WorkIsActive( WorkTypeDefOf.Hunting ) )
                mode = mode | MasterMode.Hunters;

            if ( pawn.workSettings.WorkIsActive( WorkTypeDefOf.Handling ) )
                mode = mode | MasterMode.Trainers;

            if ( pawn.equipment.Primary?.def.IsMeleeWeapon ?? true ) // no weapon = melee 
                mode = mode | MasterMode.Melee;

            if ( pawn.equipment.Primary?.def.IsRangedWeapon ?? false )
                mode = mode | MasterMode.Ranged;

            if ( !pawn.WorkTagIsDisabled( WorkTags.Violent ) )
                mode = mode | MasterMode.Violent;

            else
                mode = mode | MasterMode.NonViolent;

            return mode;
        }

        public static List<Pawn> GetMasterOptions( this PawnKindDef pawnkind, Map map, MasterMode mode )
        {
            // check if we have a cached version
            IEnumerable<Pawn> cached;

            // does it exist at all?
            var key         = new Triplet<PawnKindDef, Map, MasterMode>( pawnkind, map, mode );
            var cacheExists = MasterCache.ContainsKey( key );

            // is it up to date?
            if ( cacheExists                                &&
                 MasterCache[key].TryGetValue( out cached ) && cached != null )
                return cached.ToList();

            // if not, get a new list.
            cached = map.mapPawns.FreeColonistsSpawned
                        .Where( p => !p.Dead &&

                                     // matches mode
                                     ( p.GetMasterMode() & mode ) != MasterMode.Default
                         );

            // update if key exists
            if ( cacheExists )
                MasterCache[key].Update( cached );

            // else add it
            else
                // severely limit cache to only apply for one cycle (one job)
                MasterCache.Add( key, new Utilities.CachedValue<IEnumerable<Pawn>>( cached, 2 ) );
            return cached.ToList();
        }

        public static IEnumerable<Pawn> GetTame( this PawnKindDef pawnKind, Map map )
        {
            var key = new Pair<PawnKindDef, Map>( pawnKind, map );
            if ( TameCache.TryGetValue( key, out var pawns ) )
                return pawns;

            Func<IEnumerable<Pawn>> getter = () => pawnKind.GetAll( map ).Where( p => p.Faction == Faction.OfPlayer );
            TameCache.Add( key, getter );
            return getter();
        }

        public static IEnumerable<Pawn> GetTame( this PawnKindDef pawnKind, Map map, AgeAndSex ageSex )
        {
#if DEBUG_LIFESTOCK_COUNTS
            List<Pawn> tame = GetAll( ageSex ).Where( p => p.Faction == Faction.OfPlayer ).ToList();
            Log.Message( "Tamecount " + ageSex + ": " + tame.Count );
            return tame;
#else
            var key = new Triplet<PawnKindDef, Map, AgeAndSex>( pawnKind, map, ageSex );
            if ( TameSexedCache.TryGetValue( key, out var pawns ) )
                return pawns;

            Func<IEnumerable<Pawn>> getter = () =>
                pawnKind.GetAll( map, ageSex ).Where( p => p.Faction == Faction.OfPlayer );
            TameSexedCache.Add( key, getter );
            return getter();
#endif
        }

        public static List<Pawn> GetTrainers( this PawnKindDef pawnkind, Map map, MasterMode mode )
        {
            return pawnkind.GetMasterOptions( map, mode ).Where( p =>
                                                                     // skill high enough to handle (copied from StatWorker_MinimumHandlingSkill)
                                                                     // NOTE: This does NOT apply postprocessing, so scenario and other offsets DO NOT apply.
                                                                     // we can't actually use StatRequests because they're hardcoded for either Things or BuildableDefs.
                                                                     p.skills.GetSkill( SkillDefOf.Animals ).Level >=
                                                                     Mathf.Clamp(
                                                                         GenMath.LerpDouble(
                                                                             0.3f, 1f, 0f, 9f,
                                                                             pawnkind.RaceProps.wildness ), 0f, 20f ) )
                           .ToList();
        }

        public static IEnumerable<Pawn> GetWild( this PawnKindDef pawnKind, Map map )
        {
            var key = new Pair<PawnKindDef, Map>( pawnKind, map );
            if ( WildCache.TryGetValue( key, out var pawns ) )
                return pawns;

            Func<IEnumerable<Pawn>> getter = () => pawnKind.GetAll( map ).Where( p => p.Faction == null );
            WildCache.Add( key, getter );
            return getter();
        }

        public static IEnumerable<Pawn> GetWild( this PawnKindDef pawnKind, Map map, AgeAndSex ageSex )
        {
#if DEBUG_LIFESTOCK_COUNTS
            foreach (Pawn p in GetAll( ageSex )) Log.Message(p.Faction?.GetCallLabel() ?? "NULL" );
            List<Pawn> wild = GetAll( ageSex ).Where( p => p.Faction == null ).ToList();
            Log.Message( "Wildcount " + ageSex + ": " + wild.Count );
            return wild;
#else
            var key = new Triplet<PawnKindDef, Map, AgeAndSex>( pawnKind, map, ageSex );
            if ( WildSexedCache.TryGetValue( key, out var pawns ) )
                return pawns;

            Func<IEnumerable<Pawn>> getter = () => pawnKind.GetAll( map, ageSex ).Where( p => p.Faction == null );
            WildSexedCache.Add( key, getter );
            return getter();
#endif
        }


        public static bool Juvenile( this AgeAndSex ageSex )
        {
            return ageSex == AgeAndSex.JuvenileFemale || ageSex == AgeAndSex.JuvenileMale;
        }

        public static bool Milkable( this PawnKindDef pawnKind )
        {
            if ( pawnKind == null )
                return false;

            var ret = false;
            if ( MilkablePawnkind.ContainsKey( pawnKind ) )
            {
                if ( MilkablePawnkind[pawnKind].TryGetValue( out ret ) ) return ret;

                ret = pawnKind.race.comps.OfType<CompProperties_Milkable>().Any( cp => cp.milkDef != null );
                MilkablePawnkind[pawnKind].Update( ret );
                return ret;
            }

            ret = pawnKind.race.comps.OfType<CompProperties_Milkable>().Any( cp => cp.milkDef != null );
            MilkablePawnkind.Add( pawnKind, new Utilities.CachedValue<bool>( ret, int.MaxValue ) );
            return ret;
        }

        public static bool Milkable( this Pawn pawn )
        {
            var ret = false;
            if ( MilkablePawn.ContainsKey( pawn ) )
            {
                if ( MilkablePawn[pawn].TryGetValue( out ret ) ) return ret;

                ret = pawn._milkable();
                MilkablePawn[pawn].Update( ret );
                return ret;
            }

            ret = pawn._milkable();
            MilkablePawn.Add( pawn, new Utilities.CachedValue<bool>( ret, 5000 ) );
            return ret;
        }

        public static bool PawnIsOfAgeSex( this Pawn p, AgeAndSex ageSex )
        {
            // note; we're making the assumption here that anything with a lifestage
            // index of 2 or greater is adult - so baby, juvenile, adult, ... this
            // works for vanilla and all modded animals that I know off.

            // note; we're treating anything non-male as female. I know, I'm sorry.

            switch ( ageSex )
            {
                case AgeAndSex.AdultFemale:
                    return p.gender != Gender.Male && p.ageTracker.CurLifeStageIndex >= 2;

                case AgeAndSex.AdultMale:
                    return p.gender == Gender.Male && p.ageTracker.CurLifeStageIndex >= 2;

                case AgeAndSex.JuvenileFemale:
                    return p.gender != Gender.Male && p.ageTracker.CurLifeStageIndex < 2;

                case AgeAndSex.JuvenileMale:
                    return p.gender == Gender.Male && p.ageTracker.CurLifeStageIndex < 2;

                default:
                    throw new ArgumentOutOfRangeException( nameof( ageSex ), ageSex, null );
            }
        }

        public static bool Shearable( this PawnKindDef pawnKind )
        {
            if ( pawnKind == null )
                return false;

            var ret = false;
            if ( ShearablePawnkind.ContainsKey( pawnKind ) )
            {
                if ( ShearablePawnkind[pawnKind].TryGetValue( out ret ) ) return ret;

                ret = pawnKind.race.comps.OfType<CompProperties_Shearable>().Any( cp => cp.woolDef != null );
                ShearablePawnkind[pawnKind].Update( ret );
                return ret;
            }

            ret = pawnKind.race.comps.OfType<CompProperties_Shearable>().Any( cp => cp.woolDef != null );
            ShearablePawnkind.Add( pawnKind, new Utilities.CachedValue<bool>( ret, int.MaxValue ) );
            return ret;
        }

        public static bool Shearable( this Pawn pawn )
        {
            var ret = false;
            if ( ShearablePawn.ContainsKey( pawn ) )
            {
                if ( ShearablePawn[pawn].TryGetValue( out ret ) ) return ret;

                ret = pawn._shearable();
                ShearablePawn[pawn].Update( ret );
                return ret;
            }

            ret = pawn._shearable();
            ShearablePawn.Add( pawn, new Utilities.CachedValue<bool>( ret, 5000 ) );
            return ret;
        }

        public static int TicksTillHarvestable( this CompHasGatherableBodyResource comp )
        {
            var interval          = Traverse.Create( comp ).Property( "GatherResourcesIntervalDays" ).GetValue<int>();
            var growthRatePerTick = 1f / ( interval * GenDate.TicksPerDay );

            var pawn = comp.parent as Pawn;
            if ( pawn == null ) throw new ArgumentException( "harvestable should always be on a Pawn" );
            growthRatePerTick *= PawnUtility.BodyResourceGrowthSpeed( (Pawn) comp.parent );

            // Logger.Debug( $"rate: {growthRatePerTick}, interval: {interval}");

            return Mathf.CeilToInt( ( 1 - comp.Fullness ) / growthRatePerTick );
        }

        public static int TicksUntilStarving( this Pawn pawn )
        {
            // this isn't perfect. we assume this is linear when it isn't. the real calculation is more complicated.
            // check out "Need_Food.FoodFallPerTickAssumingCategory(...)" for more details
            return Mathf.CeilToInt(pawn.needs.food.CurLevel / pawn.needs.food.FoodFallPerTick);
        }

        public static bool VisiblyPregnant( this Pawn pawn )
        {
            return pawn?.health.hediffSet.GetHediffs<Hediff_Pregnant>().Any( hp => hp.Visible ) ?? false;
        }

        private static bool _milkable( this Pawn pawn )
        {
            var    comp                = pawn?.TryGetComp<CompMilkable>();
            object active              = false;
            if ( comp != null ) active = comp.GetPrivatePropertyValue( "Active" );
            return (bool) active;
        }

        private static bool _shearable( this Pawn pawn )
        {
            var    comp                = pawn?.TryGetComp<CompShearable>();
            object active              = false;
            if ( comp != null ) active = comp.GetPrivatePropertyValue( "Active" );
            return (bool) active;
        }
    }
}