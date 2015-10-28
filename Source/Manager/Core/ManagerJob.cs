﻿using System.Text;
using UnityEngine;
using Verse;

namespace FM
{
    internal interface IManagerJob
    {
        bool TryDoJob();
    }

    public abstract class ManagerJob : IManagerJob, IExposable
    {
        public int ActionInterval = 3600; // should be 1 minute.

        public int LastAction;

        public int Priority;

        public Trigger Trigger;

        public virtual bool Active { get; set; }

        public bool ShouldDoNow => Active && ( LastAction + ActionInterval ) < Find.TickManager.TicksGame;

        public virtual void ExposeData()
        {
            Scribe_Values.LookValue( ref ActionInterval, "ActionInterval" );
            Scribe_Values.LookValue( ref LastAction, "LastAction" );
            Scribe_Values.LookValue( ref Priority, "Priority" );
        }

        public virtual bool TryDoJob()
        {
            Log.Warning( "Tried to perform job, but the dispatch was not correctly implemented" );
            return false;
        }

        public abstract void CleanUp();

        public void Touch()
        {
            LastAction = Find.TickManager.TicksGame;
        }

        public override string ToString()
        {
            StringBuilder strout = new StringBuilder();
            strout.AppendLine( Priority + " " + Active + "LastAction" + LastAction + "(interval: " + ActionInterval +
                               ", gameTick: " + Find.TickManager.TicksGame + ")" );
            return strout.ToString();
        }

        public virtual void Tick() {}

        public virtual void DrawListEntry( Rect rect )
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label( rect, ToString() );
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}