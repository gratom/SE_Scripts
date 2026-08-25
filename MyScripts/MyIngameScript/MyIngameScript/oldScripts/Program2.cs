using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Screens.Helpers;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Scripting;
using VRageMath;

namespace IngameScriptCheckDamage
{
    internal partial class Program : MyGridProgram
    {

        private List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        private IMyTextPanel screen;
        public Program()
        {
            Init();
        }
        public void Init()
        {
            GridTerminalSystem.GetBlocks(blocks);
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("MiningDisp3");
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            string s = "";
            for (int i = 0; i < blocks.Count; i++)
            {
                if (!blocks[i].IsFunctional)
                {
                    s += "Block:" + blocks[i].Name + ";" + blocks[i].CustomName + ";pos:" + blocks[i].Position + "\n";
                }
            }
            if (s == "")
            {
                screen.FontColor = Color.Green;
                screen.WriteText("Damaged blocks :\nnot found...");
            }
            else
            {
                screen.FontColor = Color.Red;
                screen.WriteText("Damaged blocks :\n" + s);
            }
        }

    }
}

namespace IngameScriptMiningCounter
{
    internal partial class Program : MyGridProgram
    {
        private enum TankType
        {
            oxy,
            hydro,
            none
        }

        private IMyTextPanel screen;
        private IMyTextPanel screen2;
        private IMyTextPanel screen3;
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        private List<IMyThrust> engines = new List<IMyThrust>();
        private List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        private List<IMyGasTank> gasOxy = new List<IMyGasTank>();
        private List<IMyGasTank> gasHydro = new List<IMyGasTank>();
        private IMyAirVent vent;

        private double prevCurrentHydro = 0;

        public Program()
        {
            Init();
        }

        public void Init()
        {
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("MiningDisp4");
            screen2 = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("MiningDisp2");
            screen3 = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("MiningDisp1");
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(engines);
            engines.RemoveAll(x => !x.CustomName.Contains("MiningEngineUp"));

            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
            batteries.RemoveAll(x => !x.CustomName.Contains("MiningBattery"));

            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers);
            containers.RemoveAll(x => !x.CustomName.Contains("MiningContainer"));

            List<IMyGasTank> gas = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(gas);
            gas.RemoveAll(x => !x.CustomName.Contains("Mining"));
            for (int i = 0; i < gas.Count; i++)
            {
                if (GetTankType(gas[i]) == TankType.oxy)
                {
                    gasOxy.Add(gas[i]);
                }
                if (GetTankType(gas[i]) == TankType.hydro)
                {
                    gasHydro.Add(gas[i]);
                }
            }
            vent = (IMyAirVent)GridTerminalSystem.GetBlockWithName("ventMining");

            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }
        private TankType GetTankType(IMyTerminalBlock theBlock)
        {
            if (theBlock is IMyGasTank)
            {
                if (theBlock.BlockDefinition.SubtypeId.Contains("Hydro"))
                {
                    return TankType.hydro;
                }
                else
                {
                    return TankType.oxy;
                }
            }
            return TankType.none;
        }
        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "r")
            {
                Init();
            }

            double volumeSum = 0;
            double volumeMax = 0;

            Dictionary<string, double> others = new Dictionary<string, double>();

            for (int i = 0; i < containers.Count; i++)
            {
                IMyInventory invent = containers[i].GetInventory();
                volumeMax += (double)invent.MaxVolume;
                volumeSum += (double)invent.CurrentVolume;

                List<MyInventoryItem> inventoryItem = new List<MyInventoryItem>();
                invent.GetItems(inventoryItem);
                for (int j = 0; j < inventoryItem.Count; j++)
                {
                    if (inventoryItem[j].Type.TypeId.Contains("Component"))
                    {

                    }
                    else
                    {
                        if (!others.ContainsKey(inventoryItem[j].Type.SubtypeId))
                        {
                            others.Add(inventoryItem[j].Type.SubtypeId, 0);
                        }
                        others[inventoryItem[j].Type.SubtypeId] += (double)inventoryItem[j].Amount;
                    }
                }
            }

            string strVolume = "Volume absolute : " + ValueToString(volumeSum * 1000) + "l / " + ValueToString(volumeMax * 1000) + "l";
            string strVolumePersent = "Volume percent : " + (volumeSum / volumeMax * 100).ToString("0.0") + "%";

            string OtherString = "Ores:\n";
            foreach (KeyValuePair<string, double> item in others)
            {
                OtherString += item.Key + " : " + CountToString(item.Value) + "\n";
            }

            float sumEnginesCur = 0;
            float sumEnginesMax = 0;

            for (int i = 0; i < engines.Count; i++)
            {
                sumEnginesCur += engines[i].CurrentThrust;
                sumEnginesMax += engines[i].MaxEffectiveThrust;
            }

            double Sum = 0;
            double Max = 0;
            double EnPlus = 0;
            double EnMinus = 0;

            for (int i = 0; i < batteries.Count; i++)
            {
                Max += batteries[i].MaxStoredPower;
                Sum += batteries[i].CurrentStoredPower;
                EnPlus += batteries[i].CurrentInput;
                EnMinus += batteries[i].CurrentOutput;
            }

            string strEn1 = "Energy : " + CountToString(Sum * 1000000) + "wh / " + CountToString(Max * 1000000) + "wh";
            string steEnPers = "Energy percent : " + (Sum / Max * 100).ToString("0.0") + "%";
            string InOut = "in : +" + CountToString(EnPlus * 1000000) + "wh" +
                           "\nout : -" + CountToString(EnMinus * 1000000) + "wh" +
                           "\ntotal : " + CountToString((EnPlus - EnMinus) * 1000000) + "wh";
            double time = (Max - Sum) / (EnPlus - EnMinus);
            long timeTicks = (long)(time * 3600 * 10000000);
            TimeSpan timeSpan = new TimeSpan(timeTicks);

            if (time > 0)
            {
                InOut += "\ntime to charge : " + timeSpan.ToString(@"dd\.hh\:mm\:ss");
            }
            else
            {
                InOut += "\ntime to discharge : " + timeSpan.ToString(@"dd\.hh\:mm\:ss");
            }

            double OxyCapacity = 0;
            double OxySum = 0;
            for (int i = 0; i < gasOxy.Count; i++)
            {
                OxyCapacity += gasOxy[i].Capacity;
                OxySum += gasOxy[i].FilledRatio * gasOxy[i].Capacity;
            }
            string oxyState = "oxygen : " + CountToString(OxySum) + "/" + CountToString(OxyCapacity) +
                              "\nin tanks : " + (OxySum / OxyCapacity * 100).ToString("0.00") +
                              "%\nin room : " + (vent.GetOxygenLevel() * 100).ToString("0.0000") + "%";

            double HydroCapacity = 0;
            double HydroSum = 0;
            for (int i = 0; i < gasHydro.Count; i++)
            {
                HydroCapacity += gasHydro[i].Capacity;
                HydroSum += gasHydro[i].FilledRatio * gasHydro[i].Capacity;
            }

            string chardgeState = "";
            if (prevCurrentHydro <= HydroSum)
            {
                double plusHydroPerSecond = Math.Abs(prevCurrentHydro - HydroSum) * 6;
                double hydroTime = (HydroCapacity - HydroSum) / plusHydroPerSecond;
                long timeHydroTicks = (long)(hydroTime * 10000000);
                TimeSpan timeHydroSpan = new TimeSpan(timeHydroTicks);

                chardgeState = "\nHydro charging in : " + timeHydroSpan.ToString(@"dd\.hh\:mm\:ss");
            }
            else
            {
                double minusHydroPerSecond = Math.Abs(prevCurrentHydro - HydroSum) * 6;
                double hydroTime = HydroSum / minusHydroPerSecond;
                long timeHydroTicks = (long)(hydroTime * 10000000);
                TimeSpan timeHydroSpan = new TimeSpan(timeHydroTicks);

                chardgeState = "\nHydro will end in : " + timeHydroSpan.ToString(@"dd\.hh\:mm\:ss");
            }
            prevCurrentHydro = HydroSum;

            string hydroState = "hydro : " + CountToString(HydroSum) + "/" + CountToString(HydroCapacity) +
                                "\nin tanks : " + (HydroSum / HydroCapacity * 100).ToString("0.00") + "%" + chardgeState;

            screen3.WriteText(OtherString);
            screen2.WriteText(oxyState);
            screen.WriteText(strVolume +
                             "\n" + strVolumePersent +
                             "\nEngines : " + (sumEnginesCur / sumEnginesMax * 100).ToString("0.0") + "%" +
                             "\n" + hydroState +
                             "\n\n" + strEn1 + "\n" + steEnPers + "\n" + InOut);
        }

        private string ValueToString(double count)
        {
            if (count >= 1000000000)
            {
                return (count / 1000000000).ToString("0.0") + "G";
            }

            if (count >= 1000000)
            {
                return (count / 1000000).ToString("0.0") + "M";
            }

            if (count >= 1000)
            {
                return (count / 1000).ToString("0.0") + "k";
            }

            return count.ToString("0.0");
        }

        private string CountToString(double count)
        {
            if (count >= 1000000000)
            {
                return (count / 1000000000).ToString("0.0") + "B";
            }

            if (count >= 1000000)
            {
                return (count / 1000000).ToString("0.0") + "M";
            }

            if (count >= 1000)
            {
                return (count / 1000).ToString("0.0") + "k";
            }

            return count.ToString("0.0");
        }


    }
}

namespace IngameScriptAutoPilot
{

}

namespace IngameScript2.ServerScripts2
{
    internal partial class Program : MyGridProgram
    {

        private IMyTextPanel screen;
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        private List<IMyShipDrill> drills = new List<IMyShipDrill>();
        private List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
        private IMyCockpit cockpit;
        private int screenIndex1 = 4;

        public Program()
        {
            Init();
        }

        private void Init()
        {
            GridTerminalSystem.GetBlocks(blocks);
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers);
            GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(drills);
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("screen1");
            cockpit = (IMyCockpit)GridTerminalSystem.GetBlockWithName("Cock1");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Update(argument);
        }

        public void Update(string arg)
        {
            if (arg == "r")
            {
                Init();
            }
            double volumeSum = 0;
            double volumeMax = 0;
            for (int i = 0; i < containers.Count; i++)
            {
                IMyInventory invent = containers[i].GetInventory();
                volumeMax += (double)invent.MaxVolume;
                volumeSum += (double)invent.CurrentVolume;
            }
            for (int i = 0; i < drills.Count; i++)
            {
                IMyInventory invent = drills[i].GetInventory();
                volumeMax += (double)invent.MaxVolume;
                volumeSum += (double)invent.CurrentVolume;
            }
            string strVolumeCargoPersent = "Cargo : " + (volumeSum / volumeMax * 100).ToString("0.0") + "%";

            string s = "";
            for (int i = 0; i < blocks.Count; i++)
            {
                if (!blocks[i].IsFunctional)
                {
                    s += "Block:" + blocks[i].Name + ";" + blocks[i].CustomName + ";pos:" + blocks[i].Position + "\n";
                }
            }
            if (s == "")
            {
                screen.FontColor = Color.Green;
                cockpit.GetSurface(screenIndex1).WriteText("Damaged blocks :\nnot found...");
            }
            else
            {
                screen.FontColor = Color.Red;
                cockpit.GetSurface(screenIndex1).WriteText("Damaged blocks :\n" + s);
            }

            screen.WriteText(strVolumeCargoPersent);
        }

