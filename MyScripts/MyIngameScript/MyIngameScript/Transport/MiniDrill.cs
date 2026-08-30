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

namespace MiniDrill
{
    internal partial class Program : MyGridProgram
    {
        #region ALL

        #region basics

        private DateTime TimeNow => DateTime.Now;
        private DateTime PrevTime;
        private TimeSpan DeltaTime => TimeNow - PrevTime;
        private float PerSecond => (float)(1.0 / DeltaTime.TotalSeconds);

        private DateTime lastRecompileTime = DateTime.Now;

        private const int SKIP_UPDATE_COUNT = 10;
        private int updateCounter = 0;
        private const string LOAD_STRING = "|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||";
        private int UpdateCounter
        {
            get
            {
                return updateCounter;
            }
            set
            {
                updateCounter = value % SKIP_UPDATE_COUNT;
            }
        }

        private IMyCubeGrid grid;
        private List<SCR> thisScreens;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            REinit();
        }

        private void REinit()
        {
            lastRecompileTime = TimeNow;
            grid = Me.CubeGrid;
            InitScreens();
            AdditionInits();
        }

        private void InitScreens()
        {
            thisScreens = SCR.GetAll(Me, true, 1.6f);
        }

        public void InitBlocks<T>(List<T> outList, string withNaming = "", IMyCubeGrid cubeGrid = null) where T : class, IMyEntity, IMyCubeBlock, IMyTerminalBlock
        {
            if (cubeGrid == null)
            {
                cubeGrid = grid;
            }
            GridTerminalSystem.GetBlocksOfType<T>(outList, x => x.CubeGrid == cubeGrid && !x.CustomName.Contains("scrIgnore"));
            if (!string.IsNullOrEmpty(withNaming))
            {
                for (int i = 0; i < outList.Count; i++)
                {
                    outList[i].CustomName = $"{withNaming}{typeof(T)}_{i}";
                }
            }
        }

        public void InitBlock<T>(out T outBlock) where T : class, IMyEntity, IMyCubeBlock, IMyTerminalBlock
        {
            List<T> temp = new List<T>();
            GridTerminalSystem.GetBlocksOfType<T>(temp, x => x.CubeGrid == grid && !x.CustomName.Contains("scrIgnore"));
            outBlock = temp.FirstOrDefault();
        }

        #endregion

        private const string NAME = "Drill";

        private IMyCockpit cockpit;
        private List<SCR> scr;
        private SCR cargoDrill;
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        private List<IMyShipDrill> drills = new List<IMyShipDrill>();
        private List<IMyMotorStator> hinges = new List<IMyMotorStator>();
        private List<IMyMotorSuspension> wheels = new List<IMyMotorSuspension>();

        private IMyShipConnector connector;

        private const float RAD2DEG = 57.29f;

        private void AdditionInits()
        {
//INIT HERE---------

            InitBlock(out cockpit);
            scr = SCR.GetAll(cockpit, false);
            scr[0].SetAsTXT(4f);
            scr[1].SetAsTXT(2f);
            cargoDrill = new SCR(GridTerminalSystem, "cargoDrill", true, 4);
            InitBlocks(containers);
            InitBlocks(hinges);
            InitBlocks(wheels);
            InitBlock(out connector);
            InitBlocks(drills, "", hinges[0].TopGrid);
            speed = wheels[0].GetValue<float>("Speed Limit");

            targetAngle = hinges[0].Angle * RAD2DEG;
            for (int i = 0; i < hinges.Count; i++)
            {
                hinges[i].LowerLimitDeg = targetAngle - 0.3f;
                hinges[i].UpperLimitDeg = targetAngle + 0.3f;
            }

        }

        public void Main(string argument, UpdateType updateSource)
        {
            scr[1].SetText($"Target:{targetAngle:0.0}\n{hinges[0].Angle * RAD2DEG:0.0}\nspeed:{speed}");

            #region basics

            if (argument == "RE")
            {
                REinit();
            }
            ProcessHinge(argument);
            ProcessWheels(argument);

            DateTime t = TimeNow;
            thisScreens[0].Text = $"{Me.CustomName}\n{NAME}\n working...\n{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}:{t.Millisecond:D3}\n{LOAD_STRING.Substring(0, UpdateCounter)}\nLast update:\n{(DateTime.Now - lastRecompileTime).ToString("hh\\:mm\\:ss")}";

            UpdateCounter++;
            if (updateCounter != 0)
            {
                return;
            }

            #endregion

//CODE HERE----------------
            ProcessContainers();

//CODE END-----------------
            PrevTime = TimeNow;
        }

