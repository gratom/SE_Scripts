using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
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

namespace MegaDrillScript
{

    partial class Program : MyGridProgram
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
                this.name = index.ToString();
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
        Dictionary<string, double> prevRefine = new Dictionary<string, double>();

        private float addCraftValue = 0.02f;

        #region addition dictionaries
        private Dictionary<string, int> componentsMinimum = new Dictionary<string, int>()
        {
            { "BulletproofGlass", 0},                     //бронестекло
            { "ComputerComponent", 0},                   //компьютер
            { "ConstructionComponent", 0},               //строительный компонент
            { "DetectorComponent", 0},                     //компоненты детектора руды
            { "Display", 0},                               //экран
            { "ExplosivesComponent", 0},                   //взрывчатка
            { "GirderComponent", 0},                      //балки
            { "GravityGeneratorComponent", 0},             //компоненты грави-генератора
            { "InteriorPlate", 0},                       //внутренняя пластина
            { "LargeTube", 0},                           //большая труба
            { "MedicalComponent", 0},                      //медицинские компоненты
            { "MetalGrid", 0},                            //решетка
            { "MotorComponent", 0},                       //мотор
            { "PowerCell", 0},                           //батарея
            { "RadioCommunicationComponent", 0},           //радио-компоненты
            { "ReactorComponent", 0},                      //реакторные компоненты
            { "SmallTube", 0},                            //малая труба
            { "SolarCell", 0},                            //солненые ячейки
            { "SteelPlate", 0},                          //стальная пластина
            { "Superconductor", 0},                       //сверхпроводник
            { "ThrustComponent", 0},                      //ионный ускоритель
        };

