﻿using FakeProvider;
using System;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TerrariaUI;
using TerrariaUI.Base;
using TerrariaUI.Hooks;

namespace TUIPlusFakeProvider
{
    [ApiVersion(2, 1)]
    public class TUIPlusFakeProviderPlugin : TerrariaPlugin
    {
        public override string Name => "TUI + FakeProvider";

        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public override string Author => "ASgo & Anzhelika";

        public override string Description => "Automatically creates fake providers for TUI applications";

        public TUIPlusFakeProviderPlugin(Main game)
            : base(game)
        {

        }

        public override void Initialize()
        {
            TUI.Hooks.CreateProvider.Event += OnCreateProvider;
            TUI.Hooks.RemoveProvider.Event += OnRemoveProvider;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TUI.Hooks.CreateProvider.Event -= OnCreateProvider;
                TUI.Hooks.RemoveProvider.Event -= OnRemoveProvider;
            }
            base.Dispose(disposing);
        }

        private void OnCreateProvider(CreateProviderArgs args)
        {
            if (args.Provider != null)
                return;

            RootVisualObject root = args.Root;
            if (root.Personal)
                args.Provider = FakeProviderAPI.CreatePersonalTileProvider(root.Name, root.Observers,
                    root.X, root.Y, root.Width, root.Height, root.Layer);
            else
                args.Provider = FakeProviderAPI.CreateTileProvider(root.Name,
                    root.X, root.Y, root.Width, root.Height, root.Layer);
        }

        private void OnRemoveProvider(RemoveProviderArgs args)
        {
            if (args.Provider is FakeProvider.TileProvider provider)
                FakeProviderAPI.Tile.Remove(provider.Name);
        }
    }
}
