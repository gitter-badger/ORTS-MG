﻿// COPYRIGHT 2010, 2011, 2012, 2013 by the Open Rails project.
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
using System.IO;

using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts.Parsers;
using Orts.Scripting.Api;
using Orts.Simulation.AIs;

namespace Orts.Simulation.RollingStocks.SubSystems.Controllers
{
    public class ScriptedBrakeController : IController
    {
        readonly MSTSLocomotive Locomotive;
        readonly Simulator Simulator;

        private readonly bool activated;
        private string scriptName = "MSTS";
        BrakeController Script;
        public List<INotchController> Notches { get; } = new List<INotchController>();

        private bool emergencyBrakingPushButton;
        private bool tcsEmergencyBraking;
        private bool tcsFullServiceBraking;
        private bool overchargeButtonPressed;
        private bool quickReleaseButtonPressed;

        public bool EmergencyBraking
        {
            get
            {
                return emergencyBrakingPushButton || tcsEmergencyBraking || (Script.GetState() == ControllerState.Emergency);
            }
        }

        public bool EmergencyBrakingPushButton
        {
            get
            {
                return emergencyBrakingPushButton;
            }
            set
            {
                if (Simulator.Confirmer != null)
                {
                    if (value && !emergencyBrakingPushButton && !tcsEmergencyBraking)
                        Simulator.Confirmer.Confirm(CabControl.EmergencyBrake, CabSetting.On);
                    else if (!value && emergencyBrakingPushButton && !tcsEmergencyBraking)
                        Simulator.Confirmer.Confirm(CabControl.EmergencyBrake, CabSetting.Off);
                }

                emergencyBrakingPushButton = value;
            }
        }

        public bool TCSEmergencyBraking
        {
            get
            {
                return tcsEmergencyBraking;
            }
            set
            {
                if (Simulator.Confirmer != null)
                {
                    if (value && !emergencyBrakingPushButton && !tcsEmergencyBraking)
                        Simulator.Confirmer.Confirm(CabControl.EmergencyBrake, CabSetting.On);
                    else if (!value && !emergencyBrakingPushButton && tcsEmergencyBraking)
                        Simulator.Confirmer.Confirm(CabControl.EmergencyBrake, CabSetting.Off);
                }

                tcsEmergencyBraking = value;
            }
        }

        public bool TCSFullServiceBraking
        {
            get
            {
                return tcsFullServiceBraking;
            }
            set
            {
                if (Simulator.Confirmer != null)
                {
                    if (value && !tcsFullServiceBraking)
                        Simulator.Confirmer.Confirm(CabControl.TrainBrake, CabSetting.On);
                }

                tcsFullServiceBraking = value;
            }
        }

        public bool QuickReleaseButtonPressed
        {
            get
            {
                return quickReleaseButtonPressed;
            }
            set
            {
                if (Simulator.Confirmer != null)
                {
                    if (value && !quickReleaseButtonPressed)
                        Simulator.Confirmer.Confirm(CabControl.QuickRelease, CabSetting.On);
                    else if (!value && quickReleaseButtonPressed)
                        Simulator.Confirmer.Confirm(CabControl.QuickRelease, CabSetting.Off);
                }

                quickReleaseButtonPressed = value;
            }
        }

        public bool OverchargeButtonPressed
        {
            get
            {
                return overchargeButtonPressed;
            }
            set
            {
                if (Simulator.Confirmer != null)
                {
                    if (value && !overchargeButtonPressed)
                        Simulator.Confirmer.Confirm(CabControl.Overcharge, CabSetting.On);
                    else if (!value && overchargeButtonPressed)
                        Simulator.Confirmer.Confirm(CabControl.Overcharge, CabSetting.Off);
                }

                overchargeButtonPressed = value;
            }
        }

