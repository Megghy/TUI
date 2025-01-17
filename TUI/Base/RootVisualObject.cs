﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TerrariaUI.Hooks;
using TerrariaUI.Hooks.Args;

namespace TerrariaUI.Base
{
    /// <summary>
    /// Basic root of user interface tree widget.
    /// </summary>
    public class RootVisualObject : VisualContainer
    {
        #region Data

        /// <summary>
        /// Last Apply counter drawn to a player.
        /// </summary>
        public ConcurrentDictionary<int, ulong> PlayerApplyCounter { get; } =
            new ConcurrentDictionary<int, ulong>();
        /// <summary>
        /// List of players that are currently close enough to this interface.
        /// </summary>
        public HashSet<int> Players { get; } = new HashSet<int>();
        /// <summary>
        /// List of players who can see this interface if personal, null otherwise.
        /// </summary>
        public HashSet<int> Observers { get; } = new HashSet<int>();

        /// <summary>
        /// Unique interface root identifier.
        /// </summary>
        public override string Name { get; set; }
        public override dynamic Provider => _Provider;
        /// <summary>
        /// Counter of Apply() calls. Interface won't redraw to user if ApplyCounter hasn't changed.
        /// </summary>
        public ulong DrawState { get; protected internal set; }
        /// <summary>
        /// Tile provider object, default value - null (interface would
        /// be drawn on the Main.tile, tiles would be irrevocably modified).
        /// <para></para>
        /// TileProvider from [FakeProvider](https://github.com/AnzhelikaO/FakeProvider)
        /// can be passed as a value so that interface would be drawn above the Main.tile.
        /// </summary>
        protected dynamic _Provider { get; set; }
        protected VisualContainer PopUpBackground { get; set; }
        protected Dictionary<VisualObject, Action<VisualObject>> PopUpCancelCallbacks { get; set; } =
            new Dictionary<VisualObject, Action<VisualObject>>();

        /// <summary>
        /// Personal interface can be seen by only specified players.
        /// </summary>
        public bool Personal => Observers != null;
        /// <summary>
        /// 0 for Root with MainTileProvider, 1 for root with any fake provider.
        /// </summary>
        public override int Layer => UsesDefaultMainProvider ? 0 : 1;
        public override bool Orderable => true;
        protected override int MinWidth => Math.Max(base.MinWidth, 1);
        protected override int MinHeight => Math.Max(base.MinHeight, 1);

        #endregion

        #region Constructor

        /// <summary>
        /// Basic root of user interface tree widget.
        /// </summary>
        /// <param name="name">Unique interface identifier</param>
        /// be drawn on the Main.tile, tiles would be irrevocably modified).
        /// <para></para>
        /// FakeTileRectangle from [FakeManager](https://github.com/AnzhelikaO/FakeManager)
        /// can be passed as a value so that interface would be drawn above the Main.tile.</param>
        public RootVisualObject(string name, int x, int y, int width, int height,
                UIConfiguration configuration = null, ContainerStyle style = null, object provider = null, HashSet<int> observers = null)
            : base(x, y, width, height, configuration ?? new UIConfiguration() { UseBegin=true, UseMoving=true, UseEnd=true }, style)
        {
            Name = name;
            Observers = observers; // we have to initialize this field before trying to create provider
            _Provider = provider;
        }

        #endregion
        #region Tile

        public override dynamic Tile(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                throw new ArgumentOutOfRangeException($"{FullName}: Invalid tile x or y.");
            return Provider?[ProviderX + x, ProviderY + y];
        }

        #endregion
        #region SetXYWH

        public override VisualObject SetXYWH(int x, int y, int width, int height)
        {
            int oldX = X, oldY = Y, oldWidth = Width, oldHeight = Height;
            base.SetXYWH(x, y, width, height);
            if (oldX != X || oldY != Y || oldWidth != Width || oldHeight != Height)
            {
                if (UsesDefaultMainProvider)
                    Clear();

                // MainTileProvider ignores this SetXYWH
                Provider?.SetXYWH(x, y, width, height, false);
                TUI.Hooks.SetXYWH.Invoke(new SetXYWHArgs(this, x, y, width, height));
            }
            return this;
        }

        #endregion
        #region Enable

        public override VisualObject Enable(bool draw = true)
        {
            if (!Enabled)
            {
                Provider.Enable(false);
                base.Enable(draw);
                TUI.Hooks.Enabled.Invoke(new EnabledArgs(this, true));
            }
            return this;
        }

        #endregion
        #region Disable

        public override VisualObject Disable(bool draw = true)
        {
            if (Enabled)
            {
                if (UsesDefaultMainProvider && draw)
                    Clear();

                Provider.Disable(false);
                base.Disable(draw);
                TUI.Hooks.Enabled.Invoke(new EnabledArgs(this, false));
            }
            return this;
        }

