using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Sandbox.Game.EntityComponents;
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
using VRageMath;

namespace SomeProg
{
    internal partial class Program : MyGridProgram
    {

        private List<IMyRefinery> refinery = new List<IMyRefinery>();
        private Dictionary<string, double> PrevItemsToRefine = new Dictionary<string, double>();
        private Dictionary<string, double> PrevItemsRefined = new Dictionary<string, double>();
        private IMyTextPanel screen;
        private IMyTextPanel screen2;
        public Program()
        {
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("stationMainDisp4");
            screen2 = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("stationMainDisp5");
            GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refinery);
            refinery.RemoveAll(x => !x.CustomName.Contains("StationRefinery"));
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Dictionary<string, double> itemsToRefine = new Dictionary<string, double>();
            Dictionary<string, double> itemsRefined = new Dictionary<string, double>();

            for (int i = 0; i < refinery.Count; i++)
            {
                IMyInventory inInvent = refinery[i].InputInventory;

                List<MyInventoryItem> inventoryItem = new List<MyInventoryItem>();
                inInvent.GetItems(inventoryItem);
                for (int j = 0; j < inventoryItem.Count; j++)
                {
                    string key = inventoryItem[j].Type.TypeId.Substring(16) + inventoryItem[j].Type.SubtypeId;
                    if (!itemsToRefine.ContainsKey(key))
                    {
                        itemsToRefine.Add(key, 0);
                    }
                    itemsToRefine[key] += (double)inventoryItem[j].Amount;
                }

                IMyInventory outInvent = refinery[i].OutputInventory;
                inventoryItem = new List<MyInventoryItem>();
                outInvent.GetItems(inventoryItem);
                for (int j = 0; j < inventoryItem.Count; j++)
                {
                    string key = inventoryItem[j].Type.TypeId.Substring(16) + inventoryItem[j].Type.SubtypeId;
                    if (!itemsRefined.ContainsKey(key))
                    {
                        itemsRefined.Add(key, 0);
                    }
                    itemsRefined[key] += (double)inventoryItem[j].Amount;
                }

            }

            string sItemsToRefine = "Items to refine :\n";
            foreach (KeyValuePair<string, double> item in itemsToRefine)
            {
                sItemsToRefine += item.Key + " : " + CountToString(item.Value) + "g\n";
            }

            string sItemsRefined = "Items refined :\n";
            foreach (KeyValuePair<string, double> item in itemsRefined)
            {
                sItemsRefined += item.Key + " : " + CountToString(item.Value) + "\n";
            }

            string sRefineSpeed = "Refine speed per second :\n";
            foreach (KeyValuePair<string, double> item in itemsToRefine)
            {
                if (PrevItemsToRefine.ContainsKey(item.Key))
                {
                    sRefineSpeed += item.Key + " : " + CountToString((PrevItemsToRefine[item.Key] - item.Value) * 0.6f) + "g/s\n";
                }
            }

            PrevItemsToRefine = itemsToRefine;

            string sIngotSpeed = "Get ingots per second :\n";
            foreach (KeyValuePair<string, double> item in itemsRefined)
            {
                if (PrevItemsRefined.ContainsKey(item.Key))
                {
                    sIngotSpeed += item.Key + " : " + CountToString((item.Value - PrevItemsRefined[item.Key]) * 0.6f) + "/s\n";
                }
            }
            PrevItemsRefined = itemsRefined;

            screen.WriteText(sItemsToRefine + "\n\n" + sItemsRefined);
            screen2.WriteText(sRefineSpeed + "\n\n" + sIngotSpeed);
        }
        private string CountToString(double count)
        {
            if (count >= 1000000000)
            {
                return (count / 1000000000).ToString("0.000") + "B";
            }

            if (count >= 1000000)
            {
                return (count / 1000000).ToString("0.000") + "M";
            }

            if (count >= 1000)
            {
                return (count / 1000).ToString("0.000") + "k";
            }

            return count.ToString("0.000");
        }

    }
}

namespace IngameScript7GasGenerators
{
    internal partial class Program : MyGridProgram
    {
        private IMyTextPanel screen;
        private List<IMyGasGenerator> containers = new List<IMyGasGenerator>();

        public Program()
        {
            Init();
        }

        public void Init()
        {
            GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(containers);
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("stationMainDisp4");
            containers.RemoveAll(x => !x.CustomName.Contains("StationGasGenerator"));
        }