        public float MaxPressurePSI { get; set; }
        public float MaxOverchargePressurePSI { get; private set; }
        public float ReleaseRatePSIpS { get; private set; }
        public float QuickReleaseRatePSIpS { get; private set; }
        public float OverchargeEliminationRatePSIpS { get; private set; }
        public float SlowApplicationRatePSIpS { get; private set; }
        public float ApplyRatePSIpS { get; private set; }
        public float EmergencyRatePSIpS { get; private set; }
        public float FullServReductionPSI { get; private set; }
        public float MinReductionPSI { get; private set; }

        /// <summary>
        /// Needed for proper mouse operation in the cabview
        /// </summary>
        public float IntermediateValue { get { return Script is MSTSBrakeController ? (Script as MSTSBrakeController).NotchController.IntermediateValue : CurrentValue; } }

        /// <summary>
        /// Knowing actual notch and its change is needed for proper repeatability of mouse and RailDriver operation
        /// </summary>
        public int CurrentNotch { get { return Script is MSTSBrakeController ? (Script as MSTSBrakeController).NotchController.CurrentNotch : 0; } set { } }

        public ControllerState TrainBrakeControllerState
        {
            get
            {
                if (Script is MSTSBrakeController)
                    return Notches.Count > 0 ? Notches[CurrentNotch].NotchStateType : ControllerState.Dummy;
                else
                    return Script.GetState();
            }
        }

        private float oldValue;

        public float CurrentValue { get; set; }
        public float MinimumValue { get; set; }
        public float MaximumValue { get; set; }
        public float StepSize { get; set; }
        public float UpdateValue { get; set; }
        public double CommandStartTime { get; set; }

        public ScriptedBrakeController(MSTSLocomotive locomotive)
        {
            Simulator = locomotive.Simulator;
            Locomotive = locomotive;

            MaxPressurePSI = 90;
            MaxOverchargePressurePSI = 95;
            ReleaseRatePSIpS = 5;
            QuickReleaseRatePSIpS = 10;
            OverchargeEliminationRatePSIpS = 0.036f;
            ApplyRatePSIpS = 2;
            SlowApplicationRatePSIpS = 1;
            EmergencyRatePSIpS = 10;
            FullServReductionPSI = 26;
            MinReductionPSI = 6;
        }

        public ScriptedBrakeController(ScriptedBrakeController controller, MSTSLocomotive locomotive)
        {
            Simulator = locomotive.Simulator;
            Locomotive = locomotive;

            scriptName = controller.scriptName;
            MaxPressurePSI = controller.MaxPressurePSI;
            MaxOverchargePressurePSI = controller.MaxOverchargePressurePSI;
            ReleaseRatePSIpS = controller.ReleaseRatePSIpS;
            QuickReleaseRatePSIpS = controller.QuickReleaseRatePSIpS;
            OverchargeEliminationRatePSIpS = controller.OverchargeEliminationRatePSIpS;
            ApplyRatePSIpS = controller.ApplyRatePSIpS;
            SlowApplicationRatePSIpS = controller.SlowApplicationRatePSIpS;
            EmergencyRatePSIpS = controller.EmergencyRatePSIpS;
            FullServReductionPSI = controller.FullServReductionPSI;
            MinReductionPSI = controller.MinReductionPSI;

            CurrentValue = controller.CurrentValue;
            MinimumValue = controller.MinimumValue;
            MaximumValue = controller.MaximumValue;
            StepSize = controller.StepSize;

            controller.Notches.ForEach(
                (item) => { Notches.Add(new MSTSNotch(item)); }
            );

            Initialize();
        }

        public ScriptedBrakeController Clone(MSTSLocomotive locomotive)
        {
            return new ScriptedBrakeController(this, locomotive);
        }

        public void Parse(STFReader stf)
        {
            Parse(stf.Tree.ToLower(), stf);
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(trainbrakescontrollermaxsystempressure":
                case "engine(enginebrakescontrollermaxsystempressure":
                    MaxPressurePSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null);
                    break;

                case "engine(ortstrainbrakescontrollermaxoverchargepressure":
                    MaxOverchargePressurePSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null);
                    break;