        #endregion
        #region DrawReposition

        protected override void DrawReposition(int oldX, int oldY, int oldWidth, int oldHeight)
        {
            RequestDrawChanges();
            Draw(oldX - X, oldY - Y, oldWidth, oldHeight, OutdatedPlayers(toEveryone: true));

            Update();
            if (UsesDefaultMainProvider || oldWidth != Width || oldHeight != Height)
                Apply();
            else
                RequestDrawChanges();
            Draw(targetPlayers: OutdatedPlayers(toEveryone: true));
        }

        #endregion
        #region DrawEnable

        protected override void DrawEnable()
        {
            Update().Apply().Draw();
        }

        #endregion
        #region DrawDisable

        protected override void DrawDisable()
        {
            RequestDrawChanges().Draw(targetPlayers: OutdatedPlayers(toEveryone: true));
        }

        #endregion

        #region LoadThisNative

        protected override void LoadThisNative()
        {
            base.LoadThisNative();

            if (Provider == null)
            {
                var args = new CreateProviderArgs(this);
                TUI.Hooks.CreateProvider.Invoke(args); // fake or personal fake is root.Observers is not null
                _Provider = args.Provider ?? new MainTileProvider();
            }
            if (Personal && Provider is MainTileProvider)
                throw new NotSupportedException("Personal UI is not supported with MainTileProvider: "+ FullName);
            // MainTileProvider ignores this SetXYWH
            Provider.SetXYWH(X, Y, Width, Height, false);

            TUI.Hooks.LoadRoot.Invoke(new LoadRootArgs(this));
        }

        #endregion
        #region DisposeThisNative

        protected override void DisposeThisNative()
        {
            base.DisposeThisNative();
            if (!(Provider is MainTileProvider))
                TUI.Hooks.RemoveProvider.Invoke(new RemoveProviderArgs(Provider));
        }

        #endregion
        #region UpdateThisNative

        protected override void UpdateThisNative()
        {
            // MainTileProvider acquires Main.tile field
            Provider?.Update();
            base.UpdateThisNative();
        }

        #endregion
        #region ApplyThisNative

        protected override void ApplyThisNative()
        {
#if DEBUG
            Stopwatch sw = Stopwatch.StartNew();
#endif
            base.ApplyThisNative();
#if DEBUG
            TUI.Log($"Apply ({Name}): {sw.ElapsedMilliseconds}");
            sw.Stop();
#endif
        }

        #endregion
        #region PrePostCallbacks

        #region Pulse

        /// <summary> Called just before a widget in this interface tree is pulsed. Self-inclusive. </summary>
        /// <param name="widget"> The widget that will be pulsed. </param>
        /// <param name="type"> The type of signal that will be pulsed. </param>
        public virtual void PrePulseObject(VisualObject widget, PulseType type) { }

        /// <summary> Called just after a widget in this interface tree was pulsed. Self-inclusive. </summary>
        /// <param name="widget"> The widget that was pulsed. </param>
        /// <param name="type"> The type of signal that was pulsed. </param>
        public virtual void PostPulseObject(VisualObject widget, PulseType type) { }

        #endregion
        #region Update

        /// <summary> Called just before a widget in this interface tree is updated. Self-inclusive. </summary>
        /// <param name="widget"> The widget that will be updated. </param>
        public virtual void PreUpdateObject(VisualObject widget) { }

        /// <summary> Called just after a widget in this interface tree was updated. Self-inclusive. </summary>
        /// <param name="widget"> The widget that was updated. </param>
        public virtual void PostUpdateObject(VisualObject widget) { }

        #endregion
        #region Apply

        /// <summary> Called just before a widget in this interface tree is applied. Self-inclusive. </summary>
        /// <param name="widget"> The widget that will be applied. </param>
        public virtual void PreApplyObject(VisualObject widget) { }

        /// <summary> Called just after a widget in this interface tree was applied. Self-inclusive. </summary>
        /// <param name="widget"> The widget that was applied. </param>
        public virtual void PostApplyObject(VisualObject widget) { }

        #endregion
        #region Draw

        /// <summary> Called just before a widget in this interface tree is drawn. Self-inclusive. </summary>
        /// <param name="args"> The arguments that will be passed to the draw event. </param>
        public virtual void PreDrawObject(DrawObjectArgs args) { }

        /// <summary> Called just after a widget in this interface tree is drawn. Self-inclusive. </summary>
        /// <param name="args"> The arguments that were passed to the draw event. </param>
        public virtual void PostDrawObject(DrawObjectArgs args) { }

        #endregion

        #endregion
    }
}