        private string ValueToString(double count)
        {
            if (Math.Abs(count) >= 1000000000)
            {
                return (count / 1000000000).ToString("0.0") + "G";
            }

            if (Math.Abs(count) >= 1000000)
            {
                return (count / 1000000).ToString("0.0") + "M";
            }

            if (Math.Abs(count) >= 1000)
            {
                return (count / 1000).ToString("0.0") + "k";
            }

            return count.ToString("0.0");
        }

        private string CountToString(double count)
        {
            if (Math.Abs(count) >= 1000000000)
            {
                return (count / 1000000000).ToString("0.0") + "B";
            }

            if (Math.Abs(count) >= 1000000)
            {
                return (count / 1000000).ToString("0.0") + "M";
            }

            if (Math.Abs(count) >= 1000)
            {
                return (count / 1000).ToString("0.0") + "k";
            }

            return count.ToString("0.0");
        }

    }
}

namespace IngameScript2.ServerScripts3Solars
{
    internal partial class Program : MyGridProgram
    {

        private enum RotorType
        {
            main,
            second
        }

        private enum State
        {
            rotationMain,
            rotationSecond,
            stop,
            init
        }

        private float StartRotorSpeed = 2;
        private float StopRotorSpeed = 0.15f;
        private float DecreaseKoef = -0.75f;

        private IMyCockpit debugSeet;
        private IMyTextPanel screen;

        private string mainRotorName = "mainRotorSolar";
        private IMyMotorStator mainMotorStator;
        private string nameRotorName1 = "secondRotor1";
        private IMyMotorStator secondMotorStator1;
        private string nameRotorName2 = "secondRotor2";
        private IMyMotorStator secondMotorStator2;

        private List<IMySolarPanel> solars = new List<IMySolarPanel>();
        private List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        private State CurrentState = State.init;
        private float currentOut;
        private float GoodOut = 0.145f;

        private List<IMyLightingBlock> lamps = new List<IMyLightingBlock>();

        public Program()
        {
            Init();
        }

        private void Init()
        {
            debugSeet = (IMyCockpit)GridTerminalSystem.GetBlockWithName("debug1");
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("disp1");
            GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(solars);
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
            GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lamps);
            lamps.RemoveAll(x => !x.CustomName.Contains("SolarLamp"));
            mainMotorStator = (IMyMotorStator)GridTerminalSystem.GetBlockWithName(mainRotorName);
            secondMotorStator1 = (IMyMotorStator)GridTerminalSystem.GetBlockWithName(nameRotorName1);
            secondMotorStator2 = (IMyMotorStator)GridTerminalSystem.GetBlockWithName(nameRotorName2);
            StartRotorSpeed *= -1;
            CurrentState = State.init;
            currentOut = 0;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Update(argument);
        }

        public void Update(string arg)
        {
            if (arg == "r")
            {
                Init();
            }
            float batterieskoef = batteries.Sum(x => x.CurrentStoredPower) / batteries.Sum(x => x.MaxStoredPower);
            if (batterieskoef < 0.9f)
            {
                string debug = "";
                float percent = solars.Sum(x => x.MaxOutput) / solars.Count;

                if (CurrentState == State.init)
                {
                    StopAll();
                    Rotate(RotorType.main, StartRotorSpeed);
                    CurrentState = State.rotationMain;
                }

                if (CurrentState == State.rotationSecond && currentOut > percent)
                {
                    Rotate(RotorType.second, secondMotorStator1.TargetVelocityRPM * DecreaseKoef);
                    if (Math.Abs(secondMotorStator1.TargetVelocityRPM) < StopRotorSpeed)
                    {
                        StopAll();
                        CurrentState = State.stop;
                    }
                }

                if (CurrentState == State.rotationMain && currentOut > percent)
                {
                    Rotate(RotorType.main, mainMotorStator.TargetVelocityRPM * DecreaseKoef);
                    if (Math.Abs(mainMotorStator.TargetVelocityRPM) < StopRotorSpeed)
                    {
                        StopRotor(RotorType.main);
                        CurrentState = State.rotationSecond;
                        Rotate(RotorType.second, StartRotorSpeed);
                    }
                }
                currentOut = percent;

                debug = percent.ToString("0.0000") + "\n" + CurrentState.ToString();
                debugSeet.GetSurface(0).WriteText(debug);

                if (percent < GoodOut && CurrentState == State.stop)
                {
                    MakeRedLamps();
                    Init();
                }
                if (percent >= GoodOut)
                {
                    StopAll();
                    MakeGreenLamps();
                    CurrentState = State.stop;
                }
            }
            else
            {
                StopAll();
                MakeGreenLamps();
                CurrentState = State.stop;
            }
        }

        private void MakeRedLamps()
        {
            foreach (IMyLightingBlock lamp in lamps)
            {
                lamp.Color = Color.Red;
            }
        }

        private void MakeGreenLamps()
        {
            foreach (IMyLightingBlock lamp in lamps)
            {
                lamp.Color = Color.Green;
            }
        }

        private void Rotate(RotorType rotorType, float DegreesPerSecond)
        {
            if (rotorType == RotorType.main)
            {
                mainMotorStator.TargetVelocityRPM = DegreesPerSecond;
            }
            if (rotorType == RotorType.second)
            {
                secondMotorStator1.TargetVelocityRPM = DegreesPerSecond;
                secondMotorStator2.TargetVelocityRPM = -DegreesPerSecond;
            }
        }

        private void StopAll()
        {
            currentOut = 0;
            StopRotor(RotorType.main);
            StopRotor(RotorType.second);
        }

        private void StopRotor(RotorType rotorType)
        {
            if (rotorType == RotorType.main)
            {
                mainMotorStator.TargetVelocityRPM = 0;
            }
            if (rotorType == RotorType.second)
            {
                secondMotorStator1.TargetVelocityRPM = 0;
                secondMotorStator2.TargetVelocityRPM = 0;
            }
        }

    }
}

namespace IngameScript10.MegaDrill
{
    internal partial class Program : MyGridProgram
    {
        /// <summary>
        /// Base class for all average types
        /// </summary>
        /// <typeparam name="T">The type of object to be averaged</typeparam>
        public abstract class AbstractAverage<T> where T : new()
        {
            /// <summary>
            /// Number of values to average
            /// </summary>
            public int Count => All.Length;

            /// <summary>
            /// Average value
            /// </summary>
            public abstract T Average { get; }

            /// <summary>
            /// Array for all values
            /// </summary>
            protected readonly T[] All;
            public int Counter
            {
                get
                {
                    return counter;
                }
                private set
                {
                    counter = value;
                    if (counter >= All.Length)
                    {
                        counter = 0;
                    }
                }
            }
            private int counter = 0;

            protected AbstractAverage(int count = 5)
            {
                All = new T[count];
            }

            /// <summary>
            /// Empty all values
            /// </summary>
            public void Clear()
            {
                for (int i = 0; i < All.Length; i++)
                {
                    All[i] = new T();
                }
            }

            /// <summary>
            /// Add next value to inner array
            /// </summary>
            public void AddNext(T value)
            {
                All[Counter++] = value;
            }

            public T this[int i] => All[i];

            public abstract T Max { get; }

        }

        /// <summary>
        /// Average realization for float-type
        /// </summary>
        public class AverageDouble : AbstractAverage<double>
        {
            public AverageDouble(int count = 5) : base(count)
            {
            }

            public override double Max
            {
                get
                {
                    return All.Max(x => x);
                }
            }

            public override double Average
            {
                get
                {
                    double sum = 0;
                    int indexes = 0;
                    foreach (double value in All)
                    {
                        if (value != 0)
                        {
                            sum += value;
                            indexes++;
                        }
                    }
                    return indexes != 0 ? sum / indexes : 0;
                }
            }
        }

        private class SCR
        {
            public readonly string name;

            public IMyTextPanel screen;
            public IMyTextSurface surface;
            private bool isInitWithPanel;

            public SCR(IMyGridTerminalSystem grid, string name)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
                isInitWithPanel = true;
            }

            public SCR(IMyTextPanel textPanel, string name)
            {
                this.name = name;
                screen = textPanel;
                isInitWithPanel = true;
            }

            public SCR(IMyCockpit cockpit, int index)
            {
                name = index.ToString();
                surface = cockpit.GetSurface(index);
                isInitWithPanel = false;
            }

            public SCR(IMyTextSurface surface, int index)
            {
                name = index.ToString();
                this.surface = surface;
                isInitWithPanel = false;
            }

            public string Text
            {
                get
                {
                    if (isInitWithPanel)
                    {
                        return screen?.GetText();
                    }
                    else
                    {
                        return surface?.GetText();
                    }
                }
                set
                {
                    if (isInitWithPanel)
                    {
                        screen?.WriteText(value);
                    }
                    else
                    {
                        surface?.WriteText(value);
                    }
                }
            }
        }

        private DateTime TimeNow => DateTime.Now;
        private DateTime PrevTime;
        private TimeSpan DeltaTime => TimeNow - PrevTime;

        private List<SCR> screens = new List<SCR>();
        private string[] screensNames = new string[] { "disp1", "disp2", "disp3", "disp4", "disp5", "disp6", "disp7", "disp8" };
        private const string spaceString = "____________________________________________________________________________";
        private const int oneStringLength = 30;

        private List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        private List<IMyGasGenerator> H2Henerators = new List<IMyGasGenerator>();
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        private List<IMyCargoContainer> containersForCraft = new List<IMyCargoContainer>();

        private List<IMyProductionBlock> assemblers = new List<IMyProductionBlock>();
        private List<IMyRefinery> refineries = new List<IMyRefinery>();
        private Dictionary<string, AverageDouble> refinesTotal = new Dictionary<string, AverageDouble>();
        private Dictionary<string, double> prevRefine = new Dictionary<string, double>();

        private float addCraftValue = 0.02f;

        #region addition dictionaries

        private Dictionary<string, int> componentsMinimum = new Dictionary<string, int>()
        {
            { "BulletproofGlass", 0 }, //бронестекло
            { "ComputerComponent", 0 }, //компьютер
            { "ConstructionComponent", 0 }, //строительный компонент
            { "DetectorComponent", 0 }, //компоненты детектора руды
            { "Display", 0 }, //экран
            { "ExplosivesComponent", 0 }, //взрывчатка
            { "GirderComponent", 0 }, //балки
            { "GravityGeneratorComponent", 0 }, //компоненты грави-генератора
            { "InteriorPlate", 0 }, //внутренняя пластина
            { "LargeTube", 0 }, //большая труба
            { "MedicalComponent", 0 }, //медицинские компоненты
            { "MetalGrid", 0 }, //решетка
            { "MotorComponent", 0 }, //мотор
            { "PowerCell", 0 }, //батарея
            { "RadioCommunicationComponent", 0 }, //радио-компоненты
            { "ReactorComponent", 0 }, //реакторные компоненты
            { "SmallTube", 0 }, //малая труба
            { "SolarCell", 0 }, //солненые ячейки
            { "SteelPlate", 0 }, //стальная пластина
            { "Superconductor", 0 }, //сверхпроводник
            { "ThrustComponent", 0 } //ионный ускоритель
        };

