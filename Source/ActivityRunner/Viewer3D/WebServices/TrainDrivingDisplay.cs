﻿// COPYRIGHT 2019, 2020 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Orts.Common;
using Orts.Common.Input;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.RollingStocks.SubSystems.Brakes;

namespace Orts.ActivityRunner.Viewer3D.WebServices
{
    public static class TrainDrivingDisplay
    {
        private static bool reverserBackwardDown;
        private static bool reverserForwardDown;
        private static bool throttleIncrease;
        private static bool throttleDecrease;
        private static bool trainBrakeIncrease;
        private static bool trainBrakeDecrease;
        private static bool gearUp;
        private static bool gearDown;
        private static bool pantographKey;

        private static Viewer viewer;

        public static void Initialize(Viewer viewer)
        {
            TrainDrivingDisplay.viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));

            viewer.UserCommandController.AddEvent(UserCommand.ControlReverserBackward, KeyEventType.KeyDown, () => reverserBackwardDown = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlReverserForward, KeyEventType.KeyDown, () => reverserForwardDown = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlThrottleIncrease, KeyEventType.KeyDown, () => throttleIncrease = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlThrottleDecrease, KeyEventType.KeyDown, () => throttleDecrease = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlTrainBrakeIncrease, KeyEventType.KeyDown, () => trainBrakeIncrease = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlTrainBrakeDecrease, KeyEventType.KeyDown, () => trainBrakeDecrease = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlGearUp, KeyEventType.KeyDown, () => gearUp = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlGearDown, KeyEventType.KeyDown, () => gearDown = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph1, KeyEventType.KeyDown, () => pantographKey = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph2, KeyEventType.KeyDown, () => pantographKey = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph3, KeyEventType.KeyDown, () => pantographKey = true);
            viewer.UserCommandController.AddEvent(UserCommand.ControlPantograph4, KeyEventType.KeyDown, () => pantographKey = true);
        }

        /// <summary>
        /// A Train Driving row with data fields.
        /// </summary>
        public struct ListLabel
        {
            public string FirstCol;
            public string LastCol;
            public string SymbolCol;
            public string KeyPressed;
        }

        /// <summary>
        /// Table of Colors to client-side color codes.
        /// </summary>
        /// <remarks>
        /// Compare codes with index.css.
        /// </remarks>
        private static readonly Dictionary<Color, string> ColorCode = new Dictionary<Color, string>
        {
            { Color.Yellow, "???" },
            { Color.Green, "??!" },
            { Color.Black, "?!?" },
            { Color.PaleGreen, "?!!" },
            { Color.White, "!??" },
            { Color.Orange, "!!?" },
            { Color.OrangeRed, "!!!" },
            { Color.Cyan, "%%%" },
            { Color.Brown, "%$$" },
            { Color.LightGreen, "%%$" },
            { Color.Blue, "$%$" },
            { Color.LightSkyBlue, "$$$" },
        };

        private static class Symbols
        {
            public const string ArrowUp = "▲";
            public const string SmallArrowUp = "△";
            public const string ArrowDown = "▼";
            public const string SmallArrowDown = "▽";
            public const string End = "▬";
            public const string EndLower = "▖";
            public const string ArrowToRight = "►";
            public const string SmallDiamond = "●";
            public const string GradientDown = "\u2198";
            public const string GradientUp = "\u2197";
        }

        private static readonly Dictionary<string, string> FirstColToAbbreviated = new Dictionary<string, string>()
        {
            {Viewer.Catalog.GetString("Autopilot"), Viewer.Catalog.GetString("AUTO")},
            {Viewer.Catalog.GetString("Boiler pressure"), Viewer.Catalog.GetString("PRES")},
            {Viewer.Catalog.GetString("Boiler water glass"), Viewer.Catalog.GetString("WATR")},
            {Viewer.Catalog.GetString("Boiler water level"), Viewer.Catalog.GetString("LEVL")},
            {Viewer.Catalog.GetString("Circuit breaker"), Viewer.Catalog.GetString("CIRC")},
            {Viewer.Catalog.GetString("Cylinder cocks"), Viewer.Catalog.GetString("CCOK")},
            {Viewer.Catalog.GetString("Direction"), Viewer.Catalog.GetString("DIRC")},
            {Viewer.Catalog.GetString("Doors open"), Viewer.Catalog.GetString("DOOR")},
            {Viewer.Catalog.GetString("Dynamic brake"), Viewer.Catalog.GetString("BDYN")},
            {Viewer.Catalog.GetString("Engine brake"), Viewer.Catalog.GetString("BLOC")},
            {Viewer.Catalog.GetString("Engine"), Viewer.Catalog.GetString("ENGN")},
            {Viewer.Catalog.GetString("Fire mass"), Viewer.Catalog.GetString("FIRE")},
            {Viewer.Catalog.GetString("Fixed gear"), Viewer.Catalog.GetString("GEAR")},
            {Viewer.Catalog.GetString("Fuel levels"), Viewer.Catalog.GetString("FUEL")},
            {Viewer.Catalog.GetString("Gear"), Viewer.Catalog.GetString("GEAR")},
            {Viewer.Catalog.GetString("Gradient"), Viewer.Catalog.GetString("GRAD")},
            {Viewer.Catalog.GetString("Grate limit"), Viewer.Catalog.GetString("GRAT")},
            {Viewer.Catalog.GetString("Pantographs"), Viewer.Catalog.GetString("PANT")},
            {Viewer.Catalog.GetString("Power"), Viewer.Catalog.GetString("POWR")},
            {Viewer.Catalog.GetString("Regulator"), Viewer.Catalog.GetString("REGL")},
            {Viewer.Catalog.GetString("Replay"), Viewer.Catalog.GetString("RPLY")},
            {Viewer.Catalog.GetString("Retainers"), Viewer.Catalog.GetString("RETN")},
            {Viewer.Catalog.GetString("Reverser"), Viewer.Catalog.GetString("REVR")},
            {Viewer.Catalog.GetString("Sander"), Viewer.Catalog.GetString("SAND")},
            {Viewer.Catalog.GetString("Speed"), Viewer.Catalog.GetString("SPED")},
            {Viewer.Catalog.GetString("Steam usage"), Viewer.Catalog.GetString("STEM")},
            {Viewer.Catalog.GetString("Throttle"), Viewer.Catalog.GetString("THRO")},
            {Viewer.Catalog.GetString("Time"), Viewer.Catalog.GetString("TIME")},
            {Viewer.Catalog.GetString("Train brake"), Viewer.Catalog.GetString("BTRN")},
            {Viewer.Catalog.GetString("Wheel"), Viewer.Catalog.GetString("WHEL")},
        };

        private static readonly Dictionary<string, string> LastColToAbbreviated = new Dictionary<string, string>()
        {
            {Viewer.Catalog.GetString("apply Service"), Viewer.Catalog.GetString("Apply")},
            {Viewer.Catalog.GetString("Apply Quick"), Viewer.Catalog.GetString("ApplQ")},
            {Viewer.Catalog.GetString("Apply Slow"), Viewer.Catalog.GetString("ApplS")},
            {Viewer.Catalog.GetString("coal"), Viewer.Catalog.GetString("c")},
            {Viewer.Catalog.GetString("Emergency Braking Push Button"), Viewer.Catalog.GetString("EmerBPB")},
            {Viewer.Catalog.GetString("Lap Self"), Viewer.Catalog.GetString("LapS")},
            {Viewer.Catalog.GetString("Minimum Reduction"), Viewer.Catalog.GetString("MRedc")},
            {Viewer.Catalog.GetString("safe range"), Viewer.Catalog.GetString("safe")},
            {Viewer.Catalog.GetString("skid"), Viewer.Catalog.GetString("Skid")},
            {Viewer.Catalog.GetString("slip warning"), Viewer.Catalog.GetString("Warning")},
            {Viewer.Catalog.GetString("slip"), Viewer.Catalog.GetString("Slip")},
            {Viewer.Catalog.GetString("water"), Viewer.Catalog.GetString("w")},
        };

        /// <summary>
        /// Sanitize the fields of a <see cref="ListLabel"/> in-place.
        /// </summary>
        /// <param name="label">A reference to the <see cref="ListLabel"/> to check.</param>
        private static void CheckLabel(ref ListLabel label, bool normalMode)
        {
            void CheckString(ref string s) => s = s ?? "";
            CheckString(ref label.FirstCol);
            CheckString(ref label.LastCol);
            CheckString(ref label.SymbolCol);
            CheckString(ref label.KeyPressed);

            if (!normalMode)
            {
                foreach (KeyValuePair<string, string> mapping in FirstColToAbbreviated)
                    label.FirstCol = label.FirstCol.Replace(mapping.Key, mapping.Value);
                foreach (KeyValuePair<string, string> mapping in LastColToAbbreviated)
                    label.LastCol = label.LastCol.Replace(mapping.Key, mapping.Value);
            }
        }


        /// <summary>
        /// Retrieve a formatted list <see cref="ListLabel"/>s to be displayed as an in-browser Track Monitor.
        /// </summary>
        /// <param name="viewer">The Viewer to read train data from.</param>
        /// <returns>A list of <see cref="ListLabel"/>s, one per row of the popup.</returns>
        public static IEnumerable<ListLabel> TrainDrivingDisplayList(this Viewer viewer, bool normalTextMode = true)
        {
            bool useMetric = viewer.MilepostUnitsMetric;
            var labels = new List<ListLabel>();
            void AddLabel(ListLabel label)
            {
                CheckLabel(ref label, normalTextMode);
                labels.Add(label);
            }
            void AddSeparator() => AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Sprtr"),
            });

            TrainCar trainCar = viewer.PlayerLocomotive;
            Train train = trainCar.Train;
            string trainBrakeStatus = trainCar.GetTrainBrakeStatus();
            string dynamicBrakeStatus = trainCar.GetDynamicBrakeStatus();
            string engineBrakeStatus = trainCar.GetEngineBrakeStatus();
            MSTSLocomotive locomotive = (MSTSLocomotive)trainCar;
            string locomotiveStatus = locomotive.GetStatus();
            bool combinedControlType = locomotive.CombinedControlType == MSTSLocomotive.CombinedControl.ThrottleDynamic;
            bool showMUReverser = Math.Abs(train.MUReverserPercent) != 100f;
            bool showRetainers = train.RetainerSetting != RetainerSetting.Exhaust;
            bool stretched = train.Cars.Count > 1 && train.CouplersPulled == train.Cars.Count - 1;
            bool bunched = !stretched && train.Cars.Count > 1 && train.CouplersPushed == train.Cars.Count - 1;
            Orts.Simulation.Physics.TrainInfo trainInfo = train.GetTrainInfo();

            // First Block
            // Client and server may have a time difference.
            AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Time"),
                LastCol = FormatStrings.FormatTime(viewer.Simulator.ClockTime + (MultiPlayer.MPManager.IsClient() ? MultiPlayer.MPManager.Instance().serverTimeDifference : 0)),
            });
            if (viewer.Simulator.IsReplaying)
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Replay"),
                    LastCol = FormatStrings.FormatTime(viewer.Log.ReplayEndsAt - viewer.Simulator.ClockTime),
                });
            }

            Color speedColor;
            if (locomotive.SpeedMpS < trainInfo.AllowedSpeed - 1f)
                speedColor = Color.White;
            else if (locomotive.SpeedMpS < trainInfo.AllowedSpeed)
                speedColor = Color.PaleGreen;
            else if (locomotive.SpeedMpS < trainInfo.AllowedSpeed + 5f)
                speedColor = Color.Orange;
            else
                speedColor = Color.OrangeRed;
            AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Speed"),
                LastCol = $"{FormatStrings.FormatSpeedDisplay(locomotive.SpeedMpS, useMetric)}{ColorCode[speedColor]}",
            });

            // Gradient info
            if (normalTextMode)
            {
                float gradient = -trainInfo.Gradient;
                const float minSlope = 0.00015f;
                string gradientIndicator;
                if (gradient < -minSlope)
                    gradientIndicator = $"{gradient:F1}%{Symbols.GradientDown}{ColorCode[Color.LightSkyBlue]}";
                else if (gradient > minSlope)
                    gradientIndicator = $"{gradient:F1}%{Symbols.GradientUp}{ColorCode[Color.Yellow]}";
                else
                    gradientIndicator = $"{gradient:F1}%";
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Gradient"),
                    LastCol = gradientIndicator,
                });
            }
            // Separator
            AddSeparator();

            // Second block
            // Direction
            {
                string reverserKey;
                bool moving = Math.Abs(trainCar.SpeedMpS) > 1;
                bool nonSteamEnd = trainCar.EngineType != TrainCar.EngineTypes.Steam && trainCar.Direction == MidpointDirection.N && (trainCar.ThrottlePercent >= 1 || moving);
                bool steamEnd = locomotive is MSTSSteamLocomotive steamLocomotive2 && steamLocomotive2.CutoffController.MaximumValue == Math.Abs(train.MUReverserPercent / 100);
                if (reverserForwardDown)
                {
                    if (nonSteamEnd || steamEnd)
                        reverserKey = Symbols.End + ColorCode[Color.Yellow];
                    else
                        reverserKey = Symbols.ArrowUp + ColorCode[Color.Yellow];
                    reverserForwardDown = false;
                }
                else if (reverserBackwardDown)
                {
                    if (nonSteamEnd || steamEnd)
                        reverserKey = Symbols.End + ColorCode[Color.Yellow];
                    else
                        reverserKey = Symbols.ArrowDown + ColorCode[Color.Yellow];
                    reverserBackwardDown = false;
                }
                else
                    reverserKey = "";

                string reverserIndicator = showMUReverser ? $"{Round(Math.Abs(train.MUReverserPercent))}% " : "";
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString(locomotive.EngineType == TrainCar.EngineTypes.Steam ? "Reverser" : "Direction"),
                    LastCol = $"{reverserIndicator}{FormatStrings.Catalog.GetParticularString("Reverser", locomotive.Direction.GetDescription())}",
                    KeyPressed = reverserKey,
                    SymbolCol = reverserKey,
                });
            }

            // Throttle
            {
                string throttleKey;
                if (locomotive.DynamicBrakePercent > -1)
                    throttleKey = Symbols.EndLower + ColorCode[Color.Yellow];
                else if (throttleIncrease)
                {
                    if (locomotive.DynamicBrakePercent < 1 && locomotive.ThrottleController.MaximumValue == trainCar.ThrottlePercent / 100)
                        throttleKey = Symbols.End + ColorCode[Color.Yellow];
                    else
                        throttleKey = Symbols.ArrowUp + ColorCode[Color.Yellow];
                    throttleIncrease = false;
                }
                else if (throttleDecrease)
                {
                    if (locomotive.DynamicBrakePercent < 1 && trainCar.ThrottlePercent == 0)
                        throttleKey = Symbols.End + ColorCode[Color.Yellow];
                    else
                        throttleKey = Symbols.ArrowDown + ColorCode[Color.Yellow];
                    throttleDecrease = false;
                }
                else
                    throttleKey = "";

                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString(locomotive is MSTSSteamLocomotive ? "Regulator" : "Throttle"),
                    LastCol = $"{Round(locomotive.ThrottlePercent)}%",
                    KeyPressed = throttleKey,
                    SymbolCol = throttleKey,
                });
            }

            // Cylinder Cocks
            if (locomotive is MSTSSteamLocomotive steamLocomotive)
            {
                string cocksIndicator, cocksKey;
                if (steamLocomotive.CylinderCocksAreOpen)
                {
                    cocksIndicator = Viewer.Catalog.GetString("Open") + ColorCode[Color.Orange];
                    cocksKey = Symbols.ArrowToRight + ColorCode[Color.Yellow];
                }
                else
                {
                    cocksIndicator = Viewer.Catalog.GetString("Closed") + ColorCode[Color.White];
                    cocksKey = "";
                }
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Cylinder cocks"),
                    LastCol = cocksIndicator,
                    KeyPressed = cocksKey,
                    SymbolCol = cocksKey,
                });
            }

            // Sander
            if (locomotive.GetSanderOn())
            {
                bool sanderBlocked = locomotive.AbsSpeedMpS > locomotive.SanderSpeedOfMpS;
                string sanderKey = Symbols.ArrowToRight + ColorCode[Color.Yellow];
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Sander"),
                    LastCol = sanderBlocked ? Viewer.Catalog.GetString("Blocked") + ColorCode[Color.OrangeRed] : Viewer.Catalog.GetString("On") + ColorCode[Color.Orange],
                    KeyPressed = sanderKey,
                    SymbolCol = sanderKey,
                });
            }
            else
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Sander"),
                    LastCol = Viewer.Catalog.GetString("Off"),
                    KeyPressed = "",
                    SymbolCol = "",
                });
            }

            AddSeparator();

            // Train Brake multi-lines
            // TODO: A better algorithm
            //var brakeStatus = Owner.Viewer.PlayerLocomotive.GetTrainBrakeStatus();
            //steam loco
            string brakeInfoValue = "";
            int index = 0;

            if (trainBrakeStatus.Contains(Viewer.Catalog.GetString("EQ")))
            {
                string brakeKey;
                if (trainBrakeIncrease)
                {
                    brakeKey = Symbols.ArrowUp + ColorCode[Color.Yellow];
                    trainBrakeIncrease = false;
                }
                else if (trainBrakeDecrease)
                {
                    brakeKey = Symbols.ArrowDown + ColorCode[Color.Yellow];
                    trainBrakeDecrease = false;
                }
                else
                    brakeKey = "";

                brakeInfoValue = trainBrakeStatus.Substring(0, trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("EQ"))).TrimEnd();
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Train brake"),
                    LastCol = $"{brakeInfoValue}{ColorCode[Color.Cyan]}",
                    KeyPressed = brakeKey,
                    SymbolCol = brakeKey,
                });

                index = trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("EQ"));
                brakeInfoValue = trainBrakeStatus.Substring(index, trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("BC")) - index).TrimEnd();

                AddLabel(new ListLabel
                {
                    LastCol = brakeInfoValue,
                });
                if (trainBrakeStatus.Contains(Viewer.Catalog.GetString("EOT")))
                {
                    int indexOffset = Viewer.Catalog.GetString("EOT").Length + 1;
                    index = trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"));
                    brakeInfoValue = trainBrakeStatus.Substring(index, trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("EOT")) - index).TrimEnd();
                    AddLabel(new ListLabel
                    {
                        LastCol = brakeInfoValue,
                    });
                    index = trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("EOT")) + indexOffset;
                    brakeInfoValue = trainBrakeStatus.Substring(index, trainBrakeStatus.Length - index).TrimStart();
                    AddLabel(new ListLabel
                    {
                        LastCol = brakeInfoValue,
                    });
                }
                else
                {
                    index = trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"));
                    brakeInfoValue = trainBrakeStatus.Substring(index, trainBrakeStatus.Length - index).TrimEnd();
                    AddLabel(new ListLabel
                    {
                        LastCol = brakeInfoValue,
                    });
                }
            }
            else if (trainBrakeStatus.Contains(Viewer.Catalog.GetString("Lead")))
            {
                int indexOffset = Viewer.Catalog.GetString("Lead").Length + 1;
                brakeInfoValue = trainBrakeStatus.Substring(0, trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("Lead"))).TrimEnd();
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Train brake"),
                    LastCol = $"{brakeInfoValue}{ColorCode[Color.Cyan]}",
                });

                index = trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("Lead")) + indexOffset;
                if (trainBrakeStatus.Contains(Viewer.Catalog.GetString("EOT")))
                {
                    brakeInfoValue = trainBrakeStatus.Substring(index, trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("EOT")) - index).TrimEnd();
                    AddLabel(new ListLabel
                    {
                        LastCol = brakeInfoValue,
                    });

                    index = trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("EOT")) + indexOffset;
                    brakeInfoValue = trainBrakeStatus.Substring(index, trainBrakeStatus.Length - index).TrimEnd();
                    AddLabel(new ListLabel
                    {
                        LastCol = brakeInfoValue,
                    });
                }
                else
                {
                    brakeInfoValue = trainBrakeStatus.Substring(index, trainBrakeStatus.Length - index).TrimEnd();
                    AddLabel(new ListLabel
                    {
                        LastCol = brakeInfoValue,
                    });
                }
            }
            else if (trainBrakeStatus.Contains(Viewer.Catalog.GetString("BC")))
            {
                brakeInfoValue = trainBrakeStatus.Substring(0, trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"))).TrimEnd();
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Train brake"),
                    LastCol = $"{brakeInfoValue}{ColorCode[Color.Cyan]}",
                });

                index = trainBrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"));
                brakeInfoValue = trainBrakeStatus.Substring(index, trainBrakeStatus.Length - index).TrimEnd();

                AddLabel(new ListLabel
                {
                    LastCol = brakeInfoValue,
                });
            }

            if (showRetainers)
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Retainers"),
                    LastCol = $"{train.RetainerPercent} {Viewer.Catalog.GetString(train.RetainerSetting.GetDescription())}",
                });
            }

            if (engineBrakeStatus.Contains(Viewer.Catalog.GetString("BC")))
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Engine brake"),
                    LastCol = engineBrakeStatus.Substring(0, engineBrakeStatus.IndexOf("BC")) + ColorCode[Color.Cyan],
                });
                index = engineBrakeStatus.IndexOf(Viewer.Catalog.GetString("BC"));
                brakeInfoValue = engineBrakeStatus.Substring(index, engineBrakeStatus.Length - index).TrimEnd();
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString(""),
                    LastCol = $"{brakeInfoValue}{ColorCode[Color.White]}",
                });
            }
            else
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Engine brake"),
                    LastCol = $"{engineBrakeStatus}{ColorCode[Color.Cyan]}",
                });
            }

            if (dynamicBrakeStatus != null && locomotive.IsLeadLocomotive())
            {
                if (locomotive.DynamicBrakePercent >= 0)
                {
                    AddLabel(new ListLabel
                    {
                        FirstCol = Viewer.Catalog.GetString("Dynamic brake"),
                        LastCol = locomotive.DynamicBrake ? dynamicBrakeStatus : Viewer.Catalog.GetString("Setup") + ColorCode[Color.Cyan],
                    });
                }
                else
                {
                    AddLabel(new ListLabel
                    {
                        FirstCol = Viewer.Catalog.GetString("Dynamic brake"),
                        LastCol = Viewer.Catalog.GetString("Off"),
                    });
                }
            }

            AddSeparator();

            if (locomotiveStatus != null)
            {
                foreach (string data in locomotiveStatus.Split('\n').Where((string d) => d.Length > 0))
                {
                    string[] parts = data.Split(new string[] { " = " }, 2, StringSplitOptions.None);
                    string keyPart = parts[0];
                    string valuePart = parts?[1];
                    if (Viewer.Catalog.GetString(keyPart).StartsWith(Viewer.Catalog.GetString("Boiler pressure")))
                    {
                        MSTSSteamLocomotive steamLocomotive2 = (MSTSSteamLocomotive)locomotive;
                        float bandUpper = steamLocomotive2.PreviousBoilerHeatOutBTUpS * 1.025f; // find upper bandwidth point
                        float bandLower = steamLocomotive2.PreviousBoilerHeatOutBTUpS * 0.975f; // find lower bandwidth point - gives a total 5% bandwidth

                        string heatIndicator;
                        if (steamLocomotive2.BoilerHeatInBTUpS > bandLower && steamLocomotive2.BoilerHeatInBTUpS < bandUpper)
                            heatIndicator = $"{Symbols.SmallDiamond}{ColorCode[Color.White]}";
                        else if (steamLocomotive2.BoilerHeatInBTUpS < bandLower)
                            heatIndicator = $"{Symbols.SmallArrowDown}{ColorCode[Color.Cyan]}";
                        else if (steamLocomotive2.BoilerHeatInBTUpS > bandUpper)
                            heatIndicator = $"{Symbols.SmallArrowUp}{ColorCode[Color.Orange]}";
                        else
                            heatIndicator = ColorCode[Color.White];

                        AddLabel(new ListLabel
                        {
                            FirstCol = Viewer.Catalog.GetString("Boiler pressure"),
                            LastCol = Viewer.Catalog.GetString(valuePart),
                            SymbolCol = heatIndicator,
                        });
                    }
                    else if (!normalTextMode && Viewer.Catalog.GetString(parts[0]).StartsWith(Viewer.Catalog.GetString("Fuel levels")))
                    {
                        AddLabel(new ListLabel
                        {
                            FirstCol = keyPart.EndsWith("?") || keyPart.EndsWith("!") ? Viewer.Catalog.GetString(keyPart.Substring(0, keyPart.Length - 3)) : Viewer.Catalog.GetString(keyPart),
                            LastCol = valuePart.Length > 1 ? Viewer.Catalog.GetString(valuePart.Replace(" ", string.Empty)) : "",
                        });
                    }
                    else if (keyPart.StartsWith(Viewer.Catalog.GetString("Gear")))
                    {
                        string gearKey;
                        if (gearUp)
                        {
                            gearKey = Symbols.ArrowUp + ColorCode[Color.Yellow];
                            gearUp = false;
                        }
                        else if (gearDown)
                        {
                            gearKey = Symbols.ArrowDown + ColorCode[Color.Yellow];
                            gearDown = false;
                        }
                        else
                            gearKey = "";

                        AddLabel(new ListLabel
                        {
                            FirstCol = Viewer.Catalog.GetString(keyPart),
                            LastCol = valuePart != null ? Viewer.Catalog.GetString(valuePart) : "",
                            KeyPressed = gearKey,
                            SymbolCol = gearKey,
                        });
                    }
                    else if (parts.Contains(Viewer.Catalog.GetString("Pantographs")))
                    {
                        string pantoKey;
                        if (pantographKey)
                        {
                            string arrow = parts[1].StartsWith(Viewer.Catalog.GetString("Up")) ? Symbols.ArrowUp : Symbols.ArrowDown;
                            pantoKey = arrow + ColorCode[Color.Yellow];
                            pantographKey = false;
                        }
                        else
                            pantoKey = "";

                        AddLabel(new ListLabel
                        {
                            FirstCol = Viewer.Catalog.GetString(keyPart),
                            LastCol = valuePart != null ? Viewer.Catalog.GetString(valuePart) : "",
                            KeyPressed = pantoKey,
                            SymbolCol = pantoKey,
                        });
                    }
                    else if (parts.Contains(Viewer.Catalog.GetString("Engine")))
                    {
                        AddLabel(new ListLabel
                        {
                            FirstCol = Viewer.Catalog.GetString(keyPart),
                            LastCol = valuePart != null ? $"{Viewer.Catalog.GetString(valuePart)}{ColorCode[Color.White]}" : "",
                        });
                    }
                    else
                    {
                        AddLabel(new ListLabel
                        {
                            FirstCol = keyPart.EndsWith("?") || keyPart.EndsWith("!") ? Viewer.Catalog.GetString(keyPart.Substring(0, keyPart.Length - 3)) : Viewer.Catalog.GetString(keyPart),
                            LastCol = valuePart != null ? Viewer.Catalog.GetString(valuePart) : "",
                        });
                    }
                }
            }

            AddSeparator();

            if (normalTextMode)
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("FPS"),
                    LastCol = $"{Math.Floor(viewer.RenderProcess.FrameRate.SmoothedValue)}",
                });
            }

            // Messages
            // Autopilot
            bool autopilot = locomotive.Train.TrainType == TrainType.AiPlayerHosting;
            AddLabel(new ListLabel
            {
                FirstCol = Viewer.Catalog.GetString("Autopilot"),
                LastCol = autopilot ? Viewer.Catalog.GetString("On") + ColorCode[Color.Yellow] : Viewer.Catalog.GetString("Off"),
            });

            // Grate limit
            if (locomotive is MSTSSteamLocomotive steamLocomotive1)
            {
                if (steamLocomotive1.IsGrateLimit && steamLocomotive1.GrateCombustionRateLBpFt2 > steamLocomotive1.GrateLimitLBpFt2)
                {
                    AddLabel(new ListLabel
                    {
                        FirstCol = Viewer.Catalog.GetString("Grate limit"),
                        LastCol = Viewer.Catalog.GetString("Exceeded") + ColorCode[Color.OrangeRed],
                    });
                }
                else
                {
                    AddLabel(new ListLabel
                    {
                        FirstCol = Viewer.Catalog.GetString("Grate limit") + ColorCode[Color.Black],
                        LastCol = Viewer.Catalog.GetString("Normal") + ColorCode[Color.Black],
                    });
                }
            }
            else
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Grate limit") + ColorCode[Color.Black],
                    LastCol = Viewer.Catalog.GetString("-") + ColorCode[Color.Black],
                });
            }

            // Wheel
            if (train.IsWheelSlip)
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Wheel"),
                    LastCol = Viewer.Catalog.GetString("slip") + ColorCode[Color.OrangeRed],
                });
            }
            else if (train.IsWheelSlipWarninq)
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Wheel"),
                    LastCol = Viewer.Catalog.GetString("slip warning") + ColorCode[Color.Yellow],
                });
            }
            else if (train.IsBrakeSkid)
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Wheel"),
                    LastCol = Viewer.Catalog.GetString("skid") + ColorCode[Color.OrangeRed],
                });
            }
            else
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Wheel") + ColorCode[Color.Black],
                    LastCol = Viewer.Catalog.GetString("Normal") + ColorCode[Color.Black],
                });
            }

            // Doors
            var wagon = (MSTSWagon)locomotive;
            if (wagon.DoorLeftOpen || wagon.DoorRightOpen)
            {
                var status = new List<string>();
                bool flipped = locomotive.GetCabFlipped();
                if (wagon.DoorLeftOpen)
                    status.Add(Viewer.Catalog.GetString(Viewer.Catalog.GetString(flipped ? "Right" : "Left")));
                if (wagon.DoorRightOpen)
                    status.Add(Viewer.Catalog.GetString(Viewer.Catalog.GetString(flipped ? "Left" : "Right")));

                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Doors open"),
                    LastCol = string.Join(" ", status) + ColorCode[locomotive.AbsSpeedMpS > 0.1f ? Color.OrangeRed : Color.Yellow],
                });
            }
            else
            {
                AddLabel(new ListLabel
                {
                    FirstCol = Viewer.Catalog.GetString("Doors open") + ColorCode[Color.Black],
                    LastCol = Viewer.Catalog.GetString("Closed") + ColorCode[Color.Black],
                });
            }

            AddLabel(new ListLabel());
            return labels;
        }

        private static string Round(float x) => $"{Math.Round(x):F0}";
    }
}
