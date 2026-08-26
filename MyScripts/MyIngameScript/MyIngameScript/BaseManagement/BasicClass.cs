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

namespace BasicClass
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

        private const int SKIP_UPDATE_COUNT = 30;
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

        private void AdditionInits()
        {
//INIT HERE---------

        }

        public void Main(string argument, UpdateType updateSource)
        {
            #region basics

            if (argument == "RE")
            {
                REinit();
            }

            DateTime t = TimeNow;
            thisScreens[0].Text = $"{Me.DisplayName} working...\n{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}:{t.Millisecond:D3}\n{LOAD_STRING.Substring(0, updateCounter)}\nLast update:\n{(DateTime.Now - lastRecompileTime).ToString("hh\\:mm\\:ss")}";

            UpdateCounter++;
            if (updateCounter != 0)
            {
                return;
            }

            #endregion

//CODE HERE----------------


//CODE END-----------------
            PrevTime = TimeNow;
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