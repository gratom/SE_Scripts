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

namespace RefineryManager
{
    internal partial class Program : MyGridProgram
    {
        #region ALL

        private Dictionary<string, int> componentsMinimum = new Dictionary<string, int>()
        {
            { "BulletproofGlass", 500 }, //бронестекло
            { "ComputerComponent", 3000 }, //компьютер
            { "ConstructionComponent", 5000 }, //строительный компонент
            { "DetectorComponent", 100 }, //компоненты детектора руды
            { "Display", 500 }, //экран
            { "ExplosivesComponent", 0 }, //взрывчатка
            { "GirderComponent", 2000 }, //балки
            { "GravityGeneratorComponent", 0 }, //компоненты грави-генератора
            { "InteriorPlate", 5000 }, //внутренняя пластина
            { "LargeTube", 1000 }, //большая труба
            { "MedicalComponent", 0 }, //медицинские компоненты
            { "MetalGrid", 2000 }, //решетка
            { "MotorComponent", 2000 }, //мотор
            { "PowerCell", 2000 }, //батарея
            { "RadioCommunicationComponent", 500 }, //радио-компоненты
            { "ReactorComponent", 0 }, //реакторные компоненты
            { "SmallTube", 5000 }, //малая труба
            { "SolarCell", 2000 }, //солненые ячейки
            { "SteelPlate", 20000 }, //стальная пластина
            { "Superconductor", 0 }, //сверхпроводник
            { "ThrustComponent", 0 } //ионный ускоритель
        };

        private DateTime TimeNow => DateTime.Now;
        private DateTime PrevTime;
        private TimeSpan DeltaTime => TimeNow - PrevTime;
        private float PerSecond => (float)(1.0 / DeltaTime.TotalSeconds);

        private DateTime lastRecompileTime = DateTime.Now;
        private IMyCubeGrid grid;

        private List<IMyRefinery> refineries = new List<IMyRefinery>();
        private List<IMyProductionBlock> assemblers = new List<IMyProductionBlock>();
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();

        private static float ADD_CRAFT_VALUE = 0.02f;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            REinit();
        }

        private List<SCR> thisScreens;
        private const string ORE_SCREEN = "oresSCR";
        private const string INGOT_SCREEN = "ingotsSCR";
        private const string CARGO_SCREEN = "cargoSCR";
        private const string COMPONENTS_SCREEN = "compSCR";

        private static string[] screensNames = new[]
        {
            ORE_SCREEN,
            INGOT_SCREEN,
            CARGO_SCREEN,
            COMPONENTS_SCREEN
        };

        private Dictionary<string, SCR> screens = new Dictionary<string, SCR>()
        {
            { ORE_SCREEN, null },
            { INGOT_SCREEN, null },
            { CARGO_SCREEN, null },
            { COMPONENTS_SCREEN, null }
        };

        private void InitScreens()
        {
            thisScreens = SCR.GetAll(Me, true, 1.6f);

            foreach (string key in screensNames)
            {
                screens[key] = new SCR(GridTerminalSystem, key);
            }
            screens[COMPONENTS_SCREEN]?.SetAsTXT(0.67f);
            screens[ORE_SCREEN]?.SetAsTXT(0.9f);
            screens[INGOT_SCREEN]?.SetAsTXT(0.9f);
        }

        private void REinit()
        {
            lastRecompileTime = TimeNow;
            grid = Me.CubeGrid;
            InitScreens();
            InitBlocks(containers);
            InitBlocks(refineries);
            InitBlocks(assemblers);
        }

        public void InitBlocks<T>(List<T> outList) where T : class, IMyEntity, IMyCubeBlock, IMyTerminalBlock
        {
            GridTerminalSystem.GetBlocksOfType<T>(outList, x => x.CubeGrid == grid && !x.CustomName.Contains("scrIgnore"));
        }

        private Dictionary<string, double> components = new Dictionary<string, double>();
        private Dictionary<string, double> others = new Dictionary<string, double>();
        private Dictionary<string, double> production = new Dictionary<string, double>();
        private Dictionary<string, double> prevCount = new Dictionary<string, double>();