        public void Main(string argument, UpdateType updateSource)
        {
            string s = "";
            int iterator = 0;
            for (int i = 0; i < containers.Count; i++)
            {
                containers[i].CustomName = "StationGasGenerator" + iterator.ToString();
                iterator++;
            }

            screen.WriteText(s);
        }
    }
}

namespace IngameScript6Battery
{
    internal partial class Program : MyGridProgram
    {

        private IMyTextPanel screen;
        private List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();

        public Program()
        {
            Init();
        }

        public void Init()
        {
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("stationMainDisp1");
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);

            //batteries.RemoveAll(x => !x.CustomName.Contains("StationBattery"));
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "r")
            {
                Init();
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

            string strVolume = "Energy : " + CountToString(Sum * 1000000) + "wh / " + CountToString(Max * 1000000) + "wh";
            string strVolumePersent = "Energy percent : " + (Sum / Max * 100).ToString("0.0") + "%";
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

            screen.WriteText(strVolume + "\n" + strVolumePersent + "\n" + InOut);
        }

        private string CountToString(double count)
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

    }
}

namespace IngameScript5Counter
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
        private IMyTextPanel shop;
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        private List<IMyGasTank> gasHydro = new List<IMyGasTank>();

        public Program()
        {
            Init();
        }

        public void Init()
        {
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("stationMainDisp1");
            screen2 = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("stationMainBig1");
            screen3 = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("stationMainBig2");
            shop = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("shop");

            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(containers);
            containers.RemoveAll(x => !x.CustomName.Contains("StationCargoCont"));

            List<IMyGasTank> gas = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(gas);
            gas.RemoveAll(x => !x.CustomName.Contains("StationGasHydro"));
            for (int i = 0; i < gas.Count; i++)
            {
                if (GetTankType(gas[i]) == TankType.hydro)
                {
                    gasHydro.Add(gas[i]);
                }
            }

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

            string strVolume = "Volume absolute : " + ValueToString(volumeSum * 1000) + "l / " + ValueToString(volumeMax * 1000) + "l";
            string strVolumePersent = "Volume percent : " + (volumeSum / volumeMax * 100).ToString("0.0") + "%";

            string ComponentsString = "Components:\n";
            foreach (KeyValuePair<string, double> item in components)
            {
                ComponentsString += item.Key + ":" + CountToString(item.Value) + "\n";
            }
            string OtherString = "Ores and ingots:\n";
            foreach (KeyValuePair<string, double> item in others)
            {
                OtherString += item.Key + ":" + CountToString(item.Value) + "\n";
            }
            string shopString = "0 $";
            if (others.ContainsKey("SpaceCredit"))
            {
                shopString = CountToString(others["SpaceCredit"]) + " $";
            }

            double HydroCapacity = 0;
            double HydroSum = 0;
            for (int i = 0; i < gasHydro.Count; i++)
            {
                HydroCapacity += gasHydro[i].Capacity;
                HydroSum += gasHydro[i].FilledRatio * gasHydro[i].Capacity;
            }
            string hydroState = "hydro : " + CountToString(HydroSum) + "/" + CountToString(HydroCapacity) +
                                "\nin tanks : " + (HydroSum / HydroCapacity * 100).ToString("0.00000") + "%";

            screen2.WriteText(ComponentsString);
            screen3.WriteText(OtherString);
            shop.WriteText(shopString);
            screen.WriteText(strVolume + "\n" + strVolumePersent + "\n\n" + hydroState);
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

namespace IngameScriptLightAndrey
{

    internal partial class Program : MyGridProgram
    {

        private IMyLightingBlock light1;
        private IMyLightingBlock light2;

        //private IMyTextPanel screen;

        private int Timer;

        private const int TickInSecond = 60;

        private int TimeToBlink = 3 * TickInSecond;
        private int TimeBlick = (int)(0.5f * TickInSecond);

        public Program()
        {
            Init();
        }

        public void Init()
        {
            //screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("screen");
            light1 = (IMyLightingBlock)GridTerminalSystem.GetBlockWithName("OuterLight1");
            light2 = (IMyLightingBlock)GridTerminalSystem.GetBlockWithName("OuterLight2");
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Timer++;
            if (Timer >= TimeToBlink)
            {
                light1.Enabled = true;
                light2.Enabled = true;
                float Intensity = (float)(Math.Sin((Timer - TimeToBlink) / (float)TimeBlick * Math.PI) * 10);

                //screen.WriteText(Intensity.ToString("0.000"));
                light1.Intensity = Intensity;
                light2.Intensity = Intensity;
            }
            if (Timer >= TimeToBlink + TimeBlick)
            {
                light1.Enabled = false;
                light2.Enabled = false;
                Timer = 0;
            }
        }

    }
}

namespace IngameScript3_1
{
    internal partial class Program : MyGridProgram
    {
        private enum TankType
        {
            oxy,
            hydro,
            none
        }

        //private IMyTextPanel screen;

        public void Main(string argument, UpdateType updateSource)
        {
            List<IMyGasTank> gas = new List<IMyGasTank>();
            List<IMyGasTank> gasOxy = new List<IMyGasTank>();
            List<IMyGasTank> gasHydro = new List<IMyGasTank>();
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(gas);

            //gas.RemoveAll(x => !x.CustomName.Contains("Mining"));
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

            //screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("MiningDisplayT");
            //string s = "";

            int iterator = 0;
            for (int i = 0; i < gasOxy.Count; i++)
            {
                gasOxy[i].CustomName = "StationGasOxy" + iterator.ToString();
                iterator++;
            }
            for (int i = 0; i < gasHydro.Count; i++)
            {
                gasHydro[i].CustomName = "StationGasHydro" + iterator.ToString();
                iterator++;
            }

            //screen.WriteText(s);
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
    }
}

namespace IngameScript4Ventilation
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
        private List<IMyGasTank> gas = new List<IMyGasTank>();
        private IMyDoor door1;
        private IMyDoor door2;
        private IMyAirVent vent;
        private List<IMyOxygenFarm> farms = new List<IMyOxygenFarm>();

        private int TimeToOpen1 = 0;
        private int TimeToOpen2 = 0;

        public Program()
        {
            Init();
        }

        public void Init()
        {
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("stationVentilationDisp");
            screen2 = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("stationVentilationDisp2");
            vent = (IMyAirVent)GridTerminalSystem.GetBlockWithName("ventStation");
            door1 = (IMyDoor)GridTerminalSystem.GetBlockWithName("MainDoor1");
            door2 = (IMyDoor)GridTerminalSystem.GetBlockWithName("MainDoor2");
            GridTerminalSystem.GetBlocksOfType<IMyGasTank>(gas);
            gas.RemoveAll(x => GetTankType(x) != TankType.oxy);

            GridTerminalSystem.GetBlocksOfType<IMyOxygenFarm>(farms);

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "r")
            {
                Init();
            }
            CheckDoor();

            double capacity = 0;
            double sum = 0;
            for (int i = 0; i < gas.Count; i++)
            {
                capacity += gas[i].Capacity;
                sum += gas[i].FilledRatio * gas[i].Capacity;
            }
            string oxyState = "oxygen : " + CountToString(sum) + "/" + CountToString(capacity) +
                              "\nin tanks : " + (sum / capacity * 100).ToString("0.00") +
                              "%\nin room : " + (vent.GetOxygenLevel() * 100).ToString("0.0000") + "%";

            string doorState = "door1 : " + DoorOpenRatioToString(door1) +
                               "\ndoor2 : " + DoorOpenRatioToString(door2);

            double farmSum = 0;
            for (int i = 0; i < farms.Count; i++)
            {
                farmSum += farms[i].GetOutput();
            }
            string farmString = "oxy generation : " + farmSum.ToString("0.0") + "l/m";

            screen.WriteText(oxyState + "\n" + farmString + "\n" + doorState);
            screen2.WriteText(doorState + "\n" + TimeToOpen1 + "   " + TimeToOpen2);
        }


        private void CheckDoor()
        {
            if (IsOpenDoor(door1))
            {
                TimeToOpen1 = 120;
            }
            if (IsOpenDoor(door2))
            {
                TimeToOpen2 = 120;
            }
            door2.Enabled = !IsOpenDoor(door1) && TimeToOpen1 == 0;
            door1.Enabled = !IsOpenDoor(door2) && TimeToOpen2 == 0;
            if (TimeToOpen1 > 0)
            {
                TimeToOpen1--;
            }
            if (TimeToOpen2 > 0)
            {
                TimeToOpen2--;
            }
        }

        private bool IsOpenDoor(IMyDoor door)
        {
            if (door.OpenRatio == 0)
            {
                return false;
            }
            return true;

        }

        private string DoorOpenRatioToString(IMyDoor door)
        {
            if (door.OpenRatio == 1)
            {
                return "open";
            }
            if (door.OpenRatio == 0)
            {
                return "close";
            }
            return "opening.. " + ((int)(door.OpenRatio * 100)).ToString() + "%";
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

        private string CountToString(double count)
        {
            if (count >= 1000000000)
            {
                return (count / 1000000000).ToString("0.0") + "b";
            }

            if (count >= 1000000)
            {
                return (count / 1000000).ToString("0.0") + "m";
            }

            if (count >= 1000)
            {
                return (count / 1000).ToString("0.0") + "k";
            }

            return count.ToString("0.0");
        }

    }
}