        private Dictionary<string, MyDefinitionId> blueprints = new Dictionary<string, MyDefinitionId>()
        {
            { "BulletproofGlass", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/BulletproofGlass") },
            { "ComputerComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ComputerComponent") },
            { "ConstructionComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ConstructionComponent") },
            { "DetectorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/DetectorComponent") },
            { "Display", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/Display") },
            { "ExplosivesComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ExplosivesComponent") },
            { "GirderComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/GirderComponent") },
            { "GravityGeneratorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/GravityGeneratorComponent") },
            { "InteriorPlate", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/InteriorPlate") },
            { "LargeTube", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/LargeTube") },
            { "MedicalComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MedicalComponent") },
            { "MetalGrid", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MetalGrid") },
            { "MotorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MotorComponent") },
            { "PowerCell", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/PowerCell") },
            { "RadioCommunicationComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/RadioCommunicationComponent") },
            { "ReactorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ReactorComponent") },
            { "SmallTube", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SmallTube") },
            { "SolarCell", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SolarCell") },
            { "SteelPlate", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SteelPlate") },
            { "Superconductor", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/Superconductor") },
            { "ThrustComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ThrustComponent") }
        };

        private Dictionary<string, string> blueprintsToTypes = new Dictionary<string, string>()
        {
            { "BulletproofGlass", "BulletproofGlass" },
            { "ComputerComponent", "Computer" },
            { "ConstructionComponent", "Construction" },
            { "DetectorComponent", "Detector" },
            { "Display", "Display" },
            { "ExplosivesComponent", "Explosives" },
            { "GirderComponent", "Girder" },
            { "GravityGeneratorComponent", "GravityGenerator" },
            { "InteriorPlate", "InteriorPlate" },
            { "LargeTube", "LargeTube" },
            { "MedicalComponent", "Medical" },
            { "MetalGrid", "MetalGrid" },
            { "MotorComponent", "Motor" },
            { "PowerCell", "PowerCell" },
            { "RadioCommunicationComponent", "RadioCommunication" },
            { "ReactorComponent", "Reactor" },
            { "SmallTube", "SmallTube" },
            { "SolarCell", "SolarCell" },
            { "SteelPlate", "SteelPlate" },
            { "Superconductor", "Superconductor" },
            { "ThrustComponent", "Thrust" }
        };



        private Dictionary<string, string> typesToBlueprints = new Dictionary<string, string>()
        {
            { "BulletproofGlass", "BulletproofGlass" },
            { "Computer", "ComputerComponent" },
            { "Construction", "ConstructionComponent" },
            { "Detector", "DetectorComponent" },
            { "Display", "Display" },
            { "Explosives", "ExplosivesComponent" },
            { "Girder", "GirderComponent" },
            { "GravityGenerator", "GravityGeneratorComponent" },
            { "InteriorPlate", "InteriorPlate" },
            { "LargeTube", "LargeTube" },
            { "Medical", "MedicalComponent" },
            { "MetalGrid", "MetalGrid" },
            { "Motor", "MotorComponent" },
            { "PowerCell", "PowerCell" },
            { "RadioCommunication", "RadioCommunicationComponent" },
            { "Reactor", "ReactorComponent" },
            { "SmallTube", "SmallTube" },
            { "SolarCell", "SolarCell" },
            { "SteelPlate", "SteelPlate" },
            { "Superconductor", "Superconductor" },
            { "Thrust", "ThrustComponent" }
        };

        #endregion

        public Program()
        {
            InitAll();
        }

        public void Main(string argument, UpdateType updateSource)
        {

            BatteriesInfo(argument);
            CargoInfo(argument);
            PrevTime = TimeNow;
        }

        private void InitAll()
        {
            InitScreens();
            InitBatteries();
            InitContainers();
            InitRefinery();
        }

        #region initing

        private void InitRefinery()
        {
            GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refineries);
        }

        private void InitContainers()
        {
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers);
            containers.RemoveAll(x => !x.CustomName.Contains("MegaDrillCont"));
            assemblers.RemoveAll(x => !x.CustomName.Contains("autoAsm"));
        }

        private void InitBatteries()
        {
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
        }

        private void InitScreens()
        {
            //foreach (string name in screensNames)
            //{
            //    screens.Add(new SCR(GridTerminalSystem, name));
            //}
            IMyCockpit myCockpit = (IMyCockpit)GridTerminalSystem.GetBlockWithName("kock");
            for (int i = 0; i < 6; i++)
            {
                screens.Add(new SCR(myCockpit, i));
            }
            screens.Add(new SCR(GridTerminalSystem, "DrillMegaDisp"));
        }

        #endregion

        public void BatteriesInfo(string arg)
        {
            double Sum = 0;
            double Max = 0;
            double EnPlus = 0;
            double EnMinus = 0;

            for (int i = 0; i < batteries.Count; i++)
            {
                if (batteries[i] != null)
                {
                    Max += batteries[i].MaxStoredPower;
                    Sum += batteries[i].CurrentStoredPower;
                    EnPlus += batteries[i].CurrentInput;
                    EnMinus += batteries[i].CurrentOutput;
                }
            }

            string strVolume = "Energy : " + CountToString(Sum * 1000000) + "w / " + CountToString(Max * 1000000) + "w";
            string strVolumePersent = "Energy percent : " + (Sum / Max * 100).ToString("0.0") + "%";
            string InOut = "in : +" + CountToString(EnPlus * 1000000) + "wh" +
                           "\nout : -" + CountToString(EnMinus * 1000000) + "wh" +
                           "\ntotal : " + CountToString((EnPlus - EnMinus) * 1000000) + "wh";
            double time = (Max - Sum) / (EnPlus - EnMinus);

            long timeTicks = (long)(time * 3600 * 10000000);
            TimeSpan timeSpan = new TimeSpan(timeTicks);

            if (Sum / Max * 100 < 99)
            {
                if (time > 0)
                {
                    InOut += "\ntime to charge : " + timeSpan.ToString(@"dd\.hh\:mm\:ss");
                }
                else
                {
                    double timeToDiscarge = Sum / (EnPlus - EnMinus);
                    long timeTicksToDiscarge = (long)(timeToDiscarge * 3600 * 10000000);
                    TimeSpan timeSpanToDiscarge = new TimeSpan(timeTicks);
                    InOut += "\ntime to discharge : " + timeSpanToDiscarge.ToString(@"dd\.hh\:mm\:ss");
                }
            }
            else
            {
                InOut += "\nBatteries charged";
            }

            screens[0].Text = strVolume + "\n" + strVolumePersent + "\n" + InOut + "\n";
        }

        private void CargoInfo(string arg)
        {
            screens[1].Text = "0";

            double volumeSum = 0;
            double volumeMax = 0;
            for (int i = 0; i < containers.Count; i++)
            {
                IMyInventory invent = containers[i].GetInventory();
                volumeMax += (double)invent.MaxVolume;
                volumeSum += (double)invent.CurrentVolume;
            }

            screens[1].Text = "1";

            Dictionary<string, double> components = new Dictionary<string, double>();
            Dictionary<string, double> others = new Dictionary<string, double>();

            for (int i = 0; i < containers.Count; i++)
            {
                IMyInventory invent = containers[i].GetInventory();
                volumeMax += (double)invent.MaxVolume;
                volumeSum += (double)invent.CurrentVolume;

                List<MyInventoryItem> inventoryItem = new List<MyInventoryItem>();
                invent.GetItems(inventoryItem);
                for (int j = 0; j < inventoryItem.Count; j++)
                {
                    if (inventoryItem[j].Type.TypeId.Contains("Component"))
                    {
                        string key = inventoryItem[j].Type.SubtypeId;
                        if (!components.ContainsKey(key))
                        {
                            components.Add(key, 0);
                        }
                        components[key] += (double)inventoryItem[j].Amount;
                    }
                    else
                    {
                        if (inventoryItem[j].Type.TypeId.Contains("Ingot") || inventoryItem[j].Type.TypeId.Contains("Ore"))
                        {
                            string key = inventoryItem[j].Type.TypeId.Substring(16) + inventoryItem[j].Type.SubtypeId;
                            if (!others.ContainsKey(key))
                            {
                                others.Add(key, 0);
                            }
                            others[key] += (double)inventoryItem[j].Amount;
                        }
                    }
                }
            }

            screens[1].Text = "2";

            Dictionary<string, double> production = new Dictionary<string, double>();

            foreach (IMyAssembler asm in assemblers)
            {
                List<MyProductionItem> queue = new List<MyProductionItem>();
                asm.GetQueue(queue);
                foreach (MyProductionItem item in queue)
                {
                    string key = item.BlueprintId.SubtypeName;
                    if (!production.ContainsKey(key))
                    {
                        production.Add(key, 0);
                    }
                    production[key] += (int)item.Amount;
                }

                IMyInventory asmInvent = asm.OutputInventory;
                List<MyInventoryItem> inventoryItems = new List<MyInventoryItem>();
                asmInvent.GetItems(inventoryItems);

                foreach (MyInventoryItem item in inventoryItems)
                {
                    if (item.Type.TypeId.Contains("Component"))
                    {
                        string key = item.Type.SubtypeId;
                        if (!components.ContainsKey(key))
                        {
                            components.Add(key, 0);
                        }
                        components[key] += (double)item.Amount;

                        IMyInventory inventTo = containersForCraft.FirstOrDefault(x =>
                        {
                            return x.GetInventory().MaxVolume - x.GetInventory().CurrentVolume > item.Amount;
                        })?.GetInventory();
                        asmInvent.TransferItemTo(inventTo, item);

                    }
                }
            }

            screens[1].Text = "3";

            foreach (KeyValuePair<string, int> item in componentsMinimum)
            {
                screens[1].Text = "3.0";

                int prodValue = 0;
                if (production.ContainsKey(item.Key))
                {
                    prodValue = (int)production[item.Key];
                }

                screens[1].Text = "3.1 " + item.Key;

                int cargoValue = 0;
                if (components.ContainsKey(blueprintsToTypes[item.Key]))
                {
                    cargoValue = (int)components[blueprintsToTypes[item.Key]];
                }
                else
                {
                    components.Add(blueprintsToTypes[item.Key], 0);
                }

                screens[1].Text = "3.2 " + blueprintsToTypes[item.Key];

                if (prodValue + cargoValue < item.Value)
                {
                    foreach (IMyAssembler ams in assemblers)
                    {
                        ams.AddQueueItem(blueprints[item.Key], (double)Math.Truncate(addCraftValue * item.Value));
                    }
                }

                screens[1].Text = "3.3 " + blueprints[item.Key];
            }

            screens[1].Text = "4";

            string strCargo = "Cargo absolute : " + ValueToString(volumeSum * 1000) + "l / " + ValueToString(volumeMax * 1000) + "l";
            string strCargoPersent = "\nCargo percent : " + (volumeSum / volumeMax * 100).ToString("0.0") + "%";

            screens[1].Text = "5";

            screens[3].Text = /*strCargo +*/ strCargoPersent;
            screens[6].Text = (volumeSum / volumeMax * 100).ToString("0.0") + "%";
        }

        private enum TankType
        {
            oxy,
            hydro,
            none
        }
        private TankType GetTankType(IMyTerminalBlock theBlock)
        {
            if (theBlock is IMyGasTank)
            {
                if (theBlock.BlockDefinition.SubtypeId.Contains("Hydro"))
                {
                    return TankType.hydro;
                }
                else
                {
                    return TankType.oxy;
                }
            }
            return TankType.none;
        }

        private string ValueToString(double count)
        {
            if (Math.Abs(count) >= 1000000000)
            {
                return (count / 1000000000).ToString("0.0") + "G";
            }

            if (Math.Abs(count) >= 1000000)
            {
                return (count / 1000000).ToString("0.0") + "M";
            }

            if (Math.Abs(count) >= 1000)
            {
                return (count / 1000).ToString("0.0") + "k";
            }

            return count.ToString("0.0");
        }

        private string CountToString(double count, string roundto = "0.0")
        {
            if (Math.Abs(count) >= 1000000000)
            {
                return (count / 1000000000).ToString(roundto) + "B";
            }

            if (Math.Abs(count) >= 1000000)
            {
                return (count / 1000000).ToString(roundto) + "M";
            }

            if (Math.Abs(count) >= 1000)
            {
                return (count / 1000).ToString(roundto) + "k";
            }

            if (Math.Abs(count) == Math.Abs(Math.Truncate(count)))
            {
                return count.ToString();
            }

            return count.ToString(roundto);
        }

        private double Clamp(double x, double min, double max)
        {
            return x < min ? min : x > max ? max : x;
        }

    }
}