                case "engine(trainbrakescontrollermaxreleaserate":
                case "engine(enginebrakescontrollermaxreleaserate":    
                    ReleaseRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(trainbrakescontrollermaxquickreleaserate":
                case "engine(enginebrakescontrollermaxquickreleaserate":
                    QuickReleaseRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(ortstrainbrakescontrolleroverchargeeliminationrate":
                    OverchargeEliminationRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(trainbrakescontrollermaxapplicationrate":
                case "engine(enginebrakescontrollermaxapplicationrate":
                    ApplyRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(trainbrakescontrolleremergencyapplicationrate":
                case "engine(enginebrakescontrolleremergencyapplicationrate":
                    EmergencyRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(trainbrakescontrollerfullservicepressuredrop":
                case "engine(enginebrakescontrollerfullservicepressuredrop":
                    FullServReductionPSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null);
                    break;

                case "engine(trainbrakescontrollerminpressurereduction":
                case "engine(enginebrakescontrollerminpressurereduction":
                    MinReductionPSI = stf.ReadFloatBlock(STFReader.Units.PressureDefaultPSI, null);
                    break;

                case "engine(ortstrainbrakescontrollerslowapplicationrate":
                case "engine(ortsenginebrakescontrollerslowapplicationrate":
                    SlowApplicationRatePSIpS = stf.ReadFloatBlock(STFReader.Units.PressureRateDefaultPSIpS, null);
                    break;

                case "engine(enginecontrollers(brake_train":
                case "engine(enginecontrollers(brake_engine":
                case "engine(enginecontrollers(brake_brakeman":
                    stf.MustMatch("(");
                    MinimumValue = stf.ReadFloat(STFReader.Units.None, null);
                    MaximumValue = stf.ReadFloat(STFReader.Units.None, null);
                    StepSize = stf.ReadFloat(STFReader.Units.None, null);
                    CurrentValue = stf.ReadFloat(STFReader.Units.None, null);
                    string token = stf.ReadItem(); // s/b numnotches
                    if (string.Compare(token, "NumNotches", true) != 0) // handle error in gp38.eng where extra parameter provided before NumNotches statement 
                        stf.ReadItem();
                    stf.MustMatch("(");
                    stf.ReadInt(null);
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("notch", ()=>{
                            stf.MustMatch("(");
                            float value = stf.ReadFloat(STFReader.Units.None, null);
                            int smooth = stf.ReadInt(null);
                            string type = stf.ReadString();
                            Notches.Add(new MSTSNotch(value, smooth, type, stf));
                            if (type != ")") stf.SkipRestOfBlock();
                        }),
                    });
                    break;

                case "engine(ortstrainbrakecontroller":
                case "engine(ortsenginebrakecontroller":
                    if (Locomotive.Train as AITrain == null)
                    {
                        scriptName = stf.ReadStringBlock(null);
                    }
                    break;
            }
        }

