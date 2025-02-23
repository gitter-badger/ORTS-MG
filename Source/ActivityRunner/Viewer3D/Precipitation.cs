﻿// COPYRIGHT 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Shaders;
using Orts.Common;
using Orts.Common.Position;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D
{
    public class PrecipitationViewer
    {
        public const float MinIntensityPPSPM2 = 0;
        // 16 bit version.
        public const float MaxIntensityPPSPM2_16 = 0.010f;
        // Default 32 bit version.
        public const float MaxIntensityPPSPM2 = 0.035f;
                
        readonly Viewer Viewer;
        readonly WeatherControl WeatherControl;
        readonly Weather Weather;

        readonly Material Material;
        readonly PrecipitationPrimitive Pricipitation;

        Vector3 Wind;

        public PrecipitationViewer(Viewer viewer, WeatherControl weatherControl)
        {
            Viewer = viewer;
            WeatherControl = weatherControl;
            Weather = viewer.Simulator.Weather;

            Material = viewer.MaterialManager.Load("Precipitation");
            Pricipitation = new PrecipitationPrimitive(Viewer.RenderProcess.GraphicsDevice);

            Wind = new Vector3(0, 0, 0);
            Reset();
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var gameTime = (float)Viewer.Simulator.GameTime;
            Pricipitation.DynamicUpdate(WeatherControl, Weather, Viewer, ref Wind);
            Pricipitation.Update(gameTime, elapsedTime, Weather.PricipitationIntensityPPSPM2, Viewer);

            // Note: This is quite a hack. We ideally should be able to pass this through RenderItem somehow.
            var XNAWorldLocation = Matrix.Identity;
            XNAWorldLocation.M11 = gameTime;
            XNAWorldLocation.M21 = Viewer.Camera.TileX;
            XNAWorldLocation.M22 = Viewer.Camera.TileZ;

            frame.AddPrimitive(Material, Pricipitation, RenderPrimitiveGroup.Precipitation, ref XNAWorldLocation);
        }

        public void Reset()
        {
            // This procedure is only called once at the start of an activity.
            // Added random Wind.X value for rain and snow.
            // Max value used by randWind.Next is max value - 1.
            Wind.X = Viewer.Simulator.WeatherType == WeatherType.Snow ? Viewer.Random.Next(2, 6) : Viewer.Random.Next(15, 21);
                                    
            var gameTime = (float)Viewer.Simulator.GameTime;
            Pricipitation.Initialize(Viewer.Simulator.WeatherType, Wind);
            // Camera is null during first initialisation.
            if (Viewer.Camera != null) Pricipitation.Update(gameTime, ElapsedTime.Zero, Weather.PricipitationIntensityPPSPM2, Viewer);
        }

        //[CallOnThread("Loader")]
        internal void Mark()
        {
            Material.Mark();
        }
    }

    public class PrecipitationPrimitive : RenderPrimitive
    {
        // http://www-das.uwyo.edu/~geerts/cwx/notes/chap09/hydrometeor.html
        // "Rain  1.8 - 2.2mm  6.1 - 6.9m/s"
        const float RainVelocityMpS = 6.9f;
        // "Snow flakes of any size falls at about 1 m/s"
        const float SnowVelocityMpS = 1.0f;
        // This is a fiddle factor because the above values feel too slow. Alternative suggestions welcome.
        const float ParticleVelocityFactor = 10.0f;

        readonly float ParticleBoxLengthM;
        readonly float ParticleBoxWidthM;
        readonly float ParticleBoxHeightM;

        // 16bit Box Parameters
        const float ParticleBoxLengthM_16 = 500;
        const float ParticleBoxWidthM_16 = 500;
        const float ParticleBoxHeightM_16 = 43;

        const int IndiciesPerParticle = 6;
        const int VerticiesPerParticle = 4;
        const int PrimitivesPerParticle = 2;

        readonly int MaxParticles;
        readonly ParticleVertex[] Vertices;
        readonly VertexDeclaration VertexDeclaration;
        readonly int VertexStride;
        readonly DynamicVertexBuffer VertexBuffer;
        readonly IndexBuffer IndexBuffer;

        struct ParticleVertex
        {
            public Vector4 StartPosition_StartTime;
            public Vector4 EndPosition_EndTime;
            public Vector4 TileXZ_Vertex;

            public static readonly VertexElement[] VertexElements =
            {
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.Position, 0),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.Position, 1),
                new VertexElement(16 + 16, VertexElementFormat.Vector4, VertexElementUsage.Position, 2),
            };

            public static int SizeInBytes = sizeof(float) * (4 + 4) + sizeof(float) * 4;
        }

        float ParticleDuration;
        Vector3 ParticleDirection;
        HeightCache Heights;

        // Particle buffer goes like this:
        //   +--active>-----new>--+
        //   |                    |
        //   +--<retired---<free--+

        int FirstActiveParticle;
        int FirstNewParticle;
        int FirstFreeParticle;
        int FirstRetiredParticle;

        float ParticlesToEmit;
        float TimeParticlesLastEmitted;
        int DrawCounter;

        public PrecipitationPrimitive(GraphicsDevice graphicsDevice)
        {
            // Snow is the slower particle, hence longer duration, hence more particles in total.
            // Setting the precipitaton box size based on GraphicsDeviceCapabilities.
            if (graphicsDevice.GraphicsProfile == GraphicsProfile.HiDef)
            {
                ParticleBoxLengthM = (float)Simulator.Instance.Settings.PrecipitationBoxLength;
                ParticleBoxWidthM = (float)Simulator.Instance.Settings.PrecipitationBoxWidth;
                ParticleBoxHeightM = (float)Simulator.Instance.Settings.PrecipitationBoxHeight;
            }
            else
            {
                ParticleBoxLengthM = ParticleBoxLengthM_16;
                ParticleBoxWidthM = ParticleBoxWidthM_16;
                ParticleBoxHeightM = ParticleBoxHeightM_16;
            }
            if (graphicsDevice.GraphicsProfile == GraphicsProfile.HiDef)
                MaxParticles = (int)(PrecipitationViewer.MaxIntensityPPSPM2 * ParticleBoxLengthM * ParticleBoxWidthM * ParticleBoxHeightM / SnowVelocityMpS / ParticleVelocityFactor);
            // Processing 16bit device
            else
                MaxParticles = (int)(PrecipitationViewer.MaxIntensityPPSPM2_16 * ParticleBoxLengthM * ParticleBoxWidthM * ParticleBoxHeightM / SnowVelocityMpS / ParticleVelocityFactor);
            // Checking if graphics device is 16bit.
            if (graphicsDevice.GraphicsProfile != GraphicsProfile.HiDef)
                Debug.Assert(MaxParticles * VerticiesPerParticle < ushort.MaxValue, "The maximum number of precipitation verticies must be able to fit in a ushort (16bit unsigned) index buffer.");
            Vertices = new ParticleVertex[MaxParticles * VerticiesPerParticle];
            VertexDeclaration = new VertexDeclaration(ParticleVertex.SizeInBytes, ParticleVertex.VertexElements);
            VertexStride = Marshal.SizeOf(typeof(ParticleVertex));
            VertexBuffer = new DynamicVertexBuffer(graphicsDevice, VertexDeclaration, MaxParticles * VerticiesPerParticle, BufferUsage.WriteOnly);
            // Processing either 32bit or 16bit InitIndexBuffer depending on GraphicsDeviceCapabilities.
            if (graphicsDevice.GraphicsProfile == GraphicsProfile.HiDef)
                IndexBuffer = InitIndexBuffer(graphicsDevice, MaxParticles * IndiciesPerParticle);
            else
                IndexBuffer = InitIndexBuffer16(graphicsDevice, MaxParticles * IndiciesPerParticle);
            Heights = new HeightCache(8);
            // This Trace command is used to show how much memory is used.
            Trace.TraceInformation(String.Format("Allocation for {0:N0} particles:\n\n  {1,13:N0} B RAM vertex data\n  {2,13:N0} B RAM index data (temporary)\n  {1,13:N0} B VRAM DynamicVertexBuffer\n  {2,13:N0} B VRAM IndexBuffer", MaxParticles, Marshal.SizeOf(typeof(ParticleVertex)) * MaxParticles * VerticiesPerParticle, sizeof(uint) * MaxParticles * IndiciesPerParticle));
        }

        void VertexBuffer_ContentLost()
        {
            VertexBuffer.SetData(0, Vertices, 0, Vertices.Length, VertexStride, SetDataOptions.NoOverwrite);
        }
        // IndexBuffer for 32bit process.
        static IndexBuffer InitIndexBuffer(GraphicsDevice graphicsDevice, int numIndicies)
        {
            var indices = new uint[numIndicies];
            var index = 0;
            for (var i = 0; i < numIndicies; i += IndiciesPerParticle)
            {
                indices[i] = (uint)index;
                indices[i + 1] = (uint)(index + 1);
                indices[i + 2] = (uint)(index + 2);

                indices[i + 3] = (uint)(index + 2);
                indices[i + 4] = (uint)(index + 3);
                indices[i + 5] = (uint)(index);

                index += VerticiesPerParticle;
            }
            var indexBuffer = new IndexBuffer(graphicsDevice, typeof(uint), numIndicies, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
            return indexBuffer;
        }
        // IndexBuffer for computers that still use 16bit graphics.
        static IndexBuffer InitIndexBuffer16(GraphicsDevice graphicsDevice, int numIndicies)
        {
            var indices = new ushort[numIndicies];
            var index = 0;
            for (var i = 0; i < numIndicies; i += IndiciesPerParticle)
            {
                indices[i] = (ushort)index;
                indices[i + 1] = (ushort)(index + 1);
                indices[i + 2] = (ushort)(index + 2);

                indices[i + 3] = (ushort)(index + 2);
                indices[i + 4] = (ushort)(index + 3);
                indices[i + 5] = (ushort)(index);

                index += VerticiesPerParticle;
            }
            var indexBuffer = new IndexBuffer(graphicsDevice, typeof(ushort), numIndicies, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
            return indexBuffer;
        }

        void RetireActiveParticles(float currentTime)
        {
            while (FirstActiveParticle != FirstNewParticle)
            {
                var vertex = FirstActiveParticle * VerticiesPerParticle;
                var expiry = Vertices[vertex].EndPosition_EndTime.W;

                // Stop as soon as we find the first particle which hasn't expired.
                if (expiry > currentTime)
                    break;

                // Expire particle.
                Vertices[vertex].StartPosition_StartTime.W = (float)DrawCounter;
                FirstActiveParticle = (FirstActiveParticle + 1) % MaxParticles;
            }
        }

        void FreeRetiredParticles()
        {
            while (FirstRetiredParticle != FirstActiveParticle)
            {
                var vertex = FirstRetiredParticle * VerticiesPerParticle;
                var age = DrawCounter - (int)Vertices[vertex].StartPosition_StartTime.W;

                // Stop as soon as we find the first expired particle which hasn't been expired for at least 2 'ticks'.
                if (age < 2)
                    break;

                FirstRetiredParticle = (FirstRetiredParticle + 1) % MaxParticles;
            }
        }

        int GetCountFreeParticles()
        {
            var nextFree = (FirstFreeParticle + 1) % MaxParticles;

            if (nextFree <= FirstRetiredParticle)
                return FirstRetiredParticle - nextFree;

            return (MaxParticles - nextFree) + FirstRetiredParticle;
        }

        public void Initialize(WeatherType weather, Vector3 wind)
        {
            ParticleDuration = ParticleBoxHeightM / (weather == WeatherType.Snow ? SnowVelocityMpS : RainVelocityMpS) / ParticleVelocityFactor;
            ParticleDirection = wind;
            FirstActiveParticle = FirstNewParticle = FirstFreeParticle = FirstRetiredParticle = 0;
            ParticlesToEmit = TimeParticlesLastEmitted = 0;
            DrawCounter = 0;
        }

        public void DynamicUpdate(WeatherControl weatherControl, Weather weather, Viewer viewer, ref Vector3 wind)
        {
            if (!weatherControl.weatherChangeOn || weatherControl.dynamicWeather.precipitationLiquidityTimer <= 0) return;
            ParticleDuration = ParticleBoxHeightM / ((RainVelocityMpS-SnowVelocityMpS) *  weather.PrecipitationLiquidity + SnowVelocityMpS)/ ParticleVelocityFactor;
            wind.X = 18 * weather.PrecipitationLiquidity + 2;
            ParticleDirection = wind;
        }

        public void Update(float currentTime, in ElapsedTime elapsedTime, float particlesPerSecondPerM2, Viewer viewer)
        {
            var tiles = viewer.Tiles;
            var scenery = viewer.World.Scenery;
            var worldLocation = viewer.Camera.CameraWorldLocation;
            //var worldLocation = Program.Viewer.PlayerLocomotive.WorldPosition.WorldLocation;  // This is used to test overall precipitation position.

            if (TimeParticlesLastEmitted == 0)
            {
                TimeParticlesLastEmitted = currentTime - ParticleDuration;
                ParticlesToEmit += ParticleDuration * particlesPerSecondPerM2 * ParticleBoxLengthM * ParticleBoxWidthM;
            }
            else
            {
                RetireActiveParticles(currentTime);
                FreeRetiredParticles();

                ParticlesToEmit += (float)elapsedTime.ClockSeconds * particlesPerSecondPerM2 * ParticleBoxLengthM * ParticleBoxWidthM;
            }

            var numParticlesAdded = 0;
            var numToBeEmitted = (int)ParticlesToEmit;
            var numCanBeEmitted = GetCountFreeParticles();
            var numToEmit = Math.Min(numToBeEmitted, numCanBeEmitted);

            for (var i = 0; i < numToEmit; i++)
            {
                WorldLocation temp = new WorldLocation(worldLocation.TileX, worldLocation.TileZ, 
                    worldLocation.Location.X + (float)((Viewer.Random.NextDouble() - 0.5) * ParticleBoxWidthM), 
                    0, 
                    worldLocation.Location.Z + (float)((Viewer.Random.NextDouble() - 0.5) * ParticleBoxLengthM));
                temp = new WorldLocation(temp.TileX, temp.TileZ, temp.Location.X, Heights.GetHeight(temp, tiles, scenery), temp.Location.Z);
                var position = new WorldPosition(temp);

                var time = MathHelper.Lerp(TimeParticlesLastEmitted, currentTime, (float)i / numToEmit);
                var particle = (FirstFreeParticle + 1) % MaxParticles;
                var vertex = particle * VerticiesPerParticle;

                for (var j = 0; j < VerticiesPerParticle; j++)
                {
                    Vertices[vertex + j].StartPosition_StartTime = new Vector4(position.XNAMatrix.Translation - ParticleDirection * ParticleDuration, time);
                    Vertices[vertex + j].StartPosition_StartTime.Y += ParticleBoxHeightM;
                    Vertices[vertex + j].EndPosition_EndTime = new Vector4(position.XNAMatrix.Translation, time + ParticleDuration);
                    Vertices[vertex + j].TileXZ_Vertex = new Vector4(position.TileX, position.TileZ, j, 0);
                }

                FirstFreeParticle = particle;
                ParticlesToEmit--;
                numParticlesAdded++;
            }

            if (numParticlesAdded > 0)
                TimeParticlesLastEmitted = currentTime;

            ParticlesToEmit = ParticlesToEmit - (int)ParticlesToEmit;
        }

        void AddNewParticlesToVertexBuffer()
        {
            if (FirstNewParticle < FirstFreeParticle)
            {
                var numParticlesToAdd = FirstFreeParticle - FirstNewParticle;
                VertexBuffer.SetData(FirstNewParticle * VertexStride * VerticiesPerParticle, Vertices, FirstNewParticle * VerticiesPerParticle, numParticlesToAdd * VerticiesPerParticle, VertexStride, SetDataOptions.NoOverwrite);
            }
            else
            {
                var numParticlesToAddAtEnd = MaxParticles - FirstNewParticle;
                VertexBuffer.SetData(FirstNewParticle * VertexStride * VerticiesPerParticle, Vertices, FirstNewParticle * VerticiesPerParticle, numParticlesToAddAtEnd * VerticiesPerParticle, VertexStride, SetDataOptions.NoOverwrite);
                if (FirstFreeParticle > 0)
                    VertexBuffer.SetData(0, Vertices, 0, FirstFreeParticle * VerticiesPerParticle, VertexStride, SetDataOptions.NoOverwrite);
            }

            FirstNewParticle = FirstFreeParticle;
        }

        public bool HasParticlesToRender()
        {
            return FirstActiveParticle != FirstFreeParticle;
        }

        public override void Draw()
        {
            if (VertexBuffer.IsContentLost)
                VertexBuffer_ContentLost();

            if (FirstNewParticle != FirstFreeParticle)
                AddNewParticlesToVertexBuffer();

            if (HasParticlesToRender())
            {
                graphicsDevice.Indices = IndexBuffer;
                graphicsDevice.SetVertexBuffer(VertexBuffer);

                if (FirstActiveParticle < FirstFreeParticle)
                {
                    var numParticles = FirstFreeParticle - FirstActiveParticle;
                    graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, FirstActiveParticle * IndiciesPerParticle, numParticles * PrimitivesPerParticle);
                }
                else
                {
                    var numParticlesAtEnd = MaxParticles - FirstActiveParticle;
                    if (numParticlesAtEnd > 0)
                        graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, FirstActiveParticle * IndiciesPerParticle, numParticlesAtEnd * PrimitivesPerParticle);
                    if (FirstFreeParticle > 0)
                        graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, FirstFreeParticle * PrimitivesPerParticle);
                }
            }

            DrawCounter++;
        }

        class HeightCache
        {
            const int TileCount = 10;

            readonly int BlockSize;
            readonly int Divisions;
            readonly List<Tile> Tiles = new List<Tile>();

            public HeightCache(int blockSize)
            {
                BlockSize = blockSize;
                Divisions = (int)Math.Round(2048f / blockSize);
            }

            public float GetHeight(in WorldLocation location, TileManager tiles, SceneryDrawer scenery)
            {
                WorldLocation temp = location.Normalize();

                // First, ensure we have the tile in question cached.
                var tile = Tiles.FirstOrDefault(t => t.TileX == temp.TileX && t.TileZ == temp.TileZ);
                if (tile == null)
                    Tiles.Add(tile = new Tile(temp.TileX, temp.TileZ, Divisions));

                // Remove excess entries.
                if (Tiles.Count > TileCount)
                    Tiles.RemoveAt(0);

                // Now calculate division to query.
                var x = (int)((temp.Location.X + 1024) / BlockSize);
                var z = (int)((temp.Location.Z + 1024) / BlockSize);

                // Trace the case where x or z are out of bounds
                if (x < 0 || x > 255 || z < 0 || z > 255)
                {
                    Trace.TraceWarning("Precipitation indexes are out of bounds:  x = {0}, z = {1}, Location.X = {2}, Location.Z = {3}, BlockSize = {4}, HeightDimensionX = {5}, HeightDimensionZ = {6}",
                        x, z, location.Location.X, location.Location.Z, BlockSize, tile.Height.GetLength(0), tile.Height.GetLength(1));
                    if (x > 255) x = 255;
                    else if (z > 255) z = 255;
                    else if (x < 0) x = 0;
                    else z = 0;
                }
                // If we don't have it cached, load it.
                if (tile.Height[x, z] == float.MinValue)
                {
                    var position = new WorldLocation(temp.TileX, temp.TileZ, (x + 0.5f) * BlockSize - 1024, 0, (z + 0.5f) * BlockSize - 1024);
                    tile.Height[x, z] = Math.Max(tiles.GetElevation(position), scenery.GetBoundingBoxTop(position, BlockSize));
                    tile.Used++;
                }

                return tile.Height[x, z];
            }

            [DebuggerDisplay("Tile = {TileX},{TileZ} Used = {Used}")]
            class Tile
            {
                public readonly int TileX;
                public readonly int TileZ;
                public readonly float[,] Height;
                public int Used;

                public Tile(int tileX, int tileZ, int divisions)
                {
                    TileX = tileX;
                    TileZ = tileZ;
                    Height = new float[divisions, divisions];
                    for (var x = 0; x < divisions; x++)
                        for (var z = 0; z < divisions; z++)
                            Height[x, z] = float.MinValue;
                }
            }
        }
    }

    public class PrecipitationMaterial : Material
    {
        private readonly Texture2D rainTexture;
        private readonly Texture2D snowTexture;
        private readonly Texture2D[] dynamicPrecipitationTexture = new Texture2D[12];
        private readonly PrecipitationShader shader;

        public PrecipitationMaterial(Viewer viewer)
            : base(viewer, null)
        {
            // TODO: This should happen on the loader thread.
            rainTexture = SharedTextureManager.Get(Viewer.RenderProcess.GraphicsDevice, System.IO.Path.Combine(Viewer.ContentPath, "Raindrop.png"));
            snowTexture = SharedTextureManager.Get(Viewer.RenderProcess.GraphicsDevice, System.IO.Path.Combine(Viewer.ContentPath, "Snowflake.png"));
            dynamicPrecipitationTexture[0] = snowTexture;
            dynamicPrecipitationTexture[11] = rainTexture;
            for (int i = 1; i<=10; i++)
            {
                var path = "Raindrop" + i.ToString() + ".png";
                dynamicPrecipitationTexture[11 - i] = SharedTextureManager.Get(Viewer.RenderProcess.GraphicsDevice, System.IO.Path.Combine(Viewer.ContentPath, path));
            }
            shader = Viewer.MaterialManager.PrecipitationShader;
        }

        public override void SetState(Material previousMaterial)
        {
            shader.CurrentTechnique = shader.Techniques[0]; //["Precipitation"];

            shader.LightVector.SetValue(Viewer.Settings.UseMSTSEnv ? Viewer.World.MSTSSky.mstsskysolarDirection : Viewer.World.Sky.solarDirection);
            shader.particleSize.SetValue(1f);
            if (Viewer.Simulator.Weather.PrecipitationLiquidity == 0 || Viewer.Simulator.Weather.PrecipitationLiquidity == 1)
            {
                shader.precipitation_Tex.SetValue(Viewer.Simulator.WeatherType == WeatherType.Snow ? snowTexture :
                    Viewer.Simulator.WeatherType == WeatherType.Rain ? rainTexture :
                    Viewer.Simulator.Weather.PrecipitationLiquidity == 0 ? snowTexture : rainTexture);
            }
            else
            {
                var precipitation_TexIndex = (int)(Viewer.Simulator.Weather.PrecipitationLiquidity * 11);
                shader.precipitation_Tex.SetValue(dynamicPrecipitationTexture[precipitation_TexIndex]);
            }

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            foreach (var pass in shader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    // Note: This is quite a hack. We ideally should be able to pass this through RenderItem somehow.
                    shader.cameraTileXZ.SetValue(new Vector2(item.XNAMatrix.M21, item.XNAMatrix.M22));
                    shader.currentTime.SetValue(item.XNAMatrix.M11);

                    shader.SetMatrix(ref view, ref projection);
                    pass.Apply();
                    item.RenderPrimitive.Draw();
                }
            }
        }

        public override void ResetState()
        {
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        public override bool GetBlending()
        {
            return true;
        }

        public override void Mark()
        {
            Viewer.TextureManager.Mark(rainTexture);
            Viewer.TextureManager.Mark(snowTexture);
            for (int i = 1; i <= 10; i++)
                Viewer.TextureManager.Mark(dynamicPrecipitationTexture[i]);
            base.Mark();
        }
    }

    //[CallOnThread("Render")]
    public class PrecipitationShader : BaseShader
    {
        internal readonly EffectParameter worldViewProjection;
        internal readonly EffectParameter invView;
        internal readonly EffectParameter LightVector;
        internal readonly EffectParameter particleSize;
        internal readonly EffectParameter cameraTileXZ;
        internal readonly EffectParameter currentTime;
        internal readonly EffectParameter precipitation_Tex;

        public PrecipitationShader(GraphicsDevice graphicsDevice)
            : base(graphicsDevice, "PrecipitationShader")
        {
            worldViewProjection = Parameters["worldViewProjection"];
            invView = Parameters["invView"];
            LightVector = Parameters["LightVector"];
            particleSize = Parameters["particleSize"];
            cameraTileXZ = Parameters["cameraTileXZ"];
            currentTime = Parameters["currentTime"];
            precipitation_Tex = Parameters["precipitation_Tex"];
        }

        public void SetMatrix(ref Matrix view, ref Matrix projection)
        {
            worldViewProjection.SetValue(Matrix.Identity * view * projection);
            invView.SetValue(Matrix.Invert(view));
        }
    }
}