namespace IngameScript10.DynamicGyros
{

    internal partial class Program : MyGridProgram
    {

        private float minGyroPower = 0.01f;

        private float maxGyroPower = 0.5f;

        private List<IMyGyro> gyros = new List<IMyGyro>();
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();

        public Program()
        {
            InitAll();

        }

        private void InitAll()
        {
            InitGyros();
            InitContainers();
        }

        private void InitContainers()
        {
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers);
        }

        private void InitGyros()
        {
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);
            gyros.RemoveAll(x => !x.CustomName.Contains("gyroWelder"));
        }

        public void Main(string argument, UpdateType updateSource)
        {
            BalanceGyros();
        }

        private void BalanceGyros()
        {
            double volumeSum = 0;
            double volumeMax = 0;
            for (int i = 0; i < containers.Count; i++)
            {
                IMyInventory invent = containers[i].GetInventory();
                volumeMax += (double)invent.MaxVolume;
                volumeSum += (double)invent.CurrentVolume;
            }
            double percent = volumeSum / volumeMax;

            foreach (IMyGyro gyro in gyros)
            {
                gyro.GyroPower = (float)(minGyroPower + percent * (1 - minGyroPower)) * maxGyroPower;
            }

        }
    }
}

namespace IngameScript10.TS
{
    internal partial class Program : MyGridProgram
    {
        public abstract class AbstractAverage<T> where T : new()
        {
            public int Count => All.Length;
            public abstract T Average { get; }
            protected readonly T[] All;
            public int Counter
            {
                get
                {
                    return counter;
                }
                private set
                {
                    counter = value % All.Length;
                }
            }
            private int counter = 0;

            protected AbstractAverage(int count = 5)
            {
                All = new T[count];
            }
            public void Clear()
            {
                for (int i = 0; i < All.Length; i++)
                {
                    All[i] = new T();
                }
            }
            public void AddNext(T value)
            {
                All[Counter++] = value;
            }
            public T this[int i] => All[i];
            public abstract T Max { get; }

        }

        public class AverageDouble : AbstractAverage<double>
        {
            public AverageDouble(int count = 5) : base(count)
            {
            }

            public override double Max
            {
                get
                {
                    return All.Max(x => x);
                }
            }

            public override double Average
            {
                get
                {
                    double sum = 0;
                    int indexes = 0;
                    foreach (double value in All)
                    {
                        if (value != 0)
                        {
                            sum += value;
                            indexes++;
                        }
                    }
                    return indexes != 0 ? sum / indexes : 0;
                }
            }
        }

        private class SCR
        {
            public readonly string name;

            public IMyTextPanel screen;
            public IMyTextSurface surface;
            private bool isInitWithPanel;

            private string cachedText;

            public SCR(IMyGridTerminalSystem grid, string name)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
                screen.FontColor = new Color(105, 255, 187);
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyTextPanel textPanel, string name)
            {
                this.name = name;
                screen = textPanel;
                screen.FontColor = new Color(105, 255, 187);
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyCockpit cockpit, int index)
            {
                name = index.ToString();
                surface = cockpit.GetSurface(index);
                surface.FontColor = new Color(105, 255, 187);
                isInitWithPanel = false;
                Text = name;
            }

            public SCR(IMyTextSurface surface, int index)
            {
                name = index.ToString();
                this.surface = surface;
                surface.FontColor = new Color(105, 255, 187);
                isInitWithPanel = false;
                Text = name;
            }

            public string Text
            {
                get
                {
                    return cachedText;
                }
                set
                {
                    cachedText = value;
                    if (isInitWithPanel)
                    {
                        screen?.WriteText(cachedText);
                    }
                    else
                    {
                        surface?.WriteText(cachedText);
                    }
                }
            }
        }

        private DateTime TimeNow => DateTime.Now;
        private DateTime PrevTime;
        private TimeSpan DeltaTime => TimeNow - PrevTime;

        private List<SCR> screens = new List<SCR>();
        private string[] screensNames = new string[] { "TS_disp1", "TS_disp2", "TS_disp3", "TS_disp4", "TS_disp5", "TS_disp6", "TS_disp7", "TS_disp8", "TS_disp9", "debugDisplayTS" };
        private const string spaceString = "____________________________________________________________________________";
        private const int oneStringLength = 30;

        private List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        private List<IMyGasGenerator> H2Henerators = new List<IMyGasGenerator>();
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        private List<IMyCargoContainer> containersForCraft = new List<IMyCargoContainer>();

        private List<IMyProductionBlock> assemblers = new List<IMyProductionBlock>();
        private List<IMyRefinery> refineries = new List<IMyRefinery>();

        private List<IMyJumpDrive> jumpDrives = new List<IMyJumpDrive>();
        private const string jumpDriveName = "jumpdrive";

        private Dictionary<string, AverageDouble> incomeTotal = new Dictionary<string, AverageDouble>();
        private Dictionary<string, double> prevCount = new Dictionary<string, double>();

        private float addCraftValue = 0.02f;

        #region addition dictionaries

