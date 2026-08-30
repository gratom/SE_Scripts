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

namespace DrillBatteries
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

        private const int skipUpdateCount = 5;
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
                updateCounter = value % skipUpdateCount;
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

        public void InitBlocks<T>(List<T> outList) where T : class, IMyEntity, IMyCubeBlock, IMyTerminalBlock
        {
            GridTerminalSystem.GetBlocksOfType<T>(outList, x => x.CubeGrid == grid && !x.CustomName.Contains("scrIgnore"));
        }

        public void InitBlock<T>(out T outBlock) where T : class, IMyEntity, IMyCubeBlock, IMyTerminalBlock
        {
            List<T> temp = new List<T>();
            GridTerminalSystem.GetBlocksOfType<T>(temp, x => x.CubeGrid == grid && !x.CustomName.Contains("scrIgnore"));
            outBlock = temp.FirstOrDefault();
        }

        #endregion

        private const string NAME = "Batteries";

        private List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();

        private IMyCockpit cockpit;
        private List<SCR> scr;

        private IMyShipConnector connector;

        private void AdditionInits()
        {
//INIT HERE---------
            InitBlock(out cockpit);
            InitBlock(out connector);
            scr = SCR.GetAll(cockpit, false);
            scr[2].SetAsTXT(2f);
            scr[3].SetAsTXT(2f);
            InitBlocks(batteries);
        }

        private bool connectorState = false;

        public void Main(string argument, UpdateType updateSource)
        {
            if (connector.IsConnected)
            {
                if (!connectorState)
                {
                    BatteriesControl("charge");
                    connectorState = true;
                }
            }
            else
            {
                if (connectorState)
                {
                    BatteriesControl("auto");
                    connectorState = false;
                }
            }

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
            BatteriesInfo();

//CODE END-----------------
            PrevTime = TimeNow;
        }

        private void BatteriesControl(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return;
            }
            if (command == "charge")
            {
                for (int i = 0; i < batteries.Count; i++)
                {
                    if (i != IDofBest)
                    {
                        batteries[i].ChargeMode = ChargeMode.Recharge;
                    }
                }
            }
            else if (command == "auto")
            {
                for (int i = 0; i < batteries.Count; i++)
                {
                    batteries[i].ChargeMode = ChargeMode.Auto;
                }
            }
        }

        private int IDofBest = 0;

        public void BatteriesInfo()
        {
            double Sum = 0;
            double Max = 0;
            double EnPlus = 0;
            double EnMinus = 0;

            float bestStored = 0;

            for (int i = 0; i < batteries.Count; i++)
            {
                if (batteries[i] != null)
                {
                    Max += batteries[i].MaxStoredPower;
                    Sum += batteries[i].CurrentStoredPower;
                    EnPlus += batteries[i].CurrentInput;
                    EnMinus += batteries[i].CurrentOutput;

                    if (batteries[i].CurrentStoredPower > bestStored)
                    {
                        bestStored = batteries[i].CurrentStoredPower;
                        IDofBest = i;
                    }
                }
            }

            //string strVolume = "Energy : " + ValueToString(Sum * 1000000) + "wh / " + ValueToString(Max * 1000000) + "wh";
            string strVolumePersent = "ENERGY:" + (Sum / Max * 100).ToString("0.0") + "%";
            string InOut = "";

            //"in : +" + ValueToString(EnPlus * 1000000) + "w" +
            //               "\nout : -" + ValueToString(EnMinus * 1000000) + "w" +
            //               "\ntotal : " + ValueToString((EnPlus - EnMinus) * 1000000) + "w";
            double time = (Max - Sum) / (EnPlus - EnMinus);

            long timeTicks = (long)(time * 3600 * 10000000);
            TimeSpan timeSpan = new TimeSpan(timeTicks);

            if (Sum / Max * 100 < 99)
            {
                if (time > 0)
                {
                    InOut += $"{timeSpan:dd\\.hh\\:mm\\:ss}";
                }
                else
                {
                    double timeToDiscarge = Sum / (EnPlus - EnMinus);
                    long timeTicksToDiscarge = (long)(timeToDiscarge * 3600 * 10000000);
                    TimeSpan timeSpanToDiscarge = new TimeSpan(timeTicksToDiscarge);
                    InOut += $"{timeSpanToDiscarge:dd\\.hh\\:mm\\:ss}";
                }
            }
            else
            {
                InOut += "full";
            }

            scr[2].SetText($"{strVolumePersent}\n{(time > 0 ? "charging" : "discharging")}\n{InOut}\nact:{batteries.Count}[{IDofBest}]");
            scr[3].SetText($"{(connectorState ? "connected" : "disconnected")}");
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