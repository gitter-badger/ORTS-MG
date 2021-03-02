﻿
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Position;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.View.DrawableComponents;
using Orts.View.Track.Shapes;

namespace Orts.View.Track.Widgets
{
    #region TrackItemBase
    internal abstract class TrackItemBase : PointWidget
    {
        private protected static System.Drawing.Font font;

        public TrackItemBase(TrackItem source)
        {
            Size = 3;
            ref readonly WorldLocation location = ref source.Location;
            base.location = PointD.FromWorldLocation(location);
            base.tile = new Tile(location.TileX, location.TileZ);
        }

        internal static void SetFont(System.Drawing.Font font)
        {
            TrackItemBase.font = font;
        }

        public static List<TrackItemBase> Create(TrackItem[] trackItems, SignalConfigurationFile signalConfig, TrackDB trackDb, Dictionary<uint, List<TrackSegment>> trackNodeSegments)
        {
            List<TrackItemBase> result = new List<TrackItemBase>();
            Dictionary<uint, SidingTrackItem> sidingItems = new Dictionary<uint, SidingTrackItem>();

            TrackVectorNode[] trackItemNodes = new TrackVectorNode[trackItems.Length];

            foreach (TrackNode node in trackDb.TrackNodes)
            {
                if (node is TrackVectorNode trackVectorNode && trackVectorNode.TrackItemIndices?.Length > 0)
                {
                    foreach (int trackItemIndex in trackVectorNode.TrackItemIndices)
                    {
                        trackItemNodes[trackItemIndex] = trackVectorNode;
                    }
                }
            }
            foreach (TrackItem trackItem in trackItems)
            {
                switch (trackItem)
                {
                    case SidingItem sidingItem:
                        SidingTrackItem trackSidingItem = new SidingTrackItem(sidingItem);
                        sidingItems.Add(trackSidingItem.Id, trackSidingItem);
                        break;
                    case PlatformItem platformItem:
                        result.Add(new PlatformTrackItem(platformItem));
                        break;
                    case SpeedPostItem speedPostItem:
                        result.Add(new SpeedPostTrackItem(speedPostItem));
                        break;
                    case HazardItem hazardItem:
                        result.Add(new HazardTrackItem(hazardItem));
                        break;
                    case PickupItem pickupItem:
                        result.Add(new PickupTrackItem(pickupItem));
                        break;
                    case LevelCrossingItem levelCrossingItem:
                        result.Add(new LevelCrossingTrackItem(levelCrossingItem));
                        break;
                    case SoundRegionItem soundRegionItem:
                        result.Add(new SoundRegionTrackItem(soundRegionItem));
                        break;
                    case SignalItem signalItem:
                        result.Add(new SignalTrackItem(signalItem, signalConfig, trackItemNodes, trackNodeSegments));
                        break;
                }
            }


            result.AddRange(SidingTrackItem.LinkSidingItems(sidingItems));

            return result;
        }
    }
    #endregion

    #region SidingTrackItem
    internal class SidingTrackItem : TrackItemBase
    {
        private readonly string sidingName;
        internal readonly uint Id;
        private readonly uint linkedId;
        private bool drawName;

        public SidingTrackItem(SidingItem source) : base(source)
        {
            sidingName = source.ItemName;
            Id = source.TrackItemId;
            linkedId = source.LinkedSidingId;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.CornflowerBlue, false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
            if (drawName)
                TextDrawShape.DrawString(contentArea.WorldToScreenCoordinates(in location), Color.Red, sidingName, font, Vector2.One, TextHorizontalAlignment.Left, TextVerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
        }

        /// <summary>
        /// Link siding items which belong together pairwise so text only appears once (otherwise text is mostly overlapping since siding items are too close to each other
        /// </summary>
        internal static List<SidingTrackItem> LinkSidingItems(Dictionary<uint, SidingTrackItem> sidingItems)
        {
            List<SidingTrackItem> result = new List<SidingTrackItem>();

            while (sidingItems.Count > 0)
            {
                uint sourceId = sidingItems.Keys.First();
                SidingTrackItem source = sidingItems[sourceId];
                sidingItems.Remove(sourceId);
                if (sidingItems.TryGetValue(source.linkedId, out SidingTrackItem target))
                {
                    if (target.linkedId != source.Id)
                    {
                        Trace.TraceWarning($"Siding Item Pair has inconsistent linking from Source Id {source.Id} to target {source.linkedId} vs Target id {target.Id} to source {target.linkedId}.");
                    }
                    sidingItems.Remove(target.Id);
                    result.Add(target);
                    source.drawName = true;
                    // TODO 20210115 maybe resulting location of text should be in the middle between the two linked siding items
                }
                else
                {
                    Trace.TraceWarning($"Linked Siding Item {source.linkedId} for Siding Item {source.Id} not found.");
                }
                result.Add(source);
            }

            return result;
        }
    }
    #endregion