        private Dictionary<string, int> componentsMinimum = new Dictionary<string, int>()
        {
            { "BulletproofGlass", 200000 }, //бронестекло
            { "ComputerComponent", 1000000 }, //компьютер
            { "ConstructionComponent", 1000000 }, //строительный компонент
            { "DetectorComponent", 10000 }, //компоненты детектора руды
            { "Display", 10000 }, //экран
            { "ExplosivesComponent", 10000 }, //взрывчатка
            { "GirderComponent", 200000 }, //балки
            { "GravityGeneratorComponent", 10000 }, //компоненты грави-генератора
            { "InteriorPlate", 1000000 }, //внутренняя пластина
            { "LargeTube", 300000 }, //большая труба
            { "MedicalComponent", 10000 }, //медицинские компоненты
            { "MetalGrid", 500000 }, //решетка
            { "MotorComponent", 500000 }, //мотор
            { "PowerCell", 1000000 }, //батарея
            { "RadioCommunicationComponent", 10000 }, //радио-компоненты
            { "ReactorComponent", 20000 }, //реакторные компоненты
            { "SmallTube", 500000 }, //малая труба
            { "SolarCell", 100000 }, //солненые ячейки
            { "SteelPlate", 2000000 }, //стальная пластина
            { "Superconductor", 200000 }, //сверхпроводник
            { "ThrustComponent", 150000 } //ионный ускоритель
        };
        private Dictionary<string, MyDefinitionId> blueprints = new Dictionary<string, MyDefinitionId>()
        {
            { "BulletproofGlass", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/BulletproofGlass") },
            { "ComputerComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ComputerComponent") },
            { "ConstructionComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ConstructionComponent") },
            { "DetectorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/DetectorComponent") },
            { "Display", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/Display") },
            { "ExplosivesComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ExplosivesComponent") },
            { "GirderComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/GirderComponent") },
            { "GravityGeneratorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/GravityGeneratorComponent") },
            { "InteriorPlate", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/InteriorPlate") },
            { "LargeTube", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/LargeTube") },
            { "MedicalComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MedicalComponent") },
            { "MetalGrid", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MetalGrid") },
            { "MotorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MotorComponent") },
            { "PowerCell", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/PowerCell") },
            { "RadioCommunicationComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/RadioCommunicationComponent") },
            { "ReactorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ReactorComponent") },
            { "SmallTube", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SmallTube") },
            { "SolarCell", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SolarCell") },
            { "SteelPlate", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SteelPlate") },
            { "Superconductor", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/Superconductor") },
            { "ThrustComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ThrustComponent") }
        };
        private Dictionary<string, string> blueprintsToTypes = new Dictionary<string, string>()
        {
            { "BulletproofGlass", "BulletproofGlass" },
            { "ComputerComponent", "Computer" },
            { "ConstructionComponent", "Construction" },
            { "DetectorComponent", "Detector" },
            { "Display", "Display" },
            { "ExplosivesComponent", "Explosives" },
            { "GirderComponent", "Girder" },
            { "GravityGeneratorComponent", "GravityGenerator" },
            { "InteriorPlate", "InteriorPlate" },
            { "LargeTube", "LargeTube" },
            { "MedicalComponent", "Medical" },
            { "MetalGrid", "MetalGrid" },
            { "MotorComponent", "Motor" },
            { "PowerCell", "PowerCell" },
            { "RadioCommunicationComponent", "RadioCommunication" },
            { "ReactorComponent", "Reactor" },
            { "SmallTube", "SmallTube" },
            { "SolarCell", "SolarCell" },
            { "SteelPlate", "SteelPlate" },
            { "Superconductor", "Superconductor" },
            { "ThrustComponent", "Thrust" }
        };
        private Dictionary<string, string> typesToBlueprints = new Dictionary<string, string>()
        {
            { "BulletproofGlass", "BulletproofGlass" },
            { "Computer", "ComputerComponent" },
            { "Construction", "ConstructionComponent" },
            { "Detector", "DetectorComponent" },
            { "Display", "Display" },
            { "Explosives", "ExplosivesComponent" },
            { "Girder", "GirderComponent" },
            { "GravityGenerator", "GravityGeneratorComponent" },
            { "InteriorPlate", "InteriorPlate" },
            { "LargeTube", "LargeTube" },
            { "Medical", "MedicalComponent" },
            { "MetalGrid", "MetalGrid" },
            { "Motor", "MotorComponent" },
            { "PowerCell", "PowerCell" },
            { "RadioCommunication", "RadioCommunicationComponent" },
            { "Reactor", "ReactorComponent" },
            { "SmallTube", "SmallTube" },
            { "SolarCell", "SolarCell" },
            { "SteelPlate", "SteelPlate" },
            { "Superconductor", "Superconductor" },
            { "Thrust", "ThrustComponent" }
        };

        private Dictionary<string, double> costsResources = new Dictionary<string, double>()
        {
            { "IngotPlatinum", 120000 },
            { "IngotGold", 17000 },
            { "IngotCobalt", 1300 },
            { "IngotMagnesium", 30000 },
            { "IngotSilver", 1800 },
            { "IngotUranium", 64000 },
            { "IngotIron", 145 },
            { "IngotSilicon", 165 },
            { "IngotNickel", 292 },

            { "BulletproofGlass", 1015 }, //бронестекло
            { "ComputerComponent", 43 }, //компьютер
            { "ConstructionComponent", 475 }, //строительный компонент
            { "DetectorComponent", 2613 }, //компоненты детектора руды
            { "Display", 398 }, //экран
            { "ExplosivesComponent", 35691 }, //взрывчатка
            { "GirderComponent", 358 }, //балки
            { "GravityGeneratorComponent", 385875 }, //компоненты грави-генератора
            { "InteriorPlate", 179 }, //внутренняя пластина
            { "LargeTube", 1980 }, //большая труба
            { "MedicalComponent", 43072 }, //медицинские компоненты
            { "MetalGrid", 3454 }, //решетка
            { "MotorComponent", 2123 }, //мотор
            { "PowerCell", 1125 }, //батарея
            { "RadioCommunicationComponent", 678 }, //радио-компоненты
            { "ReactorComponent", 8478 }, //реакторные компоненты
            { "SmallTube", 297 }, //малая труба
            { "SolarCell", 849 }, //солненые ячейки
            { "SteelPlate", 1249 }, //стальная пластина
            { "Superconductor", 26524 }, //сверхпроводник
            { "ThrustComponent", 45068 } //ионный ускоритель
        };

        #endregion

        private IMyProgrammableBlock programmableBlock;

        public Program()
        {
            InitAll();
        }

        private int updateCounter = 0;
        private int UpdateCounter
        {
            get
            {
                return updateCounter;
            }
            set
            {
                updateCounter = value % 10;
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            sDebug = "";

            BatteriesInfo(argument);
            CargoInfo();
            RefineryClearing();
            JumpDriveInfo();
            CloseDoors();
            UpdateInfo();

            //screens[4].Text = sDebug;
            screens[9].Text = sDebug;

            PrevTime = TimeNow;

        }

        private void UpdateInfo()
        {
            if (programmableBlock != null)
            {
                string s = "processing...\nupdate:" + UpdateCounter + "\ntime:" + TimeToString(TimeNow);
                programmableBlock.GetSurface(0).WriteText(s);
                IMyTextSurfaceProvider myButtonPanel = (IMyTextSurfaceProvider)GridTerminalSystem.GetBlockWithName("TS_stateDisp");
                if (myButtonPanel != null)
                {
                    IMyTextSurface drawingSurface = myButtonPanel.GetSurface(0);
                    drawingSurface.WriteText(s);
                }
                UpdateCounter++;
            }
        }

        private void JumpDriveInfo()
        {
            string percentJumpStr = "";
            foreach (IMyJumpDrive jd in jumpDrives)
            {
                float percentJump = jd.CurrentStoredPower / jd.MaxStoredPower * 100;
                percentJumpStr += jd.CustomName + " power : " + percentJump.ToString("0.0") + "%";
                if (percentJump == 100f)
                {
                    percentJumpStr += " (Ready)";
                }
                percentJumpStr += "\n";
            }
            screens[2].Text = "Jump drive info:\nDistance:" + Math.Truncate(jumpDrives[0].JumpDistanceMeters / 1000) + "km\n";
            screens[3].Text = percentJumpStr;
        }

        private void RefineryClearing()
        {
            foreach (IMyRefinery refinery in refineries)
            {
                TryTransferItems(refinery, containers);
            }
        }

        public string sDebug = "Debug\n";

        public bool TryTransferItems(IMyRefinery refinery, List<IMyCargoContainer> containers)
        {
            if (refinery == null || containers == null || containers.Count == 0)
            {
                return false;
            }

            IMyInventory outputInventory = refinery.OutputInventory;

            if (outputInventory.VolumeFillFactor <= 0f)
            {
                return false;
            }

            List<IMyCargoContainer> availableContainers = containers.Where(c => !c.GetInventory().IsFull).ToList();

            if (availableContainers.Count == 0)
            {
                return false;
            }

            Random random = new Random();
            IMyCargoContainer selectedContainer = availableContainers[random.Next(availableContainers.Count)];
            IMyInventory containerInventory = selectedContainer.GetInventory();

            for (int i = 0; i < outputInventory.ItemCount; i++)
            {
                MyInventoryItem? item = outputInventory.GetItemAt(i);
                if (item.HasValue)
                {
                    MyInventoryItem valueItem = item.Value;
                    if (refinery.OutputInventory.CanTransferItemTo(containerInventory, item.Value.Type))
                    {
                        outputInventory.TransferItemTo(containerInventory, valueItem, item.Value.Amount);
                    }
                }
            }

            return true;
        }

        private void InitAll()
        {
            InitScreens();
            InitBatteries();
            InitContainers();
            InitRefinery();
            InitJumpDrive();
            InitDoors();
            InitUpdateScreen();
        }

        #region initing

        private void InitUpdateScreen()
        {
            programmableBlock = Me;

        }

        private void InitRefinery()
        {
            GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refineries);
        }
        private void InitJumpDrive()
        {
            GridTerminalSystem.GetBlocksOfType<IMyJumpDrive>(jumpDrives);
        }

        private void InitContainers()
        {
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers);
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containersForCraft);
            containersForCraft.RemoveAll(x => !x.CustomName.Contains("TS_cont"));
            GridTerminalSystem.GetBlocksOfType<IMyProductionBlock>(assemblers);
            GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(H2Henerators);
            assemblers.RemoveAll(x => !x.CustomName.Contains("TS_autoAsm"));
        }

        private void InitBatteries()
        {
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
        }

        private void InitScreens()
        {
            foreach (string name in screensNames)
            {
                screens.Add(new SCR(GridTerminalSystem, name));
            }

            //IMyCockpit myCockpit = (IMyCockpit)GridTerminalSystem.GetBlockWithName("TS_kock");
            //screens.Add(new SCR(myCockpit, 0));
        }

        #endregion

        public void BatteriesInfo(string arg)
        {
            double Sum = 0;
            double Max = 0;
            double EnPlus = 0;
            double EnMinus = 0;

            for (int i = 0; i < batteries.Count; i++)
            {
                if (batteries[i] != null)
                {
                    Max += batteries[i].MaxStoredPower;
                    Sum += batteries[i].CurrentStoredPower;
                    EnPlus += batteries[i].CurrentInput;
                    EnMinus += batteries[i].CurrentOutput;
                }
            }

            string strVolume = "Energy : " + ValueToString(Sum * 1000000) + "wh / " + ValueToString(Max * 1000000) + "wh";
            string strVolumePersent = "Energy percent : " + (Sum / Max * 100).ToString("0.0") + "%";
            string InOut = "in : +" + ValueToString(EnPlus * 1000000) + "w" +
                           "\nout : -" + ValueToString(EnMinus * 1000000) + "w" +
                           "\ntotal : " + ValueToString((EnPlus - EnMinus) * 1000000) + "w";
            double time = (Max - Sum) / (EnPlus - EnMinus);

            long timeTicks = (long)(time * 3600 * 10000000);
            TimeSpan timeSpan = new TimeSpan(timeTicks);

            if (Sum / Max * 100 < 99)
            {
                if (time > 0)
                {
                    InOut += "\ntime to charge : " + timeSpan.ToString(@"dd\.hh\:mm\:ss");
                }
                else
                {
                    double timeToDiscarge = Sum / (EnPlus - EnMinus);
                    long timeTicksToDiscarge = (long)(timeToDiscarge * 3600 * 10000000);
                    TimeSpan timeSpanToDiscarge = new TimeSpan(timeTicksToDiscarge);
                    InOut += "\ntime to discharge : " + timeSpanToDiscarge.ToString(@"dd\.hh\:mm\:ss");
                }
            }
            else
            {
                InOut += "\nBatteries charged";
            }

            screens[0].Text = strVolume + "\n" + strVolumePersent + "\n" + InOut + "\n";
        }

        private void CargoInfo()
        {
            screens[1].Text = "0";

            double volumeSum = 0;
            double volumeMax = 0;
            for (int i = 0; i < containers.Count; i++)
            {
                IMyInventory invent = containers[i].GetInventory();
                volumeMax += (double)invent.MaxVolume;
                volumeSum += (double)invent.CurrentVolume;
            }

            Dictionary<string, double> components = new Dictionary<string, double>();
            Dictionary<string, double> others = new Dictionary<string, double>();

            List<IMyInventory> inventories = containers.Select(x => x.GetInventory()).ToList();
            inventories.AddRange(refineries.Select(x => x.GetInventory()).ToList());

            foreach (IMyInventory invent in inventories)
            {
                volumeMax += (double)invent.MaxVolume;
                volumeSum += (double)invent.CurrentVolume;

                List<MyInventoryItem> inventoryItem = new List<MyInventoryItem>();
                invent.GetItems(inventoryItem);
                for (int j = 0; j < inventoryItem.Count; j++)
                {
                    if (inventoryItem[j].Type.TypeId.Contains("Component"))
                    {
                        string key = inventoryItem[j].Type.SubtypeId;
                        if (!components.ContainsKey(key))
                        {
                            components.Add(key, 0);
                        }
                        components[key] += (double)inventoryItem[j].Amount;
                    }
                    else
                    {
                        if (inventoryItem[j].Type.TypeId.Contains("Ingot") || inventoryItem[j].Type.TypeId.Contains("Ore"))
                        {
                            string key = inventoryItem[j].Type.TypeId.Substring(16) + inventoryItem[j].Type.SubtypeId;
                            if (!others.ContainsKey(key))
                            {
                                others.Add(key, 0);
                            }
                            others[key] += (double)inventoryItem[j].Amount;
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, double> i in prevCount)
            {
                if (others.ContainsKey(i.Key))
                {
                    if (!incomeTotal.ContainsKey(i.Key))
                    {
                        incomeTotal.Add(i.Key, new AverageDouble());
                    }

                    incomeTotal[i.Key].AddNext(others[i.Key] - i.Value);
                }
            }

            prevCount = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> i in others)
            {
                prevCount.Add(i.Key, i.Value);
            }
            Dictionary<string, double> production = new Dictionary<string, double>();

            foreach (IMyAssembler asm in assemblers)
            {
                List<MyProductionItem> queue = new List<MyProductionItem>();
                asm.GetQueue(queue);
                foreach (MyProductionItem item in queue)
                {
                    string key = item.BlueprintId.SubtypeName;
                    if (!production.ContainsKey(key))
                    {
                        production.Add(key, 0);
                    }
                    production[key] += (int)item.Amount;
                }

                IMyInventory asmInvent = asm.OutputInventory;
                List<MyInventoryItem> inventoryItems = new List<MyInventoryItem>();
                asmInvent.GetItems(inventoryItems);

                foreach (MyInventoryItem item in inventoryItems)
                {
                    if (item.Type.TypeId.Contains("Component"))
                    {
                        string key = item.Type.SubtypeId;
                        if (!components.ContainsKey(key))
                        {
                            components.Add(key, 0);
                        }
                        components[key] += (double)item.Amount;

                        IMyInventory inventTo = containersForCraft.FirstOrDefault(x =>
                        {
                            return x.GetInventory().MaxVolume - x.GetInventory().CurrentVolume > item.Amount;
                        })?.GetInventory();
                        asmInvent.TransferItemTo(inventTo, item);

                    }
                }
            }
            foreach (KeyValuePair<string, int> item in componentsMinimum)
            {


                int prodValue = 0;
                if (production.ContainsKey(item.Key))
                {
                    prodValue = (int)production[item.Key];
                }
                int cargoValue = 0;
                if (components.ContainsKey(blueprintsToTypes[item.Key]))
                {
                    cargoValue = (int)components[blueprintsToTypes[item.Key]];
                }
                else
                {
                    components.Add(blueprintsToTypes[item.Key], 0);
                }
                if (prodValue + cargoValue < item.Value)
                {
                    foreach (IMyAssembler ams in assemblers)
                    {
                        ams.AddQueueItem(blueprints[item.Key], (double)Math.Truncate(addCraftValue * item.Value));
                    }
                }
            }

            double costApproximatly = 0;

            string strCargo = "Cargo absolute : \n" + ValueToString(volumeSum * 1000) + "l / " + ValueToString(volumeMax * 1000) + "l";
            string strCargoPersent = "Cargo percent : \n" + (volumeSum / volumeMax * 100).ToString("0.0") + "%";

            string ComponentsString = "Components:\n";
            List<KeyValuePair<string, double>> compList = components.OrderBy(x => x.Key).ToList();
            foreach (KeyValuePair<string, double> item in compList)
            {
                string str = item.Key + ":" + CountToString(item.Value, "0.000");
                str += spaceString.Substring(0, (int)Clamp(oneStringLength - str.Length, 0, oneStringLength - str.Length));
                if (typesToBlueprints.ContainsKey(item.Key))
                {
                    if (production.ContainsKey(typesToBlueprints[item.Key]))
                    {
                        str += "| In production:" + CountToString(production[typesToBlueprints[item.Key]], "0.000");
                    }
                }
                if (costsResources.ContainsKey(item.Key))
                {
                    costApproximatly += costsResources[item.Key] * item.Value;
                }
                ComponentsString += str + "\n";
            }
            string OresString = "";
            string IngotsString = "";


            foreach (KeyValuePair<string, double> item in others)
            {
                if (item.Key.Contains("Ore"))
                {
                    OresString = item.Key + ":" + CountToString(item.Value, "0.000000") + "\n" + OresString;
                }
                else
                {
                    double income = 0;
                    if (incomeTotal.ContainsKey(item.Key))
                    {
                        income = incomeTotal[item.Key].Average;
                    }

                    if (costsResources.ContainsKey(item.Key))
                    {
                        costApproximatly += costsResources[item.Key] * item.Value;
                    }

                    IngotsString = IngotsString + item.Key.Replace("Ingot", "") + ":\n";

                    if (income != 0)
                    {
                        if (income > 0)
                        {
                            IngotsString = IngotsString + CountToString(item.Value) + " (+" + CountToString(income) + ")";
                        }
                        else
                        {
                            IngotsString = IngotsString + CountToString(item.Value) + " (" + CountToString(income) + ")";
                        }
                    }
                    else
                    {
                        IngotsString = IngotsString + CountToString(item.Value);
                    }
                    IngotsString = IngotsString + "\n\n";
                }
            }
            screens[1].Text = OresString;

            screens[5].Text = IngotsString;
            screens[6].Text = strCargo + "\n\n" + strCargoPersent + "\n\n";
            screens[7].Text = ComponentsString;
            screens[8].Text = $"Cost all:\n{DoubleToStrMoney(Math.Round(costApproximatly))}$";
        }

        private enum TankType
        {
            oxy,
            hydro,
            none
        }
        private TankType GetTankType(IMyTerminalBlock theBlock)
        {
            if (theBlock is IMyGasTank)
            {
                if (theBlock.BlockDefinition.SubtypeId.Contains("Hydro"))
                {
                    return TankType.hydro;
                }
                else
                {
                    return TankType.oxy;
                }
            }
            return TankType.none;
        }

        public static string DoubleToStrMoney(double d)
        {
            //string s = d.ToString("N2");
            //s = s.Substring(0, s.Length - 3);
            return d.ToString("N2");
        }

        private string TimeToString(DateTime timeNow)
        {
            return timeNow.TimeOfDay.Hours.ToString("00") + ":" + timeNow.TimeOfDay.Minutes.ToString("00") + ":" + timeNow.TimeOfDay.Seconds.ToString("00");
        }

        private string ValueToString(double count)
        {
            if (Math.Abs(count) >= 1000000000)
            {
                return (count / 1000000000).ToString("0.0") + "G";
            }

            if (Math.Abs(count) >= 1000000)
            {
                return (count / 1000000).ToString("0.0") + "M";
            }

            if (Math.Abs(count) >= 1000)
            {
                return (count / 1000).ToString("0.0") + "k";
            }

            return count.ToString("0.0");
        }

        private string CountToString(double count, string roundto = "0.0")
        {
            if (Math.Abs(count) >= 1000000000)
            {
                return (count / 1000000000).ToString(roundto) + "B";
            }

            if (Math.Abs(count) >= 1000000)
            {
                return (count / 1000000).ToString(roundto) + "M";
            }

            if (Math.Abs(count) >= 1000)
            {
                return (count / 1000).ToString(roundto) + "k";
            }

            if (Math.Abs(count) == Math.Abs(Math.Truncate(count)))
            {
                return count.ToString();
            }

            return count.ToString(roundto);
        }

        private double Clamp(double x, double min, double max)
        {
            return x < min ? min : x > max ? max : x;
        }

        #region doors

        public const float CLOSING_TIME = 1.2f;

        public class DoorExt
        {
            public IMyDoor door;
            public DateTime lastTimeOpen;

            public DoorExt(IMyDoor door, DateTime lastTimeOpen)
            {
                this.door = door;
                this.lastTimeOpen = lastTimeOpen;
            }

            public bool isClosed => door.Status == DoorStatus.Closed || door.Status == DoorStatus.Closing;

            public double openedSeconds => (DateTime.Now - lastTimeOpen).TotalSeconds;

            public bool Close()
            {
                if (openedSeconds > CLOSING_TIME && !isClosed)
                {
                    door.CloseDoor();
                    return true;
                }
                return false;
            }
        }

        private List<DoorExt> doors = new List<DoorExt>();

        public void InitDoors()
        {
            List<IMyDoor> drs = new List<IMyDoor>();
            GridTerminalSystem.GetBlocksOfType(drs);
            doors = drs.Select(x => new DoorExt(x, TimeNow)).ToList();
        }

        public void CloseDoors()
        {
            foreach (DoorExt door in doors)
            {
                if (door.isClosed)
                {
                    door.lastTimeOpen = TimeNow;
                }
            }
            foreach (DoorExt door in doors)
            {
                door.Close();
            }
        }

        #endregion
    }
}