        public void Initialize()
        {
            if (!activated)
            {
                if (scriptName != null && scriptName != "MSTS")
                {
                    Script = Simulator.ScriptManager.Load(Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script"), scriptName) as BrakeController;
                }
                if (Script == null)
                {
                    Script = new MSTSBrakeController() as BrakeController;
                    (Script as MSTSBrakeController).ForceControllerReleaseGraduated = Simulator.Settings.GraduatedRelease;
                }

                // AbstractScriptClass
                Script.ClockTime = () => (float)Simulator.ClockTime;
                Script.GameTime = () => (float)Simulator.GameTime;
                Script.DistanceM = () => Locomotive.DistanceM;
                Script.SpeedMpS = () => Math.Abs(Locomotive.SpeedMpS);

                // BrakeController
                Script.EmergencyBrakingPushButton = () => EmergencyBrakingPushButton;
                Script.TCSEmergencyBraking = () => TCSEmergencyBraking;
                Script.TCSFullServiceBraking = () => TCSFullServiceBraking;
                Script.QuickReleaseButtonPressed = () => QuickReleaseButtonPressed;
                Script.OverchargeButtonPressed = () => OverchargeButtonPressed;

                Script.MainReservoirPressureBar = () =>
                {
                    if (Locomotive.Train != null)
                        return (float)Pressure.Atmospheric.FromPSI(Locomotive.Train.BrakeLine2PressurePSI);
                    else
                        return float.MaxValue;
                };
                Script.MaxPressureBar = () => (float)Pressure.Atmospheric.FromPSI(MaxPressurePSI);
                Script.MaxOverchargePressureBar = () => (float)Pressure.Atmospheric.FromPSI(MaxOverchargePressurePSI);
                Script.ReleaseRateBarpS = () => (float)Rate.Pressure.FromPSIpS(ReleaseRatePSIpS);
                Script.QuickReleaseRateBarpS = () => (float)Rate.Pressure.FromPSIpS(QuickReleaseRatePSIpS);
                Script.OverchargeEliminationRateBarpS = () => (float)Rate.Pressure.FromPSIpS(OverchargeEliminationRatePSIpS);
                Script.SlowApplicationRateBarpS = () => (float)Rate.Pressure.FromPSIpS(SlowApplicationRatePSIpS);
                Script.ApplyRateBarpS = () => (float)Rate.Pressure.FromPSIpS(ApplyRatePSIpS);
                Script.EmergencyRateBarpS = () => (float)Rate.Pressure.FromPSIpS(EmergencyRatePSIpS);
                Script.FullServReductionBar = () => (float)Pressure.Atmospheric.FromPSI(FullServReductionPSI);
                Script.MinReductionBar = () => (float)Pressure.Atmospheric.FromPSI(MinReductionPSI);
                Script.CurrentValue = () => CurrentValue;
                Script.MinimumValue = () => MinimumValue;
                Script.MaximumValue = () => MaximumValue;
                Script.StepSize = () => StepSize;
                Script.UpdateValue = () => UpdateValue;
                Script.Notches = () => Notches;

                Script.SetCurrentValue = (value) => CurrentValue = value;
                Script.SetUpdateValue = (value) => UpdateValue = value;

                Script.SetDynamicBrakeIntervention = (value) =>
                {
                    // TODO: Set dynamic brake intervention instead of controller position
                    // There are some issues that need to be identified and fixed before setting the intervention directly
                    if (Locomotive.DynamicBrakeController == null) return;
                    Locomotive.DynamicBrakeChangeActiveState(value > 0);
                    Locomotive.DynamicBrakeController.SetValue(value);
                };

                Script.Initialize();
            }
        }

        public void InitializeMoving()
        {
            Script.InitializeMoving();
        }

        public float Update(double elapsedClockSeconds)
        {
            return Script?.Update(elapsedClockSeconds) ?? 0;
        }

        public (double pressurePSI, double epPressureBar) UpdatePressure(double pressurePSI, double epPressureBar, double elapsedClockSeconds)
        {
            if (Script != null)
            {
                // Conversion is needed until the pressures of the brake system are converted to Pressure.Atmospheric.
                double pressureBar = Pressure.Atmospheric.FromPSI(pressurePSI);
                var result = Script.UpdatePressure(pressureBar, epPressureBar, elapsedClockSeconds);
                pressurePSI = Pressure.Atmospheric.ToPSI(result.Item1);
                epPressureBar = result.Item2;
            }
            return (pressurePSI, epPressureBar);
        }

        public double UpdateEngineBrakePressure(double pressurePSI, double elapsedClockSeconds)
        {
            if (Script != null)
            {
                // Conversion is needed until the pressures of the brake system are converted to Pressure.Atmospheric.
                double pressureBar = Pressure.Atmospheric.FromPSI(pressurePSI);
                pressureBar = Script.UpdateEngineBrakePressure(pressureBar, elapsedClockSeconds);
                pressurePSI = Pressure.Atmospheric.ToPSI(pressureBar);
            }
            return pressurePSI;
        }

        public void SignalEvent(BrakeControllerEvent evt)
        {
            if (Script != null)
                Script.HandleEvent(evt);
        }

        public void SignalEvent(BrakeControllerEvent evt, float? value)
        {
            if (Script != null)
                Script.HandleEvent(evt, value);
            else
            {
                if (evt == BrakeControllerEvent.SetCurrentValue && value != null)
                {
                    float newValue = value ?? 0F;
                    CurrentValue = newValue;
                }
            }
        }

        public void StartIncrease()
        {
            SignalEvent(BrakeControllerEvent.StartIncrease);
        }

        public void StopIncrease()
        {
            SignalEvent(BrakeControllerEvent.StopIncrease);
        }

        public void StartDecrease()
        {
            SignalEvent(BrakeControllerEvent.StartDecrease);
        }

        public void StopDecrease()
        {
            SignalEvent(BrakeControllerEvent.StopDecrease);
        }

        public void StartIncrease(float? target)
        {
            SignalEvent(BrakeControllerEvent.StartIncrease, target);
        }

        public void StartDecrease(float? target , bool toZero = false)
        {
            if (toZero) SignalEvent(BrakeControllerEvent.StartDecreaseToZero, target);
            else SignalEvent(BrakeControllerEvent.StartDecrease, target);
        }

        public float SetPercent(float percent)
        {
            SignalEvent(BrakeControllerEvent.SetCurrentPercent, percent);
            return CurrentValue;
        }

        public int SetValue(float value)
        {
            var oldNotch = CurrentNotch;
            SignalEvent(BrakeControllerEvent.SetCurrentValue, value);

            var change = CurrentNotch > oldNotch || CurrentValue > oldValue + 0.1f || CurrentValue == 1 && oldValue < 1
                ? 1 : CurrentNotch < oldNotch || CurrentValue < oldValue - 0.1f || CurrentValue == 0 && oldValue > 0 ? -1 : 0;
            if (change != 0)
                oldValue = CurrentValue;
            return change;
        }

        public bool IsValid()
        {
            if (Script != null)
                return Script.IsValid();
            else
                return true;
        }

        public string GetStatus()
        {
            if (Script != null)
            {
                //ToDo translation via catalog
                string state = EnumExtension.GetDescription(Script.GetState());
                string fraction = GetStateFractionScripted();

                if (String.IsNullOrEmpty(state) && String.IsNullOrEmpty(fraction))
                    return String.Empty;
                else if (!String.IsNullOrEmpty(state) && String.IsNullOrEmpty(fraction))
                    return state;
                else if (String.IsNullOrEmpty(state) && !String.IsNullOrEmpty(fraction))
                    return fraction;
                else
                    return String.Format("{0} {1}", state, fraction);
            }
            else
                return String.Empty;
        }

        public string GetStateFractionScripted()
        {
            if (Script != null)
            {
                float? fraction = Script.GetStateFraction();

                if (fraction != null)
                    return String.Format("{0:F0}%", 100 * (fraction ?? 0));
                else
                    return String.Empty;
            }
            else
            {
                return String.Empty;
            }
        }

        public void Save(BinaryWriter outf)
        {
            outf.Write((int)ControllerTypes.BrakeController);

            outf.Write(CurrentValue);

            outf.Write(EmergencyBrakingPushButton);
            outf.Write(TCSEmergencyBraking);
            outf.Write(TCSFullServiceBraking);
        }

        public void Restore(BinaryReader inf)
        {
            SignalEvent(BrakeControllerEvent.SetCurrentValue, inf.ReadSingle());

            EmergencyBrakingPushButton = inf.ReadBoolean();
            TCSEmergencyBraking = inf.ReadBoolean();
            TCSFullServiceBraking = inf.ReadBoolean();
        }
    }
}