        private const int SKIP_UPDATE_COUNT = 10;
        private const string LOAD_STRING = "|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||";
        private SkipCounter UpdateCounter = new SkipCounter(SKIP_UPDATE_COUNT);

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "RE")
            {
                REinit();
            }

            DateTime t = TimeNow;
            thisScreens[0].Text = $"{Me.DisplayName} working...\n{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}:{t.Millisecond:D3}\n{LOAD_STRING.Substring(0, UpdateCounter.Current)}\nLast update:\n{(DateTime.Now - lastRecompileTime).ToString("hh\\:mm\\:ss")}";

            if (!UpdateCounter.Next())
            {
                return;
            }

            // Очищаем словари вместо создания новых (избегаем GC pressure)
            components.Clear();
            others.Clear();
            production.Clear();

            double volumeSum = 0;
            double volumeMax = 0;

            // --- ЕДИНЫЙ ПРОХОД ПО ИНВЕНТАРЯМ ---
            // Сканируем контейнеры, очистители и ассемблеры за один раз
            // ... наполняем components, others, считаем volumeSum и volumeMax ...
            CollectInventoryData(out volumeSum, out volumeMax);

            // --- ЛОГИКА АВТОКРАФТА И ЛОГИСТИКИ ---
            // Используем уже собранные данные
            ProcessAutoCraft();

            ProcessRefinery();

            // --- РЕНДЕР ЭКРАНОВ ---
            // Проходим по словарям один раз, формируя текст
            UpdateDisplays(volumeSum, volumeMax);

            PrevTime = TimeNow;
        }

        private void ProcessAutoCraft()
        {
            foreach (KeyValuePair<string, int> item in componentsMinimum)
            {
                int prodValue = 0;
                if (production.ContainsKey(item.Key))
                {
                    prodValue = (int)production[item.Key];
                }

                int cargoValue = 0;
                string typeKey = blueprintsToTypes[item.Key];

                if (components.ContainsKey(typeKey))
                {
                    cargoValue = (int)components[typeKey];
                }
                else
                {
                    components.Add(typeKey, 0);
                }

                if (prodValue + cargoValue < item.Value)
                {
                    double craftAmount = Math.Truncate(ADD_CRAFT_VALUE * item.Value);

                    for (int i = 0; i < assemblers.Count; i++)
                    {
                        assemblers[i].AddQueueItem(blueprints[item.Key], craftAmount);
                    }
                }
            }
        }

        private StringBuilder sb = new StringBuilder();

        private const string spaceString = "____________________________________________________________________________";
        private const int oneStringLength = 30;

        private Dictionary<string, AverageDouble> incomeTotal = new Dictionary<string, AverageDouble>();

        private void UpdateDisplays(double volumeSum, double volumeMax)
        {
            // 1. Формируем информацию по объему контейнеров
            string strCargo = $"Cargo absolute : \n{ValueToString(volumeSum * 1000)}l / {ValueToString(volumeMax * 1000)}l";
            string strCargoPersent = $"Cargo percent : \n{volumeSum / volumeMax * 100:0.0}%";

            // 2. Формируем список компонентов
            sb.Clear();
            sb.Append("Components:\n");

            // Сортируем компоненты по имени
            List<KeyValuePair<string, double>> compList = components.OrderBy(x => x.Key).ToList();
            for (int i = 0; i < compList.Count; i++)
            {
                KeyValuePair<string, double> item = compList[i];
                string str = item.Key + ":" + CountToString(item.Value, "0.000");
                str += spaceString.Substring(0, (int)Clamp(oneStringLength - str.Length, 0, oneStringLength - str.Length));

                if (typesToBlueprints.ContainsKey(item.Key))
                {
                    if (production.ContainsKey(typesToBlueprints[item.Key]))
                    {
                        str += $"| In production:{CountToString(production[typesToBlueprints[item.Key]], "0.000")}";
                    }
                }

                sb.Append(str).Append("\n");
            }
            string componentsString = sb.ToString();

            // 3. Формируем руду и слитки
            string oresString = "";
            string ingotsString = "";

            float perSec = PerSecond;

            foreach (KeyValuePair<string, double> item in others)
            {
                if (item.Key.Contains("Ore"))
                {
                    oresString += item.Key.Replace("Ore", "") + ":";

                    double income = 0;
                    if (incomeTotal.ContainsKey(item.Key))
                    {
                        income = incomeTotal[item.Key].Average;
                    }

                    double consumptionPerSec = income * perSec;

                    if (income != 0)
                    {
                        //string speedStr = $"{CountToString(consumptionPerSec)}/sec";

                        if (consumptionPerSec < 0)
                        {
                            double secondsLeft = Math.Abs(item.Value / consumptionPerSec);
                            string timeStr = TimeSpanToString(TimeSpan.FromSeconds(secondsLeft));

                            oresString += $"{CountToString(item.Value, "0.00")} ({timeStr})";
                        }
                        else
                        {
                            oresString += $"{CountToString(item.Value, "0.00")}";
                        }
                    }
                    else
                    {
                        oresString += CountToString(item.Value, "0.00");
                    }
                    oresString += "\n";
                }
                else
                {
                    double income = 0;
                    if (incomeTotal.ContainsKey(item.Key))
                    {
                        income = incomeTotal[item.Key].Average;
                    }

                    ingotsString += item.Key.Replace("Ingot", "") + ":";

                    if (income != 0)
                    {
                        if (income > 0)
                        {
                            ingotsString += $"{CountToString(item.Value)} (+{CountToString(income * perSec)}/sec)";
                        }
                        else
                        {
                            ingotsString += $"{CountToString(item.Value)} ({CountToString(income * perSec)}/sec)";
                        }
                    }
                    else
                    {
                        ingotsString += CountToString(item.Value);
                    }
                    ingotsString += "\n";
                }
            }

            screens[ORE_SCREEN]?.SetText(oresString);
            screens[INGOT_SCREEN]?.SetText(ingotsString);
            screens[CARGO_SCREEN]?.SetText(strCargo);
            screens[COMPONENTS_SCREEN]?.SetText(componentsString);
        }

        // Переиспользуемые списки, чтобы не создавать их заново каждый тик (защита от мусора для GC)
        private List<IMyInventory> tempInventories = new List<IMyInventory>();
        private List<MyInventoryItem> tempItems = new List<MyInventoryItem>();
        private List<MyProductionItem> tempQueue = new List<MyProductionItem>();

        private void CollectInventoryData(out double volumeSum, out double volumeMax)
        {
            volumeSum = 0;
            volumeMax = 0;

            components.Clear();
            others.Clear();
            production.Clear();

            // 1. Собираем все инвентари контейнеров и очистителей
            tempInventories.Clear();

            for (int i = 0; i < containers.Count; i++)
            {
                tempInventories.Add(containers[i].GetInventory());
            }
            for (int i = 0; i < refineries.Count; i++)
            {
                tempInventories.Add(refineries[i].GetInventory());
            }

            // Обрабатываем контейнеры и очистители
            for (int i = 0; i < tempInventories.Count; i++)
            {
                IMyInventory invent = tempInventories[i];
                volumeMax += (double)invent.MaxVolume;
                volumeSum += (double)invent.CurrentVolume;

                tempItems.Clear();
                invent.GetItems(tempItems);

                for (int j = 0; j < tempItems.Count; j++)
                {
                    MyInventoryItem item = tempItems[j];
                    string typeId = item.Type.TypeId;

                    if (typeId.Contains("Component"))
                    {
                        string key = item.Type.SubtypeId;
                        if (!components.ContainsKey(key))
                        {
                            components.Add(key, 0);
                        }
                        components[key] += (double)item.Amount;
                    }
                    else if (typeId.Contains("Ingot") || typeId.Contains("Ore"))
                    {
                        string key = typeId.Substring(16) + item.Type.SubtypeId;
                        if (!others.ContainsKey(key))
                        {
                            others.Add(key, 0);
                        }
                        others[key] += (double)item.Amount;
                    }
                }
            }

            // 2. Считаем income (прирост/убыль) по рудам и слиткам на основе prevCount
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

            // Обновляем prevCount для следующего тика
            prevCount.Clear();
            foreach (KeyValuePair<string, double> i in others)
            {
                prevCount.Add(i.Key, i.Value);
            }

            // 3. Обрабатываем ассемблеры (очередь производства + выходной инвентарь)
            for (int i = 0; i < assemblers.Count; i++)
            {
                IMyProductionBlock asm = assemblers[i];

                // Очередь производства
                tempQueue.Clear();
                asm.GetQueue(tempQueue);
                for (int j = 0; j < tempQueue.Count; j++)
                {
                    MyProductionItem queueItem = tempQueue[j];
                    string key = queueItem.BlueprintId.SubtypeName;
                    if (!production.ContainsKey(key))
                    {
                        production.Add(key, 0);
                    }
                    production[key] += (int)queueItem.Amount;
                }

                // Выходной инвентарь ассемблера
                IMyInventory asmInvent = asm.OutputInventory;
                volumeMax += (double)asmInvent.MaxVolume;
                volumeSum += (double)asmInvent.CurrentVolume;

                tempItems.Clear();
                asmInvent.GetItems(tempItems);
                for (int j = 0; j < tempItems.Count; j++)
                {
                    MyInventoryItem item = tempItems[j];
                    if (item.Type.TypeId.Contains("Component"))
                    {
                        string key = item.Type.SubtypeId;
                        if (!components.ContainsKey(key))
                        {
                            components.Add(key, 0);
                        }
                        components[key] += (double)item.Amount;

                        // Логистика: перемещение готовых компонентов в контейнеры
                        IMyInventory inventTo = containers.FirstOrDefault(x =>
                        {
                            return x.GetInventory().MaxVolume - x.GetInventory().CurrentVolume > item.Amount;
                        })?.GetInventory();

                        if (inventTo != null)
                        {
                            asmInvent.TransferItemTo(inventTo, item);
                        }
                    }
                }
            }
        }

        private SkipCounter counterRefinery = new SkipCounter(20);

        private void ProcessRefinery()
        {
            foreach (IMyRefinery refinery in refineries)
            {
                TryTransferItems(refinery, containers);
            }
            if (counterRefinery.Next())
            {
                BalanceRefineries();
            }
        }

        public class FastOreComparer : IEqualityComparer<MyItemType>
        {
            public bool Equals(MyItemType x, MyItemType y)
            {
                return x.SubtypeId == y.SubtypeId;
            }

            public int GetHashCode(MyItemType obj)
            {
                return obj.SubtypeId.GetHashCode();
            }
        }

        private void BalanceRefineries()
        {
            Dictionary<MyItemType, MyFixedPoint> wholeOres = new Dictionary<MyItemType, MyFixedPoint>();
            List<IMyInventory> refIn = new List<IMyInventory>(refineries.Count);
            List<List<MyInventoryItem>> items = new List<List<MyInventoryItem>>(refineries.Count);

            //get all ores in refineries and cach all
            for (int i = 0; i < refineries.Count; i++)
            {
                items.Add(new List<MyInventoryItem>());
                refIn.Add(refineries[i].InputInventory);
                refIn[i].GetItems(items[i]);
                for (int j = 0; j < items[i].Count; j++)
                {
                    AddOrCreate(wholeOres, items[i][j].Type, items[i][j].Amount);
                }
            }

            foreach (KeyValuePair<MyItemType, MyFixedPoint> ore in wholeOres)
            {
                int targetValue = (int)ore.Value / refineries.Count;

                // if count too small just skip
                if (targetValue < 1000)
                {
                    continue;
                }

                List<int> donorsIndexes = new List<int>();
                List<int> recipientIndexes = new List<int>();

                for (int i = 0; i < refIn.Count; i++)
                {

                    MyFixedPoint val = GetItemAmount(items[i], ore.Key.SubtypeId);
                    if (val > targetValue * 2)
                    {
                        donorsIndexes.Add(i);
                    }
                    else if (val < targetValue / 2)
                    {
                        recipientIndexes.Add(i);
                    }
                }

                for (int i = donorsIndexes.Count - 1; i >= 0; i--)
                {
                    IMyInventory donor = refIn[donorsIndexes[i]];
                    for (int j = recipientIndexes.Count - 1; j >= 0; j--)
                    {
                        IMyInventory recipient = refIn[recipientIndexes[j]];

                        MyFixedPoint count = (MyFixedPoint)(targetValue * 0.9f);
                        MyInventoryItem donorItem = items[donorsIndexes[i]].First(x => x.Type.SubtypeId == ore.Key.SubtypeId);

                        TryMoveItem(donor, recipient, donorItem, count);
                        recipientIndexes.RemoveAt(j);
                        if (donor.GetItemAmount(ore.Key) < targetValue * 2)
                        {
                            donorsIndexes.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private void BalanceRefineries2()
        {
            Dictionary<MyItemType, MyFixedPoint> wholeOres = new Dictionary<MyItemType, MyFixedPoint>();
            List<IMyInventory> refIn = new List<IMyInventory>(refineries.Count);

            for (int i = 0; i < refineries.Count; i++)
            {
                IMyInventory inv = refineries[i].InputInventory;
                refIn.Add(inv);

                List<MyInventoryItem> items = new List<MyInventoryItem>();
                inv.GetItems(items);
                for (int j = 0; j < items.Count; j++)
                {
                    AddOrCreate(wholeOres, items[j].Type, items[j].Amount);
                }
            }

            foreach (KeyValuePair<MyItemType, MyFixedPoint> ore in wholeOres)
            {
                int targetValue = (int)ore.Value / refineries.Count;
                if (targetValue < 1000)
                {
                    continue;
                }

                List<int> donors = new List<int>();
                List<int> recipients = new List<int>();

                for (int i = 0; i < refIn.Count; i++)
                {
                    MyFixedPoint val = refIn[i].GetItemAmount(ore.Key);
                    if (val > targetValue * 2)
                    {
                        donors.Add(i);
                    }
                    else if (val < targetValue / 2)
                    {
                        recipients.Add(i);
                    }
                }

                int donorIndex = donors.Count - 1;
                int recipientIndex = recipients.Count - 1;

                while (donorIndex >= 0 && recipientIndex >= 0)
                {
                    IMyInventory donor = refIn[donors[donorIndex]];
                    IMyInventory recipient = refIn[recipients[recipientIndex]];

                    List<MyInventoryItem> donorItems = new List<MyInventoryItem>();
                    donor.GetItems(donorItems);

                    MyInventoryItem? itemToMove = null;
                    for (int k = 0; k < donorItems.Count; k++)
                    {
                        if (donorItems[k].Type == ore.Key)
                        {
                            itemToMove = donorItems[k];
                            break;
                        }
                    }

                    if (itemToMove == null)
                    {
                        donorIndex--;
                        continue;
                    }

                    MyFixedPoint count = (MyFixedPoint)(targetValue * 0.9f);

                    if (donor.CanTransferItemTo(recipient, ore.Key))
                    {
                        donor.TransferItemTo(recipient, itemToMove.Value, count);
                    }

                    if (donor.GetItemAmount(ore.Key) < targetValue * 2)
                    {
                        donorIndex--;
                    }

                    if (recipient.GetItemAmount(ore.Key) > targetValue / 2)
                    {
                        recipientIndex--;
                    }
                }
            }
        }

        private bool TryMoveItem(IMyInventory donor, IMyInventory recipient, MyInventoryItem item, MyFixedPoint count)
        {
            if (donor.CanTransferItemTo(recipient, item.Type))
            {
                donor.TransferItemTo(recipient, item, count);
                return true;
            }
            return false;
        }

        private MyFixedPoint GetItemAmount(List<MyInventoryItem> item, string keySubtypeId)
        {
            for (int i = 0; i < item.Count; i++)
            {
                if (item[i].Type.SubtypeId == keySubtypeId)
                {
                    return item[i].Amount;
                }
            }
            return 0;
        }

        private static readonly Random random = new Random();

        public bool TryTransferItems(IMyRefinery refinery, List<IMyCargoContainer> containers)
        {
            IMyInventory checkedInventoryFrom;
            IMyInventory checkedInventoryTo;
            if (!TryCheckAndGetPath(refinery?.OutputInventory, containers, out checkedInventoryFrom, out checkedInventoryTo))
            {
                return false;
            }

            TryWholeInventoryMove(checkedInventoryFrom, checkedInventoryTo);

            return true;
        }

        private static void TryWholeInventoryMove(IMyInventory checkedInventoryFrom, IMyInventory checkedInventoryTo)
        {
            for (int i = checkedInventoryFrom.ItemCount - 1; i >= 0; i--)
            {
                MyInventoryItem? item = checkedInventoryFrom.GetItemAt(i);
                if (item.HasValue)
                {
                    MyInventoryItem valueItem = item.Value;
                    if (checkedInventoryFrom.CanTransferItemTo(checkedInventoryTo, item.Value.Type))
                    {
                        checkedInventoryFrom.TransferItemTo(checkedInventoryTo, valueItem, item.Value.Amount);
                    }
                }
            }
        }

        private static bool TryCheckAndGetPath(IMyInventory inventoryFrom, List<IMyCargoContainer> containers, out IMyInventory checkedInventoryFrom, out IMyInventory checkedInventoryTo)
        {
            checkedInventoryFrom = null;
            checkedInventoryTo = null;
            if (inventoryFrom == null || containers == null || containers.Count == 0)
            {
                return false;
            }

            checkedInventoryFrom = inventoryFrom;

            if (checkedInventoryFrom.VolumeFillFactor <= 0f)
            {
                return false;
            }

            IMyCargoContainer availableContainer = containers.FirstOrDefault(c => !c.GetInventory().IsFull);

            if (availableContainer == null)
            {
                return false;
            }

            checkedInventoryTo = availableContainer.GetInventory();
            return true;
        }

        #region addition dictionaries

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

        #region SCR

        public class SCR
        {
            public readonly string name;

            public IMyTextPanel screen;
            public IMyTextSurface surface;
            private bool isInitWithPanel;

            public SCR(IMyGridTerminalSystem grid, string name, bool initAsTxt = true, float fontSize = 1f)
            {
                this.name = name;
                screen = (IMyTextPanel)grid.GetBlockWithName(name);
                isInitWithPanel = true;
                if (initAsTxt)
                {
                    SetAsTXT(fontSize);
                }
            }

            public SCR(IMyTextPanel textPanel, string name, bool initAsTxt = true, float fontSize = 1f)
            {
                this.name = name;
                screen = textPanel;
                isInitWithPanel = true;
                if (initAsTxt)
                {
                    SetAsTXT(fontSize);
                }
            }

            public SCR(IMyCockpit cockpit, int index, bool initAsTxt = true, float fontSize = 1f)
            {
                name = index.ToString();
                surface = cockpit.GetSurface(index);
                isInitWithPanel = false;
                if (initAsTxt)
                {
                    SetAsTXT(fontSize);
                }
            }

            public SCR(IMyTextSurface surface, int index, bool initAsTxt = true, float fontSize = 1f)
            {
                name = index.ToString();
                this.surface = surface;
                isInitWithPanel = false;
                if (initAsTxt)
                {
                    SetAsTXT(fontSize);
                }
            }

            public static List<SCR> GetAll(IMyProgrammableBlock block, bool initAsTxt = true, float fontSize = 1f)
            {
                List<SCR> screens = new List<SCR>();
                for (int i = 0; i < block.SurfaceCount; i++)
                {
                    screens.Add(new SCR(block.GetSurface(i), i, initAsTxt, fontSize));
                }
                return screens;
            }

            public static List<SCR> GetAll(IMyCockpit cockpit, bool initAsTxt = true, float fontSize = 1f)
            {
                List<SCR> screens = new List<SCR>();
                for (int i = 0; i < cockpit.SurfaceCount; i++)
                {
                    screens.Add(new SCR(cockpit, i, initAsTxt, fontSize));
                }
                return screens;
            }

            public void SetAsTXT(float fontSize = 1.0f)
            {
                IMyTextSurface targetSurface = isInitWithPanel ? (IMyTextSurface)screen : surface;
                if (targetSurface == null)
                {
                    return;
                }

                targetSurface.ContentType = ContentType.TEXT_AND_IMAGE;
                targetSurface.Font = "Monospace";
                targetSurface.FontSize = fontSize;
                targetSurface.FontColor = new Color(0, 255, 100);
                targetSurface.BackgroundColor = new Color(32, 32, 32);
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

            public void SetText(string value)
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

        #endregion

        #region toStr extensions

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

        private string TimeSpanToString(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
            {
                return $"{timeSpan.Days}d {timeSpan.Hours}h";
            }
            if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            }
            if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            return $"{timeSpan.Seconds}s";
        }

        private double Clamp(double x, double min, double max)
        {
            return x < min ? min : x > max ? max : x;
        }

        #endregion

        #region average

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

            protected AbstractAverage(int count = 15)
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

        #endregion

        #region counter

        public class SkipCounter
        {
            private readonly int _targetCount;
            private int _currentCount;

            public event Action<SkipCounter> Triggered;

            public bool Is => _currentCount == _targetCount;

            public int Current => _currentCount;

            public int Target => _targetCount;

            public SkipCounter(int targetCount = 10)
            {
                if (targetCount <= 0)
                {
                    targetCount = 10;
                }

                _targetCount = targetCount;
                _currentCount = 0;
            }

            public bool Next()
            {
                _currentCount++;

                if (_currentCount >= _targetCount)
                {
                    _currentCount = 0;
                    Triggered?.Invoke(this);
                    return true;
                }

                return false;
            }

            public void Reset()
            {
                _currentCount = 0;
            }

            public static implicit operator bool(SkipCounter counter)
            {
                return counter.Is;
            }
        }

        #endregion

        #region dictionary extension

        public static void AddOrCreate<T1>(Dictionary<T1, MyFixedPoint> dictionary, T1 key, MyFixedPoint value)
        {
            if (dictionary.ContainsKey(key))
            {
                dictionary[key] += value;
            }
            else
            {
                dictionary.Add(key, value);
            }
        }

        #endregion

        #endregion
    }
}