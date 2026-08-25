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

namespace DoorsCloser
{
    internal partial class Program : MyGridProgram
    {
        #region ALL

        private DateTime TimeNow => DateTime.Now;
        private const float CLOSING_TIME = 1.2f;
        private const int skipUpdateCount = 5;

        private DateTime lastRecompileTime = DateTime.Now;


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            REinit();
        }
        
        private void REinit()
        {
            lastRecompileTime = DateTime.Now;
            InitScreens();
            InitDoors();
        }

        private List<SCR> thisScreens;

        private void InitScreens()
        {
            thisScreens = SCR.GetAll(Me, true, 1.6f);
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
                updateCounter = value % skipUpdateCount;
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument == "RE")
            {
                REinit();
            }

            UpdateCounter++;
            if (updateCounter != 0)
            {
                return;
            }
            CloseDoors();
            DateTime t = TimeNow;
            thisScreens[0].Text = $"{Me.DisplayName} working...\n{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}:{t.Millisecond:D3}\n\n\nLast update:\n{(DateTime.Now - lastRecompileTime).ToString("hh\\:mm\\:ss")}";
        }

        public class DoorExt
        {
            public IMyDoor door;
            public DateTime lastTimeOpen;
            public float closingTime = CLOSING_TIME;

            public DoorExt(IMyDoor door, DateTime lastTimeOpen)
            {
                this.door = door;
                this.lastTimeOpen = lastTimeOpen;
                if (this.door.CustomName.Contains("CT="))
                {
                    int index = this.door.CustomName.IndexOf("CT=") + 3;
                    string sub = this.door.CustomName.Substring(index);
                    float.TryParse(sub, out closingTime);
                }
            }

            public bool isClosed => door.Status == DoorStatus.Closed || door.Status == DoorStatus.Closing;

            public double openedSeconds => (DateTime.Now - lastTimeOpen).TotalSeconds;

            public bool Close()
            {
                if (openedSeconds > closingTime && !isClosed)
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
            IMyCubeGrid grid = Me.CubeGrid;
            GridTerminalSystem.GetBlocksOfType(drs, x => x.CubeGrid == grid && !x.CustomName.Contains("scrIgnore"));
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
        }

        #endregion
    }
}