namespace IngameScript10.RenderingTry
{
    internal partial class Program : MyGridProgram
    {
        private class SCR
        {
            public readonly string name;

            public IMyTextPanel screen;
            public IMyTextSurface surface;
            private bool isInitWithPanel;

            public SCR(IMyGridTerminalSystem grid, string name)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
                isInitWithPanel = true;
            }

            public SCR(IMyTextPanel textPanel, string name)
            {
                this.name = name;
                screen = textPanel;
                isInitWithPanel = true;
            }

            public SCR(IMyCockpit cockpit, int index)
            {
                name = index.ToString();
                surface = cockpit.GetSurface(index);
                isInitWithPanel = false;
            }

            public SCR(IMyTextSurface surface, int index)
            {
                name = index.ToString();
                this.surface = surface;
                isInitWithPanel = false;
            }

            public string Text
            {
                get
                {
                    if (isInitWithPanel)
                    {
                        return screen?.GetText();
                    }
                    else
                    {
                        return surface?.GetText();
                    }
                }
                set
                {
                    if (isInitWithPanel)
                    {
                        screen?.WriteText(value);
                    }
                    else
                    {
                        surface?.WriteText(value);
                    }
                }
            }
        }

        private SCR mainScr;

        public Program()
        {
            InitAll();
        }

        private void InitAll()
        {
            InitScreen();
        }

        private void InitScreen()
        {
            mainScr = new SCR(GridTerminalSystem, "TS_disp5");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != "ray")
            {
                mainScr.Text = argument;
                return;
            }

            sDebug = "";
            Raycast();
            mainScr.Text = sDebug;
        }

        private string sDebug;

        public void Raycast()
        {
            IMyCameraBlock camera = GridTerminalSystem.GetBlockWithName("MyCameraNa") as IMyCameraBlock;

            if (camera != null)
            {
                // Определяем направление взгляда камеры
                Vector3D direction = camera.WorldMatrix.Forward;

                // Определяем начальную позицию (позиция камеры)
                Vector3D start = camera.GetPosition();

                // Определяем длину луча
                double distance = 10000.0; // Задайте нужное значение
                Vector3D end = start + direction * distance;

                camera.EnableRaycast = true;


                sDebug += "try hit...\nenable cast:" + camera.EnableRaycast + "\nrange:" + camera.AvailableScanRange + "\n";

                // Выполняем raycast через IMyCameraBlock
                MyDetectedEntityInfo hitInfo = camera.Raycast(camera.AvailableScanRange);


                Vector3D? hit = hitInfo.HitPosition;
                if (hit.HasValue)
                {
                    sDebug += $"Hit: {hit.Value}";
                }
                else
                {
                    sDebug += "empty";
                }
            }
            else
            {
                sDebug += "Camera not found";
            }
        }
    }
}

namespace IngameScript11.Drone1
{
    internal partial class Program : MyGridProgram
    {
        #region SETTINGS

        private const double gyroMult = 4d;
        private const double gyroAmplificationValue = 0.35f;
        private const float angleDeflection = 5f;
        private const float thrustersWorkAngle = 45f;

        #endregion

        private class SCR
        {
            public readonly string name;

            public IMyTextPanel screen;
            public IMyTextSurface surface;
            private bool isInitWithPanel;

            private string cachedText;

            public SCR(IMyGridTerminalSystem grid, string name)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
                screen.FontColor = new Color(105, 255, 187);
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyTextPanel textPanel, string name)
            {
                this.name = name;
                screen = textPanel;
                screen.FontColor = new Color(105, 255, 187);
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyCockpit cockpit, int index)
            {
                name = index.ToString();
                surface = cockpit.GetSurface(index);
                surface.FontColor = new Color(105, 255, 187);
                isInitWithPanel = false;
                Text = name;
            }

            public SCR(IMyTextSurface surface, int index)
            {
                name = index.ToString();
                this.surface = surface;
                surface.FontColor = new Color(105, 255, 187);
                isInitWithPanel = false;
                Text = name;
            }

            public string Text
            {
                get
                {
                    return cachedText;
                }
                set
                {
                    cachedText = value;
                    if (isInitWithPanel)
                    {
                        screen?.WriteText(cachedText);
                    }
                    else
                    {
                        surface?.WriteText(cachedText);
                    }
                }
            }
        }

