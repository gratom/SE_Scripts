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

namespace WellDrill
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

        public void InitBlock<T>(out T outBlock, string name = "", bool nameOverride = false) where T : class, IMyEntity, IMyCubeBlock, IMyTerminalBlock
        {
            List<T> temp = new List<T>();
            GridTerminalSystem.GetBlocksOfType<T>(temp, x => x.CubeGrid == grid && !x.CustomName.Contains("scrIgnore"));
            if (name == "")
            {
                outBlock = temp.FirstOrDefault();
            }
            else
            {
                outBlock = temp.FirstOrDefault(x => x.CustomName == name);
                if (outBlock == null)
                {
                    outBlock = temp.FirstOrDefault();
                }
            }

            if (outBlock != null && nameOverride)
            {
                outBlock.CustomName = name;
            }
        }

        #endregion

        private const string NAME = "WDrill";
        private const string PISTON_UP_NAME = "Well_Drill_Piston_Up";
        private const string PISTON_DOWN_NAME = "Well_Drill_Piston_Down";

        private IMyCockpit cockpit;
        private List<SCR> scr;
        private SCR cargoDrill;
        private List<IMyCargoContainer> containers = new List<IMyCargoContainer>();
        private List<IMyShipDrill> drills = new List<IMyShipDrill>();
        private List<IMyMotorSuspension> wheels = new List<IMyMotorSuspension>();


        private List<IMyPistonBase> pistonsUP = new List<IMyPistonBase>();
        private List<IMyPistonBase> pistonsDOWN = new List<IMyPistonBase>();

        private IMyShipConnector connector;

        private const float RAD2DEG = 57.29f;

        private void AdditionInits()
        {
//INIT HERE---------

            InitBlock(out cockpit, "WDrillCockpit");
            scr = SCR.GetAll(cockpit, false);
            scr[0].SetAsTXT(2f);
            scr[1].SetAsTXT(1.5f);
            cargoDrill = new SCR(GridTerminalSystem, "cargoWDrill", true, 4);
            InitBlocks(containers);
            InitBlocks(wheels);
            InitBlock(out connector);
            speed = wheels[0].GetValue<float>("Speed Limit");

            GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistonsUP, x => x.CustomName == PISTON_UP_NAME);
            GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistonsDOWN, x => x.CustomName == PISTON_DOWN_NAME);
        }

        public void Main(string argument, UpdateType updateSource)
        {

            ProceedPistons(argument);
            ProcessWheels(argument);
            ProcessCommands(argument);

            scr[1].SetText($"Well:{target:0.00}\nspeed:{speed}({cockpit.GetShipVelocities().LinearVelocity.Length():0.0})");

            #region basics

            if (argument == "RE")
            {
                REinit();
            }

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

        private const string CHANNEL_TAG = "BASE_CHANNEL";

        private void ProcessCommands(string argument)
        {
            if (argument == "conn")
            {
                Vector3D pos = connector.GetPosition();
                string msg = $"UP_PARKING|{0}|{pos.X:0.00}|{pos.Y:0.00}|{pos.Z:0.00}";
                IGC.SendBroadcastMessage(CHANNEL_TAG, msg, TransmissionDistance.AntennaRelay);
            }
        }

        private const float PISTON_VAL_CHANGE = 0.1f;
        private const float PISTON_VAL_SPEED = 0.25f;
        private float target = 0;
        private float UpTarget => 10 - target;
        private float DownTarget => target;

        private void ProceedPistons(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return;
            }

            float pistonSpeed = 0;

            switch (argument)
            {
                case "up":
                    target = Math.Min(10, Math.Max(0, target - PISTON_VAL_CHANGE));
                    pistonSpeed = PISTON_VAL_SPEED;
                    break;
                case "down":
                    target = Math.Min(10, Math.Max(0, target + PISTON_VAL_CHANGE));
                    pistonSpeed = -PISTON_VAL_SPEED;
                    break;
            }

            if (pistonSpeed == 0)
            {
                return;
            }

            for (int i = 0; i < pistonsUP.Count; i++)
            {
                pistonsUP[i].MaxLimit = UpTarget + PISTON_VAL_CHANGE;
                pistonsUP[i].MinLimit = UpTarget - PISTON_VAL_CHANGE;
                pistonsUP[i].Velocity = pistonSpeed;
            }
            for (int i = 0; i < pistonsDOWN.Count; i++)
            {
                pistonsDOWN[i].MaxLimit = DownTarget + PISTON_VAL_CHANGE;
                pistonsDOWN[i].MinLimit = DownTarget - PISTON_VAL_CHANGE;
                pistonsDOWN[i].Velocity = -pistonSpeed;
            }
        }

        private float speed = 0;
        private const float FAST = 120;
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

            if ((connector?.IsConnected ?? false) && percent > 0.02f)
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

            if ((!connector?.IsConnected ?? false) && emptyed)
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