namespace IngameScript3
{
    internal partial class Program : MyGridProgram
    {
        private IMyTextPanel screen;
        private int iter = 0;
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            List<IMyThrust> containers = new List<IMyThrust>();
            IMyTerminalBlock kp = GridTerminalSystem.GetBlockWithName("KPMining");
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(containers);
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("MiningDisplayT");
            string s = "kp : " + kp.WorldMatrix.Up;
            int finded = 0;
            int iterator = 0;
            for (int i = 0; i < containers.Count; i++)
            {
                if (i + iter < containers.Count)
                {
                    if (!containers[i + iter].IsFunctional)
                    {
                        finded = i + iter;
                        break;
                    }
                }
                iterator++;
            }
            for (int i = 0; i < containers.Count; i++)
            {
                s += "\n";
                if (i + iter < containers.Count)
                {
                    s += containers[i + iter].WorldMatrix.Up;
                    if (containers[finded].WorldMatrix.Up == containers[i + iter].WorldMatrix.Up)
                    {
                        containers[i + iter].CustomName = "MiningEngineUp" + iterator.ToString();
                        s += "___UP";
                    }
                }
                iterator++;
            }


            iter++;
            if (iter > containers.Count + 2)
            {
                iter = 0;
            }
            screen.WriteText(s);
        }

    }
}

