﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace Luminance.Core.Graphics
{
    [Autoload(Side = ModSide.Client)]
    public class PrimitivePixelationSystem : ModSystem
    {
        #region Fields/Properties
        private static ManagedRenderTarget PixelationTarget_BeforeNPCs;

        private static ManagedRenderTarget PixelationTarget_AfterNPCs;

        private static ManagedRenderTarget PixelationTarget_BeforeProjectiles;

        private static ManagedRenderTarget PixelationTarget_AfterProjectiles;

        private static readonly Dictionary<PixelationPrimitiveLayer, Queue<Action>> CustomDrawActionsByLayer = [];

        private static RenderTarget2D CreatePixelTarget(int width, int height) => new(Main.instance.GraphicsDevice, width / 2, height / 2);

        /// <summary>
        /// Whether the system is currently rendering any primitives.
        /// </summary>
        public static bool CurrentlyRendering
        {
            get;
            private set;
        }
        #endregion

        #region Loading
        public override void Load()
        {
            On_Main.CheckMonoliths += DrawToTargets;
            On_Main.DoDraw_DrawNPCsOverTiles += DrawTarget_NPCs;
            On_Main.DrawProjectiles += DrawTarget_Projectiles;

            PixelationTarget_BeforeNPCs = new(true, CreatePixelTarget);
            PixelationTarget_AfterNPCs = new(true, CreatePixelTarget);
            PixelationTarget_BeforeProjectiles = new(true, CreatePixelTarget);
            PixelationTarget_AfterProjectiles = new(true, CreatePixelTarget);
        }

        public override void Unload()
        {
            On_Main.CheckMonoliths -= DrawToTargets;
            On_Main.DoDraw_DrawNPCsOverTiles -= DrawTarget_NPCs;
        }
        #endregion

        #region Drawing To Targets

        private static Queue<Action> SafeGetDrawActionLayer(PixelationPrimitiveLayer layer)
        {
            if (!CustomDrawActionsByLayer.ContainsKey(layer))
                CustomDrawActionsByLayer[layer] = new();

            return CustomDrawActionsByLayer[layer];
        }

        private void DrawToTargets(On_Main.orig_CheckMonoliths orig)
        {
            if (Main.gameMenu)
            {
                orig();
                return;
            }

            var beforeNPCs = new List<IPixelatedPrimitiveRenderer>();
            var afterNPCs = new List<IPixelatedPrimitiveRenderer>();
            var beforeProjectiles = new List<IPixelatedPrimitiveRenderer>();
            var afterProjectiles = new List<IPixelatedPrimitiveRenderer>();

            // Check every active projectile.
            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                // If the projectile is a mod projectile and uses the interface, add it to the list of primitives to draw this frame.
                if (projectile.ModProjectile is IPixelatedPrimitiveRenderer pixelPrimitiveProjectile)
                {
                    var listToUse = pixelPrimitiveProjectile.LayerToRenderTo switch
                    {
                        PixelationPrimitiveLayer.BeforeNPCs => beforeNPCs,
                        PixelationPrimitiveLayer.AfterNPCs => afterNPCs,
                        PixelationPrimitiveLayer.BeforeProjectiles => beforeProjectiles,
                        _ => afterProjectiles
                    };

                    listToUse.Add(pixelPrimitiveProjectile);
                }
            }

            // Check every active NPC.
            foreach (NPC npc in Main.ActiveNPCs)
            {
                // If the NPC is a mod NPC and uses the interface, add it to the list of primitives to draw this frame.
                if (npc.ModNPC is IPixelatedPrimitiveRenderer pixelPrimitiveNPC)
                {
                    var listToUse = pixelPrimitiveNPC.LayerToRenderTo switch
                    {
                        PixelationPrimitiveLayer.BeforeNPCs => beforeNPCs,
                        PixelationPrimitiveLayer.AfterNPCs => afterNPCs,
                        PixelationPrimitiveLayer.BeforeProjectiles => beforeProjectiles,
                        _ => afterProjectiles
                    };
                    listToUse.Add(pixelPrimitiveNPC);
                }
            }

            CurrentlyRendering = true;

            DrawPrimsToRenderTarget(PixelationTarget_BeforeNPCs, beforeNPCs, SafeGetDrawActionLayer(PixelationPrimitiveLayer.BeforeNPCs));
            DrawPrimsToRenderTarget(PixelationTarget_AfterNPCs, afterNPCs, SafeGetDrawActionLayer(PixelationPrimitiveLayer.AfterNPCs));
            DrawPrimsToRenderTarget(PixelationTarget_BeforeProjectiles, beforeProjectiles, SafeGetDrawActionLayer(PixelationPrimitiveLayer.BeforeProjectiles));
            DrawPrimsToRenderTarget(PixelationTarget_AfterProjectiles, afterProjectiles, SafeGetDrawActionLayer(PixelationPrimitiveLayer.AfterProjectiles));

            Main.instance.GraphicsDevice.SetRenderTarget(null);

            CurrentlyRendering = false;
            orig();
        }

        private static void DrawPrimsToRenderTarget(RenderTarget2D renderTarget, List<IPixelatedPrimitiveRenderer> pixelPrimitives, Queue<Action> manualDrawActions)
        {
            // Swap to the target regardless, in order to clear any leftover content from last frame. Not doing this results in the final frame lingering once it stops rendering.
            renderTarget.SwapToRenderTarget();

            if (pixelPrimitives.Any() || manualDrawActions.Any())
            {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null);

                foreach (var pixelPrimitiveDrawer in pixelPrimitives)
                    pixelPrimitiveDrawer.RenderPixelatedPrimitives(Main.spriteBatch);
                while (manualDrawActions.TryDequeue(out Action drawAction))
                    drawAction();

                Main.spriteBatch.End();
            }
        }

        /// <summary>
        /// Prepares a draw action for rendering to the pixelation target on the next frame.
        /// </summary>
        /// 
        /// <remarks>
        /// <i>This should only be used when absolutely necessary.</i> If possible, you should use <see cref="IPixelatedPrimitiveRenderer"/> instead.
        /// </remarks>
        /// 
        /// <param name="renderAction">The render action to perform.</param>
        /// <param name="layer">The layer to draw to.</param>
        public static void RenderToPrimsNextFrame(Action renderAction, PixelationPrimitiveLayer layer)
        {
            CustomDrawActionsByLayer[layer].Enqueue(renderAction);
        }
        #endregion

        #region Target Drawing
        private void DrawTarget_NPCs(On_Main.orig_DoDraw_DrawNPCsOverTiles orig, Main self)
        {
            DrawTargetScaled(PixelationTarget_BeforeNPCs);
            orig(self);
            DrawTargetScaled(PixelationTarget_AfterNPCs);
        }

        private void DrawTarget_Projectiles(On_Main.orig_DrawProjectiles orig, Main self)
        {
            DrawTargetScaled(PixelationTarget_BeforeProjectiles);
            orig(self);
            DrawTargetScaled(PixelationTarget_AfterProjectiles);
        }

        private static void DrawTargetScaled(ManagedRenderTarget target)
        {
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            Main.spriteBatch.Draw(target, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
            Main.spriteBatch.End();
        }
        #endregion
    }
}