        private class Gyro
        {
            public IMyGyro gyroRef;
            public IMyShipController ctrl;

            public Gyro(IMyGyro gyro)
            {
                gyroRef = gyro;
            }

            public Gyro(IMyGyro gyro, IMyShipController control)
            {
                gyroRef = gyro;
                ctrl = control;
            }

            public const float ToGyro = 1f / 9.55f;
            public const float FromGyro = 9.55f;

            public Vector3 Velosity
            {
                get
                {
                    return new Vector3(
                        gyroRef.Yaw * FromGyro,
                        gyroRef.Pitch * -FromGyro,
                        gyroRef.Roll * FromGyro
                    );
                }
                set
                {
                    gyroRef.Yaw = value.X * ToGyro;
                    gyroRef.Pitch = -value.Y * ToGyro;
                    gyroRef.Roll = value.Z * ToGyro;
                }
            }

        }

        private int updateCounter = 0;
        private int UpdateCounter
        {
            get
            {
                return updateCounter;
            }
            set
            {
                updateCounter = value;
                if (updateCounter > 10)
                {
                    updateCounter = 0;
                }
            }
        }

        private SCR scr;

        //SCR scrD;

        private List<IMyThrust> thrusters = new List<IMyThrust>();
        private List<Gyro> gyros = new List<Gyro>();
        private IMyShipController controller;
        private const string RID = "ACTAG_1"; //ACTAG - automatic controllable thrusters and gyroscopes

        private double Speed => velocity.Length();
        private Vector3D velocity => controller.GetShipVelocities().LinearVelocity;

        private Vector3D Pos => controller.GetPosition();
        private Vector3D Forward => controller.WorldMatrix.GetOrientation().Forward;
        private Vector3 Rotation => ConvertToEuler();

        private string SpeedStr => Speed.ToString("0.000");
        private string PosStr => V2S(Pos);

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            controller = GridTerminalSystem.GetBlockWithName("ACTAG_1_control") as IMyShipController;

            scr = new SCR(Me.GetSurface(0), 0);

            //scrD = new SCR(GridTerminalSystem, "dispD");

            GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters);
            thrusters.RemoveAll(x => !x.CustomName.Contains(RID));
            thrusters.RemoveAll(x => x.GridThrustDirection != new Vector3I(0, 0, 1));

            List<IMyGyro> gyrosTemp = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyrosTemp);
            gyrosTemp.RemoveAll(x => !x.CustomName.Contains(RID));
            gyros = gyrosTemp.Select(x => new Gyro(x)).ToList();
        }

        private Random rnd = new Random(DateTime.Now.Millisecond);
        private DateTime prevTime = DateTime.Now;

        private bool isTargeting = false;
        private Vector3 targetPos = new Vector3(0, 0, 0);

        private double targetDistance => (targetPos - Pos).Length();
        private Vector3 targetForward => (targetPos - Pos).Normalized();
        private double targetAngleDegrees => MathHelper.ToDegrees(targetAngleRadians);
        private double targetAngleRadians => SignedAngle(Forward, targetForward, controller.WorldMatrix.Up);

        private float EngineVelocity
        {
            get
            {
                return thrusters[0].ThrustOverridePercentage;
            }
            set
            {
                foreach (IMyThrust x in thrusters)
                {
                    x.ThrustOverridePercentage = value;
                }
            }
        }

        private string sDebugOut = "";

        private bool isStopped = false;

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "start")
            {
                MakeStart();
            }

            if (isStopped)
            {
                return;
            }

            if (argument == "stop")
            {
                MakeStop();
            }

            sDebugOut = "";
            UpdateCounter++;
            double deltaTime = (DateTime.Now - prevTime).TotalSeconds;
            Update(deltaTime);
            scr.Text = sDebugOut;

            //scrD.Text = sDebugOut;
        }

        private void MakeStop()
        {
            EngineVelocity = 0;
            GyrosVelosity = new Vector3(0, 0, 0);
            foreach (Gyro x in gyros)
            {
                x.gyroRef.GyroOverride = false;
            }
            isStopped = true;
        }

        private void MakeStart()
        {
            EngineVelocity = 0;
            GyrosVelosity = new Vector3(0, 0, 0);
            foreach (Gyro x in gyros)
            {
                x.gyroRef.GyroOverride = true;
            }
            isStopped = false;
        }

        public void Update(double deltaTime)
        {
            UpdateRotation();
            UpdateThrusters();

            if (!isTargeting)
            {
                SetLookAtPoint(Pos + new Vector3(10 * RndOneMinusOne, 10 * RndOneMinusOne, 10 * RndOneMinusOne) - Forward * 20);
            }
        }

        private void UpdateThrusters()
        {
            sDebugOut += "thrusters activity : " + EngineVelocity * 100 + "%\n";
            foreach (IMyThrust x in thrusters)
            {
                sDebugOut += V2S(x.GridThrustDirection) + "\n";
            }
            if (Math.Abs(targetAngleDegrees) < thrustersWorkAngle)
            {
                EngineVelocity = 1;
            }
            else
            {
                EngineVelocity = 0;
            }
        }

        private void UpdateRotation()
        {

            if (!isTargeting)
            {
                return;
            }
            Vector3D vec = GetTargetAngles();
            sDebugOut += $"angle:{targetAngleDegrees}\ngv={V2S(vec)}\ncrF {V2S(Forward)}\ntrF {V2S(targetForward)}";
            if (Math.Abs(targetAngleDegrees) < angleDeflection && targetDistance < 1)
            {
                GyrosVelosity = new Vector3();
                isTargeting = false;
                return;
            }
            GyrosVelosity = vec * gyroMult;
            sDebugOut += $"\n\nvel {V2S(GyrosVelosity)}\nrot{V2S(Rotation)}\n\n";
        }

        private Vector3D GetTargetAngles()
        {
            Vector3D t = targetPos - Pos;

            Vector3D fow = controller.WorldMatrix.Forward;
            Vector3D up = controller.WorldMatrix.Up;
            Vector3D left = controller.WorldMatrix.Left;

            Vector3D lTemp = Vector3D.Reject(t, up).Normalized();
            double yaw = Math.Acos(Vector3D.Dot(left, lTemp)) - Math.PI / 2;
            yaw = Math.Acos(Vector3D.Dot(lTemp, fow)) > Math.PI / 2 ? (Math.PI - Math.Abs(yaw)) * Math.Sign(yaw) : yaw;
            yaw = Math.Pow(Math.Abs(yaw), gyroAmplificationValue) * Math.Sign(yaw);

            Vector3D uTemp = Vector3D.Reject(t, left).Normalized();
            double pitch = Math.Acos(Vector3D.Dot(up, uTemp)) - Math.PI / 2;
            pitch = Math.Acos(Vector3D.Dot(uTemp, fow)) > Math.PI / 2 ? (Math.PI - Math.Abs(pitch)) * Math.Sign(pitch) : pitch;
            pitch = Math.Pow(Math.Abs(pitch), gyroAmplificationValue) * Math.Sign(pitch);

            #region old

            //Vector3D vrej = Vector3D.Reject(velocity.Normalized(), t);
            //Vector3D correction = (t - vrej * 2).Normalized();

            //double pitch = Vector3D.Dot(Gup, (Vector3D.Reject(correction, Gleft)).Normalized());
            //pitch = Math.Acos(pitch) - Math.PI / 2;
            //double yaw = Vector3D.Dot(Gleft, (Vector3D.Reject(correction, Gup)).Normalized());
            //yaw = Math.Acos(yaw) - Math.PI / 2;

            #endregion
            return new Vector3D(yaw, -pitch, 0);
        }
        public Vector3 ConvertToEuler()
        {
            Matrix m = controller.WorldMatrix;
            Vector3 v = new Vector3();
            Matrix.GetEulerAnglesXYZ(ref m, out v);
            v.X = MathHelper.ToDegrees(v.X);
            v.Y = MathHelper.ToDegrees(v.Y);
            v.Z = MathHelper.ToDegrees(v.Z);
            return v;
        }
        public static double SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            double angle = Vector3.Angle(from, to);
            double sign = Math.Sign(Vector3.Dot(axis, Vector3.Cross(from, to)));
            return angle * sign;
        }

        public void SetLookAtPoint(Vector3 target)
        {
            targetPos = target;
            isTargeting = true;
        }

        public bool Raycast()
        {
            IMyCameraBlock camera = GridTerminalSystem.GetBlockWithName("ACTAG_1_cam") as IMyCameraBlock;

            if (camera != null)
            {
                Vector3D direction = camera.WorldMatrix.Forward;
                Vector3D start = camera.GetPosition();
                camera.EnableRaycast = true;
                MyDetectedEntityInfo hitInfo = camera.Raycast(camera.AvailableScanRange);

                Vector3D? hit = hitInfo.HitPosition;
                if (hit.HasValue)
                {
                    if (hitInfo.Type == MyDetectedEntityType.CharacterHuman || hitInfo.Type == MyDetectedEntityType.CharacterOther)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public float RndOneMinusOne => (float)rnd.NextDouble() * 2f - 1f;

        public Vector3 GyrosVelosity
        {
            get
            {
                return gyros[0].Velosity;
            }
            set
            {
                foreach (Gyro gyro in gyros)
                {
                    gyro.Velosity = value;
                }
            }
        }

        public string V2S(Vector3 v)
        {
            return $"x:{v.X:0.000},y:{v.Y:0.000},z:{v.Z:0.000}";
        }

        public Vector3 S2V(string str)
        {
            string[] parts = str.Split(',');
            Dictionary<string, float> vectorValues = new Dictionary<string, float>();
            foreach (string part in parts)
            {
                string[] keyValue = part.Split(':');
                if (keyValue.Length == 2)
                {
                    string key = keyValue[0].Trim();
                    try
                    {
                        vectorValues[key] = float.Parse(keyValue[1].Trim());
                    }
                    catch { }
                }
            }
            return new Vector3(
                vectorValues.ContainsKey("x") ? vectorValues["x"] : 0,
                vectorValues.ContainsKey("y") ? vectorValues["y"] : 0,
                vectorValues.ContainsKey("z") ? vectorValues["z"] : 0
            );
        }



    }
}

namespace IngameScript.Radar
{
    internal partial class Program : MyGridProgram
    {
        #region SETTINGS

        private const string cameraName = "radCam";

        #endregion

        private class SCR
        {
            public readonly string name;

            public IMyTextPanel screen;
            public IMyTextSurface surface;
            private bool isInitWithPanel;

            private string cachedText;

            public SCR(IMyGridTerminalSystem grid, string name)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
                screen.FontColor = new Color(105, 255, 187);
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyTextPanel textPanel, string name)
            {
                this.name = name;
                screen = textPanel;
                screen.FontColor = new Color(105, 255, 187);
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyCockpit cockpit, int index)
            {
                name = index.ToString();
                surface = cockpit.GetSurface(index);
                surface.FontColor = new Color(105, 255, 187);
                isInitWithPanel = false;
                Text = name;
            }

            public SCR(IMyTextSurface surface, int index)
            {
                name = index.ToString();
                this.surface = surface;
                surface.FontColor = new Color(105, 255, 187);
                isInitWithPanel = false;
                Text = name;
            }

            public string Text
            {
                get
                {
                    return cachedText;
                }
                set
                {
                    cachedText = value;
                    if (isInitWithPanel)
                    {
                        screen?.WriteText(cachedText);
                    }
                    else
                    {
                        surface?.WriteText(cachedText);
                    }
                }
            }
        }

        private class Cycled<T>
        {
            private int iterator = 0;

            public T[] val;

            public Cycled(T[] values)
            {
                val = values;
            }

            public Cycled(List<T> values)
            {
                val = values.ToArray();
            }

            public T this[int i] => val[i];

            public T Next()
            {
                return val[++iterator % val.Length];
            }

            public void ActNext(Action<T> act)
            {
                act(val[++iterator % val.Length]);
            }

            public void ActAll(Action<T> act)
            {
                foreach (T t in val)
                {
                    act(t);
                }
            }
        }

        private class Commands
        {
            public enum CmdList
            {
                command1,
                command2,
                command3
            }

            public Dictionary<string, CmdList> commands = new Dictionary<string, CmdList>()
            {
                { "command1", CmdList.command1 },
                { "command2", CmdList.command2 },
                { "command3", CmdList.command3 }
            };
        }



        private SCR scrOut;
        private SCR scrIn;
        private Cycled<IMyCameraBlock> cams;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            scrOut = new SCR(Me.GetSurface(0), 0);

            List<IMyCameraBlock> camsTemp = new List<IMyCameraBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>(camsTemp);
            camsTemp.RemoveAll(x => !x.CustomName.Contains(cameraName));
            cams = new Cycled<IMyCameraBlock>(camsTemp);
            cams.ActAll(x => { x.EnableRaycast = true; });
        }

        public void Main(string argument, UpdateType updateSource)
        {

            if (string.IsNullOrEmpty(argument))
            {
                return;
            }

            string[] commands = argument.Split(',');

        }

        public bool TRaycastForward(IMyCameraBlock camera, out MyDetectedEntityInfo hitInfo)
        {
            hitInfo = camera.Raycast(camera.AvailableScanRange);
            return hitInfo.Type != MyDetectedEntityType.None;
        }

    }
}