namespace IngameScript2
{
    internal partial class Program : MyGridProgram
    {

        private class Act
        {
            public Act nextAct;
            public double nextActTime;
            public Action thisAction;
        }

        private static int tickInSecond = 60;
        public double Now => new TimeSpan(DateTime.Now.Ticks).TotalSeconds;

        private int tick = 0;
        private IMyPistonBase piston;
        private Act currentAct;
        private IMyTextPanel screen;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            piston = (IMyPistonBase)GridTerminalSystem.GetBlockWithName("pistonLift");
            screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("displayLift");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (tick > tickInSecond)
            {
                tick = 0;
            }
            tick++;
            Update(argument);
        }

        public void Update(string arg)
        {
            if (!string.IsNullOrEmpty(arg))
            {
                if (arg == "1") //open
                {
                    Act act1 = new Act() //open
                    {
                        nextActTime = Now + 2,
                        thisAction = () =>
                        {
                            piston.Velocity = 2;
                            screen.WriteText("open\n" + Now);
                        }
                    };
                    Act act2 = new Act() //wait
                    {
                        nextActTime = Now + 4,
                        thisAction = () =>
                        {
                            piston.Velocity = 0;
                            screen.WriteText("wait\n" + Now);
                        }
                    };
                    Act act3 = new Act() //close
                    {
                        thisAction = () =>
                        {
                            piston.Velocity = -2;
                            screen.WriteText("close\n" + Now);
                        }
                    };
                    act1.nextAct = act2;
                    act2.nextAct = act3;
                    currentAct = act1;
                }
                if (arg == "2") //open with delay
                {
                    Act act0 = new Act() //wait
                    {
                        nextActTime = Now + 2,
                        thisAction = () =>
                        {
                            piston.Velocity = 0;
                            screen.WriteText("wait\n" + Now);
                        }
                    };
                    Act act1 = new Act() //open
                    {
                        nextActTime = Now + 4,
                        thisAction = () =>
                        {
                            piston.Velocity = 2;
                            screen.WriteText("open\n" + Now);
                        }
                    };
                    Act act2 = new Act() //wait
                    {
                        nextActTime = Now + 6,
                        thisAction = () =>
                        {
                            piston.Velocity = 0;
                            screen.WriteText("wait\n" + Now);
                        }
                    };
                    Act act3 = new Act() //close
                    {
                        thisAction = () =>
                        {
                            piston.Velocity = -2;
                            screen.WriteText("close\n" + Now);
                        }
                    };
                    act0.nextAct = act1;
                    act1.nextAct = act2;
                    act2.nextAct = act3;
                    currentAct = act0;
                }
            }

            if (currentAct != null)
            {
                currentAct.thisAction?.Invoke();
                if (currentAct.nextActTime < Now)
                {
                    currentAct = currentAct.nextAct;
                }
            }

        }

    }
}