        private float speed = 0;
        private const float FAST = 80;
        private const float SLOW = 13;

        private void ProcessWheels(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return;
            }

            float speedLimit = 0;
            switch (command)
            {
                case "fast":
                    speedLimit = FAST;
                    speed = FAST;
                    break;
                case "slow":
                    speedLimit = SLOW;
                    speed = SLOW;
                    break;
            }

            if (speedLimit == 0)
            {
                return;
            }

            for (int i = 0; i < wheels.Count; i++)
            {

                wheels[i].SetValue<float>("Speed Limit", speedLimit);
            }
        }

        private float targetAngle = 0;

        private void ProcessHinge(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return;
            }

            switch (command)
            {
                case "up":
                    targetAngle += 5;
                    targetAngle = Math.Max(Math.Min(targetAngle, 50), -15);
                    for (int i = 0; i < hinges.Count; i++)
                    {
                        hinges[i].LowerLimitDeg = targetAngle - 0.3f;
                        hinges[i].UpperLimitDeg = targetAngle + 0.3f;
                    }
                    break;
                case "down":
                    targetAngle -= 5;
                    targetAngle = Math.Max(Math.Min(targetAngle, 50), -15);
                    for (int i = 0; i < hinges.Count; i++)
                    {
                        hinges[i].LowerLimitDeg = targetAngle - 0.3f;
                        hinges[i].UpperLimitDeg = targetAngle + 0.3f;
                    }
                    break;
            }
        }

        private bool emptyed = false;

        private void ProcessContainers()
        {
            float currentVolume = 0;
            float maxVolume = 0;

            for (int i = 0; i < containers.Count; i++)
            {
                IMyInventory inv = containers[i].GetInventory();
                maxVolume += (float)inv.MaxVolume;
                currentVolume += (float)inv.CurrentVolume;
            }

            for (int i = 0; i < drills.Count; i++)
            {
                IMyInventory inv = drills[i].GetInventory();
                maxVolume += (float)inv.MaxVolume;
                currentVolume += (float)inv.CurrentVolume;
            }

            float percent = currentVolume / maxVolume;
            string str = $"{percent * 100:0.0}%\nEmpty:\n{emptyed}";

            scr[0].SetText(str);
            cargoDrill.SetText("\n\n\n" + str);

            if (connector.IsConnected && percent > 0.02f)
            {
                emptyed = true;

                IMyShipConnector other = connector.OtherConnector;
                List<IMyCargoContainer> otherContainers = new List<IMyCargoContainer>();
                InitBlocks(otherContainers, "", other.CubeGrid);

                for (int i = 0; i < containers.Count; i++)
                {
                    IMyInventory checkedInventoryFrom = null;
                    IMyInventory checkedInventoryTo = null;

                    for (int j = 0; j < 3; j++)
                    {
                        if (TryCheckAndGetPath(containers[i].GetInventory(), otherContainers, out checkedInventoryFrom, out checkedInventoryTo))
                        {
                            break;
                        }
                    }
                    if (checkedInventoryFrom != null && checkedInventoryTo != null)
                    {
                        TryWholeInventoryMove(checkedInventoryFrom, checkedInventoryTo);
                    }
                }
            }

            if (!connector.IsConnected && emptyed)
            {
                emptyed = false;
            }
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

            IMyCargoContainer availableContainer = containers.Where(c =>
            {
                IMyInventory inve = c.GetInventory();
                return inve.CanPutItems && (double)inve.CurrentVolume / (double)inve.MaxVolume < 0.5;
            }).FirstOrDefault();

            if (availableContainer == null)
            {
                return false;
            }

            checkedInventoryTo = availableContainer.GetInventory();
            return true;
        }

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

        #endregion

        #endregion
    }
}