        private Dictionary<string, MyDefinitionId> blueprints = new Dictionary<string, MyDefinitionId>()
        {
            { "BulletproofGlass", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/BulletproofGlass")},
            { "ComputerComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ComputerComponent")},
            { "ConstructionComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ConstructionComponent")},
            { "DetectorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/DetectorComponent")},
            { "Display", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/Display")},
            { "ExplosivesComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ExplosivesComponent")},
            { "GirderComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/GirderComponent")},
            { "GravityGeneratorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/GravityGeneratorComponent")},
            { "InteriorPlate", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/InteriorPlate")},
            { "LargeTube", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/LargeTube")},
            { "MedicalComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MedicalComponent")},
            { "MetalGrid", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MetalGrid")},
            { "MotorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/MotorComponent")},
            { "PowerCell", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/PowerCell")},
            { "RadioCommunicationComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/RadioCommunicationComponent")},
            { "ReactorComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ReactorComponent")},
            { "SmallTube", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SmallTube")},
            { "SolarCell", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SolarCell")},
            { "SteelPlate", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SteelPlate")},
            { "Superconductor", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/Superconductor")},
            { "ThrustComponent", MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/ThrustComponent")},
        };

        private Dictionary<string, string> blueprintsToTypes = new Dictionary<string, string>()
        {
            { "BulletproofGlass",          "BulletproofGlass"},
            { "ComputerComponent",         "Computer"},
            { "ConstructionComponent",     "Construction"},
            { "DetectorComponent",         "Detector"},
            { "Display",                   "Display"},
            { "ExplosivesComponent",       "Explosives"},
            { "GirderComponent",           "Girder"},
            { "GravityGeneratorComponent", "GravityGenerator"},
            { "InteriorPlate",             "InteriorPlate"},
            { "LargeTube",                 "LargeTube"},
            { "MedicalComponent",          "Medical"},
            { "MetalGrid",                 "MetalGrid"},
            { "MotorComponent",            "Motor"},
            { "PowerCell",                 "PowerCell"},
            { "RadioCommunicationComponent", "RadioCommunication"},
            { "ReactorComponent",          "Reactor"},
            { "SmallTube",                 "SmallTube"},
            { "SolarCell",                 "SolarCell"},
            { "SteelPlate",                "SteelPlate"},
            { "Superconductor",            "Superconductor"},
            { "ThrustComponent",           "Thrust"},
        };



        private Dictionary<string, string> typesToBlueprints = new Dictionary<string, string>()
        {
            {"BulletproofGlass"    ,"BulletproofGlass"              },
            {"Computer"            ,"ComputerComponent"              },
            {"Construction"        ,"ConstructionComponent"        },
            {"Detector"            ,"DetectorComponent"            },
            {"Display"             ,"Display"                      },
            {"Explosives"          ,"ExplosivesComponent"          },
            {"Girder"              ,"GirderComponent"              },
            {"GravityGenerator"    ,"GravityGeneratorComponent"        },
            {"InteriorPlate"       ,"InteriorPlate"                    },
            {"LargeTube"           ,"LargeTube"                         },
            {"Medical"             ,"MedicalComponent"                 },
            {"MetalGrid"           ,"MetalGrid"                        },
            {"Motor"               ,"MotorComponent"                     },
            {"PowerCell"           ,"PowerCell"                          },
            {"RadioCommunication"  ,"RadioCommunicationComponent"    },
            {"Reactor"             ,"ReactorComponent"                },
            {"SmallTube"           ,"SmallTube"                       },
            {"SolarCell"           ,"SolarCell"                       },
            {"SteelPlate"          ,"SteelPlate"                       },
            {"Superconductor"      ,"Superconductor"                      },
            {"Thrust"              ,"ThrustComponent"              },
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
            string strVolumePersent = "Energy percent : " + ((Sum / Max) * 100).ToString("0.0") + "%";
            string InOut = "in : +" + CountToString(EnPlus * 1000000) + "wh" +
                           "\nout : -" + CountToString(EnMinus * 1000000) + "wh" +
                           "\ntotal : " + CountToString(((EnPlus - EnMinus) * 1000000)) + "wh";
            double time = (Max - Sum) / (EnPlus - EnMinus);

            long timeTicks = (long)(time * 3600 * 10000000);
            TimeSpan timeSpan = new TimeSpan(timeTicks);

            if ((Sum / Max) * 100 < 99)
            {
                if (time > 0)
                {
                    InOut += "\ntime to charge : " + timeSpan.ToString(@"dd\.hh\:mm\:ss");
                }
                else
                {
                    double timeToDiscarge = (Sum) / (EnPlus - EnMinus);
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
                volumeMax += ((double)invent.MaxVolume);
                volumeSum += ((double)invent.CurrentVolume);
            }

            screens[1].Text = "1";

            Dictionary<string, double> components = new Dictionary<string, double>();
            Dictionary<string, double> others = new Dictionary<string, double>();

            for (int i = 0; i < containers.Count; i++)
            {
                IMyInventory invent = containers[i].GetInventory();
                volumeMax += ((double)invent.MaxVolume);
                volumeSum += ((double)invent.CurrentVolume);

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
                        components[key] += ((double)inventoryItem[j].Amount);
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
                            others[key] += ((double)inventoryItem[j].Amount);
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
                    production[key] += ((int)item.Amount);
                }

                IMyInventory asmInvent = asm.OutputInventory;
                List<MyInventoryItem> inventoryItems = new List<MyInventoryItem>();
                asmInvent.GetItems(inventoryItems);

                foreach (var item in inventoryItems)
                {
                    if (item.Type.TypeId.Contains("Component"))
                    {
                        string key = item.Type.SubtypeId;
                        if (!components.ContainsKey(key))
                        {
                            components.Add(key, 0);
                        }
                        components[key] += ((double)item.Amount);

                        IMyInventory inventTo = containersForCraft.FirstOrDefault(x =>
                        {
                            return (x.GetInventory().MaxVolume - x.GetInventory().CurrentVolume) > item.Amount;
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

            double totalKG = others.Sum(x => x.Value);
            double totalStone = others.FirstOrDefault(x => x.Key == "OreStone").Value;
            string cargoType = "ores:" + ValueToString(totalKG - totalStone) + "T\nstone:" + ValueToString(totalStone) + " ("+ ((totalStone / totalKG) * 100).ToString("0.0") + "%)";

            string strCargo = "Cargo absolute : " + ValueToString(volumeSum * 1000) + "l / " + ValueToString(volumeMax * 1000) + "l";
            string strCargoPersent = "\nCargo percent : " + ((volumeSum / volumeMax) * 100).ToString("0.0") + "%";


            screens[1].Text = "5";

            screens[3].Text = /*strCargo +*/ strCargoPersent  + "\n"+ cargoType;
            screens[6].Text = ((volumeSum / volumeMax) * 100).ToString("0.0") + "%";
        }

        enum TankType
        {
            oxy,
            hydro,
            none
        }
        TankType GetTankType(IMyTerminalBlock theBlock)
        {
            if (theBlock is IMyGasTank)
            {
                if (theBlock.BlockDefinition.SubtypeId.Contains("Hydro"))
                    return TankType.hydro;
                else
                    return TankType.oxy;
            }
            return TankType.none;
        }

        string ValueToString(double count)
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

        string CountToString(double count, string roundto = "0.0")
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
            return (x < min) ? min : ((x > max) ? max : x);
        }
    }
}