namespace IngameScript2.ServerScriptsMainShip
{
    internal partial class Program : MyGridProgram
    {
        private class SCR
        {
            public IMyTextPanel screen;
            public readonly string name;

            public SCR(IMyGridTerminalSystem grid, string name)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
            }

            public string Text
            {
                get
                {
                    return screen?.GetText();
                }
                set
                {
                    screen?.WriteText(value);
                }
            }
        }

        private DateTime TimeNow => DateTime.Now;
        private DateTime PrevTime;
        private TimeSpan DeltaTime => TimeNow - PrevTime;

        #region screens

        private List<SCR> screens = new List<SCR>();
        private string[] screensNames = new string[] { "disp1", "disp2", "disp3", "disp4", "disp5", "disp6", "disp7", "disp8" };
        private const string spaceString = "____________________________________________________________________________";
        private const int oneStringLength = 30;
        private IMyCockpit debugSeet;

        #endregion

        private List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
        private List<IMyGasGenerator> H2Henerators = new List<IMyGasGenerator>();
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        private List<IMyCargoContainer> containersForCraft = new List<IMyCargoContainer>();

        private Dictionary<string, int> componentsMinimum = new Dictionary<string, int>()
        {
            { "SmallTube", 5000 },
            { "LargeTube", 5000 },
            { "MotorComponent", 3000 },
            { "RadioCommunicationComponent", 500 },
            { "Display", 200 },
            { "GirderComponent", 1000 },
            { "BulletproofGlass", 1000 },
            { "InteriorPlate", 20000 },
            { "ConstructionComponent", 20000 },
            { "MetalGrid", 5000 },
            { "ComputerComponent", 2000 },
            { "SteelPlate", 20000 }

            //{ new MinLevel("Reactor", 1)}, // детали реактора
            //{ new MinLevel("Thrust", 1)}, // ионный ускоритель
        };

        private float addCraftValue = 0.02f;