    #region PlatformTrackItem
    internal class PlatformTrackItem : TrackItemBase
    {
        private readonly string platformName;

        public PlatformTrackItem(PlatformItem source) : 
            base(source)
        {
            platformName = source.ItemName;
            Size = 9;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Platform, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.White, false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
            TextDrawShape.DrawString(contentArea.WorldToScreenCoordinates(in location), Color.Blue, platformName, font, Vector2.One, TextHorizontalAlignment.Left, TextVerticalAlignment.Top, SpriteEffects.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region SpeedPostTrackItem
    internal class SpeedPostTrackItem : TrackItemBase
    {
        private readonly string distance;
        private readonly bool milePost;

        public SpeedPostTrackItem(SpeedPostItem source) : base(source)
        {
            distance = source.Distance.ToString(CultureInfo.CurrentCulture);
            milePost = source.IsMilePost;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            // TODO 20210117 show more of the SpeedPostItem properties (direction, number/dot)
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.Orange, false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
            TextDrawShape.DrawString(contentArea.WorldToScreenCoordinates(in location), Color.Blue, distance, font, Vector2.One, TextHorizontalAlignment.Center, TextVerticalAlignment.Center, SpriteEffects.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region HazardTrackItem
    internal class HazardTrackItem : TrackItemBase
    {
        public HazardTrackItem(HazardItem source) : base(source)
        {
            Size = 9f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Hazard, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.White, false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region PickupTrackItem
    internal class PickupTrackItem : TrackItemBase
    {
        public PickupTrackItem(PickupItem source) : base(source)
        {
            Size = 9f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Pickup, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.White, false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region LevelCrossingTrackItem
    internal class LevelCrossingTrackItem : TrackItemBase
    {
        public LevelCrossingTrackItem(LevelCrossingItem source) : base(source)
        {
            Size = 6f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Disc, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.Purple, false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region SoundRegionTrackItem
    internal class SoundRegionTrackItem : TrackItemBase
    {
        public SoundRegionTrackItem(SoundRegionItem source) : base(source)
        {
            Size = 5f;
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Sound, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size), Color.White, false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion

    #region SignalTrackItem
    internal class SignalTrackItem : TrackItemBase
    {
        private readonly float angle;
        private readonly bool normal = true;

        public SignalTrackItem(SignalItem source, SignalConfigurationFile signalConfig, TrackVectorNode[] trackItemNodes, Dictionary<uint, List<TrackSegment>> trackNodeSegments) : base(source)
        {
            Size = 7f;
            if (signalConfig.SignalTypes.ContainsKey(source.SignalType))
            {
                normal = signalConfig.SignalTypes[source.SignalType].FunctionType == SignalFunction.Normal;
            }


            PointD sourcelocation = PointD.FromWorldLocation(source.Location);
            double closest = double.MaxValue;
            TrackSegment closestSegment = null;

            TrackVectorNode vectorNode = trackItemNodes[source.TrackItemId];
            if (vectorNode != null)
            {
                foreach (TrackSegment segment in trackNodeSegments[vectorNode.Index])
                {
                    double currentDistance = sourcelocation.DistanceToLineSegmentSquared(segment.Location, segment.Vector);
                    if (currentDistance < closest)
                    {
                        closest = currentDistance;
                        closestSegment = segment;
                    }

                }
            }

            angle = MathHelper.WrapAngle((closestSegment?.Direction ?? 0) + MathHelper.PiOver2 + (source.Direction == Common.TrackDirection.Reverse ? MathHelper.TwoPi : MathHelper.Pi));

            Vector3 shiftedLocation = source.Location.Location +
                    0.1f * new Vector3((float)Math.Cos(angle), 0f, -(float)Math.Sin(angle));
                WorldLocation location = new WorldLocation(source.Location.TileX, source.Location.TileZ, shiftedLocation);
            base.location = PointD.FromWorldLocation(location);

        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None)
        {
            BasicShapes.DrawTexture(BasicTextureType.Signal, contentArea.WorldToScreenCoordinates(in Location), angle, contentArea.WorldToScreenSize(Size), Color.White, false, false, colorVariation != ColorVariation.None, contentArea.SpriteBatch);
        }
    }
    #endregion
}
