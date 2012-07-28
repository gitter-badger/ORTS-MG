﻿// COPYRIGHT 2012 by the Open Rails project.
// This code is provided to help you understand what Open Rails does and does
// not do. Suggestions and contributions to improve Open Rails are always
// welcome. Use of the code for any other purpose or distribution of the code
// to anyone else is prohibited without specific written permission from
// admin@openrails.org.
//
// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ORTS
{
    public class TransferShape : StaticShape
    {
        readonly Material Material;
        readonly TransferMesh Primitive;

        public TransferShape(Viewer3D viewer, MSTS.TransferObj transfer, WorldPosition position)
            : base(viewer, null, RemoveRotation(position), ShapeFlags.AutoZBias)
        {
            Material = viewer.MaterialManager.Load("Transfer", Helpers.GetRouteTextureFile(viewer.Simulator, Helpers.TextureFlags.None, transfer.FileName));
            Primitive = new TransferMesh(viewer, transfer.Width, transfer.Height, position);
        }

        static WorldPosition RemoveRotation(WorldPosition position)
        {
            var rv = new WorldPosition(position);
            var translation = rv.XNAMatrix.Translation;
            rv.XNAMatrix = Matrix.Identity;
            rv.XNAMatrix.Translation = translation;
            return rv;
        }

        public override void PrepareFrame(RenderFrame frame, ElapsedTime elapsedTime)
        {
            // Locate relative to the camera
            var dTileX = Location.TileX - Viewer.Camera.TileX;
            var dTileZ = Location.TileZ - Viewer.Camera.TileZ;
            var xnaTileTranslation = Matrix.CreateTranslation(dTileX * 2048, 0, -dTileZ * 2048);  // object is offset from camera this many tiles
            Matrix.Multiply(ref Location.XNAMatrix, ref xnaTileTranslation, out xnaTileTranslation);

            frame.AddPrimitive(Material, Primitive, RenderPrimitiveGroup.World, ref xnaTileTranslation, Flags);
        }

        internal override void Mark()
        {
            Material.Mark();
            base.Mark();
        }
    }

    public class TransferMesh : RenderPrimitive
    {
        readonly VertexDeclaration VertexDeclaration;
        readonly VertexBuffer VertexBuffer;
        readonly IndexBuffer IndexBuffer;

        public TransferMesh(Viewer3D viewer, float width, float height, WorldPosition position)
        {
            var center = position.Location;
            var radius = (float)Math.Sqrt(width * width + height * height) / 2;
            var minX = (int)Math.Floor((center.X - radius) / 8);
            var maxX = (int)Math.Ceiling((center.X + radius) / 8);
            var minZ = (int)Math.Floor((center.Z - radius) / 8);
            var maxZ = (int)Math.Ceiling((center.Z + radius) / 8);
            var xnaRotation = position.XNAMatrix;
            xnaRotation.Translation = Vector3.Zero;
            Matrix.Invert(ref xnaRotation, out xnaRotation);

            var verticies = new VertexPositionTexture[(maxX - minX + 1) * (maxZ - minZ + 1)];
            for (var x = 0; x <= maxX - minX; x++)
            {
                for (var z = 0; z <= maxZ - minZ; z++)
                {
                    var i = x * (maxZ - minZ + 1) + z;
                    verticies[i].Position.X = (x + minX) * 8 - center.X;
                    verticies[i].Position.Y = viewer.Tiles.GetElevation(position.TileX, position.TileZ, 128 + x + minX, 128 - z - minZ) - center.Y;
                    verticies[i].Position.Z = -(z + minZ) * 8 + center.Z;

                    var tc = new Vector3(verticies[i].Position.X / width, 0, verticies[i].Position.Z / height);
                    tc = Vector3.Transform(tc, xnaRotation);
                    verticies[i].TextureCoordinate.X = tc.X + 0.5f;
                    verticies[i].TextureCoordinate.Y = tc.Z + 0.5f;
                }
            }

            var indicies = new short[(maxX - minX) * (maxZ - minZ) * 6];
            for (var x = 0; x < maxX - minX; x++)
            {
                for (var z = 0; z < maxZ - minZ; z++)
                {
                    // Condition must match TerrainPatch.GetIndexBuffer's condition.
                    if ((((x + minX) & 1) == ((z + minZ) & 1)))
                    {
                        indicies[(x * (maxZ - minZ) + z) * 6 + 0] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 1] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 2] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 3] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 4] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 5] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 1));
                    }
                    else
                    {
                        indicies[(x * (maxZ - minZ) + z) * 6 + 0] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 1] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 2] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 3] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 4] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 5] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 0));
                    }
                }
            }

            VertexDeclaration = new VertexDeclaration(viewer.GraphicsDevice, VertexPositionTexture.VertexElements);
            VertexBuffer = new VertexBuffer(viewer.GraphicsDevice, VertexPositionTexture.SizeInBytes * verticies.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(verticies);
            IndexBuffer = new IndexBuffer(viewer.GraphicsDevice, typeof(short), indicies.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indicies);
        }

        public override void Draw(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.VertexDeclaration = VertexDeclaration;
            graphicsDevice.Vertices[0].SetSource(VertexBuffer, 0, VertexPositionTexture.SizeInBytes);
            graphicsDevice.Indices = IndexBuffer;
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, VertexBuffer.SizeInBytes / VertexPositionTexture.SizeInBytes, 0, IndexBuffer.SizeInBytes / sizeof(short) / 3);
        }
    }

    public class TransferMaterial : Material
    {
        readonly SceneryShader SceneryShader;
        readonly Texture2D Texture;
        IEnumerator<EffectPass> ShaderPasses;

        public TransferMaterial(Viewer3D viewer, string textureName)
            : base(viewer, textureName)
        {
            SceneryShader = Viewer.MaterialManager.SceneryShader;
            Texture = Viewer.TextureManager.Get(textureName);
        }

        public override void SetState(GraphicsDevice graphicsDevice, Material previousMaterial)
        {
            var shader = Viewer.MaterialManager.SceneryShader;
            shader.CurrentTechnique = shader.Techniques[Viewer.Settings.ShaderModel >= 3 ? "ImagePS3" : "ImagePS2"];
            if (ShaderPasses == null) ShaderPasses = shader.CurrentTechnique.Passes.GetEnumerator();
            shader.ImageTexture = Texture;

            var samplerState = graphicsDevice.SamplerStates[0];
            samplerState.AddressU = TextureAddressMode.Border;
            samplerState.AddressV = TextureAddressMode.Border;
            samplerState.BorderColor = Color.TransparentBlack;

            var rs = graphicsDevice.RenderState;
            rs.AlphaBlendEnable = true;
            rs.DestinationBlend = Blend.InverseSourceAlpha;
            rs.SeparateAlphaBlendEnabled = true;
            rs.SourceBlend = Blend.SourceAlpha;
        }

        public override void Render(GraphicsDevice graphicsDevice, IEnumerable<RenderItem> renderItems, ref Matrix XNAViewMatrix, ref Matrix XNAProjectionMatrix)
        {
            var shader = Viewer.MaterialManager.SceneryShader;
            var viewproj = XNAViewMatrix * XNAProjectionMatrix;

            shader.SetViewMatrix(ref XNAViewMatrix);
            shader.Begin();
            ShaderPasses.Reset();
            while (ShaderPasses.MoveNext())
            {
                ShaderPasses.Current.Begin();
                foreach (var item in renderItems)
                {
                    shader.SetMatrix(ref item.XNAMatrix, ref viewproj);
                    shader.ZBias = item.RenderPrimitive.ZBias;
                    shader.CommitChanges();
                    item.RenderPrimitive.Draw(graphicsDevice);
                }
                ShaderPasses.Current.End();
            }
            shader.End();
        }

        public override void ResetState(GraphicsDevice graphicsDevice)
        {
            var rs = graphicsDevice.RenderState;
            rs.AlphaBlendEnable = false;
            rs.DestinationBlend = Blend.Zero;
            rs.SeparateAlphaBlendEnabled = false;
            rs.SourceBlend = Blend.One;
        }

        public override bool GetBlending()
        {
            return true;
        }

        public override void Mark()
        {
            Viewer.TextureManager.Mark(Texture);
            base.Mark();
        }
    }
}