        private Dictionary<string, MyDefinitionId> blueprints = new Dictionary<string, MyDefinitionId>()
        {
            { "SmallTube", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SmallTube") },
            { "LargeTube", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/LargeTube") },
            { "MotorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MotorComponent") },
            { "RadioCommunicationComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/RadioCommunicationComponent") },
            { "Display", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/Display") },
            { "GirderComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/GirderComponent") },
            { "BulletproofGlass", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/BulletproofGlass") },
            { "InteriorPlate", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/InteriorPlate") },
            { "ConstructionComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ConstructionComponent") },
            { "MetalGrid", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MetalGrid") },
            { "SteelPlate", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SteelPlate") },
            { "ComputerComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ComputerComponent") }

            //{ "SmallTube", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SmallTube")},
        };

        private Dictionary<string, string> blueprintsToTypes = new Dictionary<string, string>()
        {
            { "SmallTube", "SmallTube" },
            { "LargeTube", "LargeTube" },
            { "MotorComponent", "Motor" },
            { "RadioCommunicationComponent", "RadioCommunication" },
            { "Display", "Display" },
            { "GirderComponent", "Girder" },
            { "BulletproofGlass", "BulletproofGlass" },
            { "InteriorPlate", "InteriorPlate" },
            { "ConstructionComponent", "Construction" },
            { "MetalGrid", "MetalGrid" },
            { "ComputerComponent", "Computer" },
            { "SteelPlate", "SteelPlate" }
        };

        private Dictionary<string, string> typesToBlueprints = new Dictionary<string, string>()
        {
            { "SmallTube", "SmallTube" },
            { "LargeTube", "LargeTube" },
            { "Motor", "MotorComponent" },
            { "RadioCommunication", "RadioCommunicationComponent" },
            { "Display", "Display" },
            { "Girder", "GirderComponent" },
            { "BulletproofGlass", "BulletproofGlass" },
            { "InteriorPlate", "InteriorPlate" },
            { "Construction", "ConstructionComponent" },
            { "MetalGrid", "MetalGrid" },
            { "Computer", "ComputerComponent" },
            { "SteelPlate", "SteelPlate" }
        };

        private List<IMyProductionBlock> assemblers = new List<IMyProductionBlock>();

        #region jump drive

        private List<IMyJumpDrive> jumpDrives = new List<IMyJumpDrive>();
        private const string jumpDriveName = "jumpdrive";

        #endregion

        private List<IMyRefinery> refineries = new List<IMyRefinery>();
        private Dictionary<string, AverageDouble> refinesTotal = new Dictionary<string, AverageDouble>();
        private Dictionary<string, double> prevRefine = new Dictionary<string, double>();

        #region Ice

        private AverageDouble iceUsing = new AverageDouble(20);
        private double IcePrevCount;
        private DateTime PrevTimeIce;
        private TimeSpan DeltaTimeIce => TimeNow - PrevTimeIce;
        private TimeSpan fixedDeltaIce = new TimeSpan(20 * 10000000);

        #endregion

        public Program()
        {
            InitAll();
            debugSeet = (IMyCockpit)GridTerminalSystem.GetBlockWithName("debug1");
        }

        private void Debug()
        {
            string debug = "";
            foreach (IMyProductionBlock asm in assemblers)
            {
                List<MyProductionItem> q = new List<MyProductionItem>();
                debug += asm.CustomName + "\n";
            }

            debugSeet?.GetSurface(0).WriteText(debug);
        }

        private void InitAll()
        {
            InitScreens();
            InitBatteries();
            InitContainers();
            InitJumpDrive();
            InitRefinery();
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
            containersForCraft.RemoveAll(x => !x.CustomName.Contains("bigMainCont"));
            GridTerminalSystem.GetBlocksOfType<IMyProductionBlock>(assemblers);
            GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(H2Henerators);
            assemblers.RemoveAll(x => !x.CustomName.Contains("autoAsm"));
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
        }

        public void Main(string argument, UpdateType updateSource)
        {

            Debug();
            BatteriesInfo(argument);
            CargoInfo(argument);
            JumpDriveInfo(argument);
            RefineryInfo();

            PrevTime = TimeNow;
        }

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
            double AdditionalIce = 0;
            foreach (IMyGasGenerator henerator in H2Henerators)
            {
                IMyInventory invent = henerator.GetInventory();
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                invent.GetItems(items);
                foreach (MyInventoryItem item in items)
                {
                    if (item.Type.SubtypeId == "OreIce")
                    {
                        AdditionalIce += (double)item.Amount;
                    }
                }
            }
            if (AdditionalIce > 0)
            {
                if (!others.ContainsKey("OreIce"))
                {
                    others.Add("OreIce", 0);
                }
                others["OreIce"] += AdditionalIce;
            }

            if (DeltaTimeIce > fixedDeltaIce)
            {
                if (others.ContainsKey("OreIce"))
                {
                    double currentIce = others["OreIce"];
                    double iceСonsumption = IcePrevCount - currentIce;
                    IcePrevCount = currentIce;

                    iceUsing.AddNext(iceСonsumption);
                    double iceSpeed = iceUsing.Average / fixedDeltaIce.TotalSeconds;


                    string IceUsingString = "Ice left:" + new TimeSpan(10000000 * (long)Math.Truncate(others["OreIce"] / iceSpeed)).ToString(@"dd\.hh\:mm\:ss") + "\n";

                    int finishIndex = iceUsing.Counter + 1;
                    if (finishIndex >= iceUsing.Count)
                    {
                        finishIndex = 0;
                    }

                    double max = iceUsing.Max;
                    if (max == 0)
                    {
                        max = double.MinValue;
                    }
                    string spaceIce = "||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||";

                    //IceUsingString += iceUsing.Counter.ToString() + finishIndex.ToString();
                    for (int i = iceUsing.Counter; i != finishIndex; i--)
                    {
                        if (i < 0)
                        {
                            i = iceUsing.Count - 1;
                        }
                        int lenght = (int)Clamp(iceUsing[i] / max * (spaceIce.Length - 1), 0, spaceIce.Length - 1);
                        IceUsingString += spaceIce.Substring(0, lenght) + "|\n";
                        if (i == finishIndex)
                        {
                            break;
                        }
                    }
                    screens[4].Text = IceUsingString;
                }
                PrevTimeIce = TimeNow;
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

            string strCargo = "Cargo absolute : " + ValueToString(volumeSum * 1000) + "l / " + ValueToString(volumeMax * 1000) + "l";
            string strCargoPersent = "\nCargo percent : " + (volumeSum / volumeMax * 100).ToString("0.0") + "%";

            string ComponentsString = "Components:\n";
            List<KeyValuePair<string, double>> compList = components.OrderBy(x => x.Key).ToList();
            foreach (KeyValuePair<string, double> item in compList)
            {
                string str = item.Key + ":" + CountToString(item.Value, "0.000");
                str += spaceString.Substring(0, oneStringLength - str.Length);
                if (typesToBlueprints.ContainsKey(item.Key))
                {
                    if (production.ContainsKey(typesToBlueprints[item.Key]))
                    {
                        str += "| In production:" + CountToString(production[typesToBlueprints[item.Key]], "0.000");
                    }
                }
                ComponentsString += str + "\n";
            }
            string OresString = "";
            string IngotsString = "";
            foreach (KeyValuePair<string, double> item in others)
            {
                if (item.Key.Contains("Ore"))
                {
                    OresString = item.Key + ":" + CountToString(item.Value) + "\n" + OresString;
                }
                else
                {
                    IngotsString = IngotsString + item.Key + ":" + CountToString(item.Value) + "\n";
                }
            }

            screens[6].Text = OresString;
            screens[7].Text = IngotsString;
            screens[3].Text = strCargo + strCargoPersent;
            screens[5].Text = ComponentsString;
        }

        private void JumpDriveInfo(string arg)
        {
            string percentJumpStr = "";
            foreach (IMyJumpDrive jd in jumpDrives)
            {
                float percentJump = jd.CurrentStoredPower / jd.MaxStoredPower * 100;
                percentJumpStr += "\n" + jd.CustomName + " power : " + percentJump.ToString("0.0") + "%";
                if (percentJump == 100f)
                {
                    percentJumpStr += " (Ready)";
                }

            }
            screens[1].Text = "Jump drive info:\nDistance:" + ValueToString(jumpDrives[0].JumpDistanceMeters) + "m\n" + percentJumpStr;
        }

        private void RefineryInfo()
        {
            Dictionary<string, double> refineItems = new Dictionary<string, double>();
            foreach (IMyRefinery refinery in refineries)
            {
                IMyInventory invent = refinery.InputInventory;
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                invent.GetItems(items);
                foreach (MyInventoryItem item in items)
                {
                    if (!refineItems.ContainsKey(item.Type.SubtypeId))
                    {
                        refineItems.Add(item.Type.SubtypeId, 0);
                    }
                    refineItems[item.Type.SubtypeId] += (double)item.Amount;
                }

                IMyInventory refOutInvent = refinery.OutputInventory;
                List<MyInventoryItem> inventoryItems = new List<MyInventoryItem>();
                refOutInvent.GetItems(inventoryItems);

                foreach (MyInventoryItem item in inventoryItems)
                {
                    IMyInventory inventTo = containersForCraft.FirstOrDefault(x =>
                    {
                        return x.GetInventory().MaxVolume - x.GetInventory().CurrentVolume > item.Amount;
                    })?.GetInventory();
                    refOutInvent.TransferItemTo(inventTo, item);
                }
            }

            string onRefineInfo = "On refine:\n";
            foreach (KeyValuePair<string, double> item in refineItems)
            {
                if (!refinesTotal.ContainsKey(item.Key))
                {
                    refinesTotal.Add(item.Key, new AverageDouble(20));
                }
                if (prevRefine.ContainsKey(item.Key))
                {
                    refinesTotal[item.Key].AddNext((prevRefine[item.Key] - item.Value) / DeltaTime.TotalSeconds);
                }
                TimeSpan timeLeft = new TimeSpan((long)(item.Value / refinesTotal[item.Key].Average * 10000000));
                onRefineInfo += item.Key + ":" + CountToString(item.Value, "0.000") + "    time:" + timeLeft.ToString(@"dd\.hh\:mm\:ss") + "\n";
            }
            prevRefine = refineItems;
            screens[2].Text = onRefineInfo;
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

    }
}

namespace IngameScript
{


    internal partial class Program : MyGridProgram
    {

        private enum TankType
        {
            oxy,
            hydro,
            none
        }

        private List<IMyGasTank> gas;
        private IMyTextPanel screen;
        private bool IsInit = false;
        private int TimerCounter = 0;
        private IMyTimerBlock timerBlock;

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "1")
            {
                IsInit = false;
            }
            if (!IsInit)
            {
                gas = new List<IMyGasTank>();
                timerBlock = (IMyTimerBlock)GridTerminalSystem.GetBlockWithName("timerBlock");
                screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("Display1");
                GridTerminalSystem.GetBlocksOfType<IMyGasTank>(gas);
                gas.RemoveAll(x => GetTankType(x) != TankType.hydro);
                screen.FontSize = 2;
                IsInit = true;
                screen.ContentType = ContentType.TEXT_AND_IMAGE;
            }

            if (TimerCounter >= 15)
            {
                TimerCounter = 0;
                string s = "";
                double sum = 0;
                double max = 0;

                for (int i = 0; i < gas.Count; i++)
                {
                    sum += gas[i].FilledRatio * gas[i].Capacity;
                    max += gas[i].Capacity;
                }
                sum /= 1000000;
                max /= 1000000;
                s = "Total gas ->  " + sum.ToString("0.0") + "M/" + max.ToString("0.0") + "M";

                screen.WriteText(s);
            }
            TimerCounter++;
            timerBlock.ApplyAction("TriggerNow");
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

    }

    internal partial class Program2 : MyGridProgram
    {
        private List<IMyGasTank> gas;
        private IMyTextPanel screen;
        private bool IsInit = false;
        private int TimerCounter = 0;
        private IMyTimerBlock timerBlock;
        private double StartGas = 0;
        private double Cost = 1;

        public void Main(string argument, UpdateType updateSource)
        {

            if (!IsInit)
            {
                timerBlock = (IMyTimerBlock)GridTerminalSystem.GetBlockWithName("timerForGas2");
                screen = (IMyTextPanel)GridTerminalSystem.GetBlockWithName("Display2");
                List<IMyTerminalBlock> tempList = new List<IMyTerminalBlock>();
                GridTerminalSystem.SearchBlocksOfName("TankH", tempList);
                gas = new List<IMyGasTank>();
                screen.FontSize = 20;

                for (int i = 0; i < tempList.Count; i++)
                {
                    gas.Add((IMyGasTank)tempList[i]);
                }
                for (int i = 0; i < gas.Count; i++)
                {
                    StartGas += gas[i].FilledRatio * gas[i].Capacity;
                }
                IsInit = true;
            }

            if (TimerCounter >= 5)
            {
                TimerCounter = 0;
                string s = "";
                double sum = 0;
                double max = 0;

                for (int i = 0; i < gas.Count; i++)
                {
                    sum += gas[i].FilledRatio * gas[i].Capacity;
                    max += gas[i].Capacity;
                }
                double FilledGas = StartGas - sum;
                s = "Total gas : " + sum.ToString("0.0") + "/" + max.ToString("0.0") + "\n"
                    + "Selled gas : " + FilledGas.ToString("0.0") + "\n Cost : " + (FilledGas * Cost).ToString("0") + "$";

                screen.ContentType = ContentType.TEXT_AND_IMAGE;
                screen.WriteText(s);

            }

            if (argument == "1") //start fill
            {
                if (IsInit)
                {
                    for (int i = 0; i < gas.Count; i++)
                    {
                        gas[i].Stockpile = false;
                        gas[i].Enabled = true;
                    }
                }
            }
            if (argument == "2") // stop
            {
                if (IsInit)
                {
                    for (int i = 0; i < gas.Count; i++)
                    {
                        gas[i].Enabled = false;
                    }
                }
            }
            if (argument == "3") // refresh
            {
                if (IsInit)
                {
                    StartGas = 0;
                    for (int i = 0; i < gas.Count; i++)
                    {
                        StartGas += gas[i].FilledRatio * gas[i].Capacity;
                        gas[i].Stockpile = true;
                        gas[i].Enabled = true;
                    }
                }
            }
            TimerCounter++;
            timerBlock.ApplyAction("TriggerNow");
        }


    }
}