namespace IngameScript.Menu
{

    internal partial class Program : MyGridProgram
    {
        #region SETTINGS

        private const string cameraName = "radCam";

        #endregion
        private class SCR
        {
            public readonly string name;

            public IMyTextPanel screen;
            public IMyTextSurface surface;
            private bool isInitWithPanel;

            private string cachedText;

            public SCR(IMyGridTerminalSystem grid, string name)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
                screen.FontColor = new Color(105, 255, 187);
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyTextPanel textPanel, string name)
            {
                this.name = name;
                screen = textPanel;
                screen.FontColor = new Color(105, 255, 187);
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyCockpit cockpit, int index)
            {
                name = index.ToString();
                surface = cockpit.GetSurface(index);
                surface.FontColor = new Color(105, 255, 187);
                isInitWithPanel = false;
                Text = name;
            }

            public SCR(IMyTextSurface surface, int index)
            {
                name = index.ToString();
                this.surface = surface;
                surface.FontColor = new Color(105, 255, 187);
                isInitWithPanel = false;
                Text = name;
            }

            public string Text
            {
                get
                {
                    return cachedText;
                }
                set
                {
                    cachedText = value;
                    if (isInitWithPanel)
                    {
                        screen?.WriteText(cachedText);
                    }
                    else
                    {
                        surface?.WriteText(cachedText);
                    }
                }
            }
        }


    }
}

namespace IngameScript.Spot
{
    internal partial class Program : MyGridProgram
    {
        #region SETTINGS

        private const string shipName = "spot";

        #endregion

        private class SCR
        {
            public readonly string name;

            public IMyTextPanel screen;
            public IMyTextSurface surface;
            private bool isInitWithPanel;

            private string cachedText;

            private static readonly Color favoriteColor = new Color(105, 255, 187);

            public SCR(IMyGridTerminalSystem grid, string name, ContentType content = ContentType.TEXT_AND_IMAGE)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
                screen.ContentType = content;
                screen.FontColor = favoriteColor;
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyTextPanel textPanel, string name, ContentType content = ContentType.TEXT_AND_IMAGE)
            {
                this.name = name;
                screen = textPanel;
                screen.ContentType = content;
                screen.FontColor = favoriteColor;
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyCockpit cockpit, int index, ContentType content = ContentType.TEXT_AND_IMAGE)
            {
                name = index.ToString();
                surface = cockpit.GetSurface(index);
                surface.ContentType = content;
                surface.FontColor = favoriteColor;
                isInitWithPanel = false;
                Text = name;
            }

            public SCR(IMyTextSurface surface, int index, ContentType content = ContentType.TEXT_AND_IMAGE)
            {
                name = index.ToString();
                this.surface = surface;
                this.surface.ContentType = content;
                this.surface.FontColor = favoriteColor;
                isInitWithPanel = false;
                Text = name;
            }

            public Color Color
            {
                get
                {
                    return isInitWithPanel ? screen.FontColor : surface.FontColor;
                }
                set
                {
                    if (isInitWithPanel)
                    {
                        screen.FontColor = value;
                    }
                    else
                    {
                        surface.FontColor = value;
                    }
                }
            }

            public string Text
            {
                get
                {
                    return cachedText;
                }
                set
                {
                    cachedText = value;
                    if (isInitWithPanel)
                    {
                        screen?.WriteText(cachedText);
                    }
                    else
                    {
                        surface?.WriteText(cachedText);
                    }
                }
            }
        }

        private SCR mainScr;
        private SCR addScr;
        private SCR addScr2;

        private string mainText = "";
        private List<IMyGasTank> tanks = new List<IMyGasTank>();

        private double filledHypdroPrev = 0;
        private double filledOxyPrev = 0;

        private IMyCameraBlock camera;

        private Color yellow = new Color(255, 156, 2);
        private Color green = new Color(105, 255, 187);

        private int counter = 0;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            mainScr = new SCR(GridTerminalSystem.GetBlockWithName("spotCocpit") as IMyCockpit, 0);
            addScr = new SCR(GridTerminalSystem.GetBlockWithName("spotCocpit") as IMyCockpit, 1);
            addScr2 = new SCR(GridTerminalSystem.GetBlockWithName("spotCocpit") as IMyCockpit, 2);

            camera = GridTerminalSystem.GetBlockWithName("spotCamDist") as IMyCameraBlock;
            camera.EnableRaycast = true;

            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(tanks);
            tanks.RemoveAll(x => !x.CustomName.Contains(shipName));
        }

        public void Main(string argument, UpdateType updateSource)
        {
            double filledHydro = tanks.Where(x => x.BlockDefinition.SubtypeId.Contains("Hydro")).Average(x => x.FilledRatio);
            double filledOxy = tanks.Where(x => !x.BlockDefinition.SubtypeId.Contains("Hydro")).Average(x => x.FilledRatio);
            mainText = $"Hydro:{filledHydro * 100:0.00}%\nH[{(filledHydro - filledHypdroPrev) * 100:0.0000}]\nOxy:{filledOxy * 100:0.00}%";

            filledHypdroPrev = filledHydro;
            filledOxyPrev = filledOxy;
            mainScr.Text = mainText;

            MyDetectedEntityInfo hitInfo;
            double scanRange;
            if (TRaycastForward(camera, out hitInfo, out scanRange))
            {
                addScr.Text = $"{Vector3.Distance(Me.GetPosition(), hitInfo.HitPosition.Value):0.0}m\n{hitInfo.Type}";
                addScr.Color = yellow;
            }
            else
            {
                addScr.Text = $"{scanRange:0.0}m";
                addScr.Color = green;
            }

            addScr2.Text = counter.ToString();
            counter = ++counter % 10;
        }

        public bool TRaycastForward(IMyCameraBlock camera, out MyDetectedEntityInfo hitInfo, out double scanRange)
        {
            scanRange = camera.AvailableScanRange;
            hitInfo = camera.Raycast(camera.AvailableScanRange);
            return hitInfo.Type != MyDetectedEntityType.None;
        }

    }
}

namespace IngameScript.Lift
{
    internal partial class Program : MyGridProgram
    {
        #region SETTINGS

        private const string shipName = "lift";

        #endregion

        private class SCR
        {
            public readonly string name;

            public IMyTextPanel screen;
            public IMyTextSurface surface;
            private bool isInitWithPanel;

            private string cachedText;

            private static readonly Color favoriteColor = new Color(105, 255, 187);

            public SCR(IMyGridTerminalSystem grid, string name, ContentType content = ContentType.TEXT_AND_IMAGE)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
                screen.ContentType = content;
                screen.FontColor = favoriteColor;
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyTextPanel textPanel, string name, ContentType content = ContentType.TEXT_AND_IMAGE)
            {
                this.name = name;
                screen = textPanel;
                screen.ContentType = content;
                screen.FontColor = favoriteColor;
                isInitWithPanel = true;
                Text = name;
            }

            public SCR(IMyCockpit cockpit, int index, ContentType content = ContentType.TEXT_AND_IMAGE)
            {
                name = index.ToString();
                surface = cockpit.GetSurface(index);
                surface.ContentType = content;
                surface.FontColor = favoriteColor;
                isInitWithPanel = false;
                Text = name;
            }

            public SCR(IMyTextSurface surface, int index, ContentType content = ContentType.TEXT_AND_IMAGE)
            {
                name = index.ToString();
                this.surface = surface;
                this.surface.ContentType = content;
                this.surface.FontColor = favoriteColor;
                isInitWithPanel = false;
                Text = name;
            }

            public Color Color
            {
                get
                {
                    return isInitWithPanel ? screen.FontColor : surface.FontColor;
                }
                set
                {
                    if (isInitWithPanel)
                    {
                        screen.FontColor = value;
                    }
                    else
                    {
                        surface.FontColor = value;
                    }
                }
            }

            public string Text
            {
                get
                {
                    return cachedText;
                }
                set
                {
                    cachedText = value;
                    if (isInitWithPanel)
                    {
                        screen?.WriteText(cachedText);
                    }
                    else
                    {
                        surface?.WriteText(cachedText);
                    }
                }
            }
        }

        private SCR mainScr;

        private string mainText = "";

        private Color yellow = new Color(255, 156, 2);
        private Color green = new Color(105, 255, 187);

        private List<IMyMotorSuspension> wheels = new List<IMyMotorSuspension>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            mainScr = new SCR(GridTerminalSystem, "liftDisp");

            GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(wheels);
            wheels.RemoveAll(x => !x.CustomName.Contains(shipName));
        }

        private float propulsion = 0;

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "+")
            {
                propulsion += 0.1f;
                propulsion = MathHelper.Clamp(propulsion, -1, 1);
                foreach (IMyMotorSuspension wheel in wheels)
                {
                    wheel.PropulsionOverride = propulsion;
                }
            }
            if (argument == "-")
            {
                propulsion -= 0.1f;
                propulsion = MathHelper.Clamp(propulsion, -1, 1);
                foreach (IMyMotorSuspension wheel in wheels)
                {
                    wheel.PropulsionOverride = propulsion;
                }
            }

            mainText = wheels.Average(x => x.PropulsionOverride).ToString("0.00");
            mainScr.Text = mainText;
        }
    }
}