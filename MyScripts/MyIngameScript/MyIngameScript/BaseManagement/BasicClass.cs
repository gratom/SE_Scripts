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

        private const int SKIP_UPDATE_COUNT = 10;
        private const string LOAD_STRING = "|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||";
        private SkipCounter UpdateCounter = new SkipCounter(SKIP_UPDATE_COUNT);

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

        //private ActionUpdater updater = new ActionUpdater();
        private const string NAME = "SCRIPT_NAME";

        private void AdditionInits()
        {
//INIT HERE---------

        }

        public void Main(string argument, UpdateType updateSource)
        {
            RealTime();
            #region basics

            if (argument == "RE")
            {
                REinit();
            }

            DateTime t = TimeNow;
            thisScreens[0].Text = $"{Me.CustomName}-{NAME} working...\n{t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}:{t.Millisecond:D3}\n{LOAD_STRING.Substring(0, UpdateCounter.Current)}\nLast update:\n{(DateTime.Now - lastRecompileTime).ToString("hh\\:mm\\:ss")}";

            if (!UpdateCounter.Next())
            {
                return;
            }

            #endregion
            Routine();
            //updater.Update((float)DeltaTime.TotalSeconds);
            PrevTime = TimeNow;
        }

        private void Routine()
        {

        }

        private void RealTime()
        {

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

        #region wheels

        public class Wheel
        {
            public Wheel(IMyMotorSuspension suspension)
            {
                motor = suspension;
            }

            public IMyMotorSuspension motor;

            private const string SPEED_LIMIT_STRING = "Speed Limit";
            public float SpeedLimit
            {
                get
                {
                    return motor.GetValue<float>(SPEED_LIMIT_STRING);
                }
                set
                {
                    motor.SetValue<float>(SPEED_LIMIT_STRING, value);
                }
            }

            public static List<Wheel> FromSus(List<IMyMotorSuspension> suspList)
            {
                List<Wheel> ret = new List<Wheel>(suspList.Count);
                for (int i = 0; i < suspList.Count; i++)
                {
                    ret.Add(new Wheel(suspList[i]));
                }
                return ret;
            }

        }

        #endregion

        #region Piston

        public class Piston
        {
            public const float TOP_PISTON_EXT = 1.41f;
            public IMyPistonBase baza;
            public IMyAttachableTopBlock top;

            public Piston(IMyPistonBase piston)
            {
                baza = piston;
                top = baza.Top;
                target = baza.CurrentPosition;
                pos = target; //update piston data
            }

            public float pos
            {
                get
                {
                    return target;
                }
                set
                {
                    target = value;
                    baza.MaxLimit = target + 0.01f;
                    baza.MinLimit = target - 0.01f;
                    if (baza.CurrentPosition > baza.MaxLimit)
                    {
                        baza.Velocity = -1f;
                    }
                    else
                    {
                        baza.Velocity = 1f;
                    }
                }
            }
            private float target;

            public Vector3D realPos => top.GetPosition();
            public Vector3D mathRealPos
            {
                get
                {
                    MatrixD baseMatrix = baza.WorldMatrix;
                    double currentExtension = baza.CurrentPosition + TOP_PISTON_EXT;
                    Vector3D worldOffset = Vector3D.TransformNormal(new Vector3D(0, currentExtension, 0), baseMatrix);
                    return baseMatrix.Translation + worldOffset;
                }
            }

            public Vector3D minPosition => baza.WorldMatrix.Translation + baza.WorldMatrix.Up * 1.41;
            public Vector3D maxPosition => baza.WorldMatrix.Translation + baza.WorldMatrix.Up * 11.41;
            private Vector3D pistonAxis => baza.WorldMatrix.Up;

            public void TrySetAsCloseAsPossibleTo(Vector3D targetPos)
            {
                Vector3D toTarget = targetPos - minPosition;
                double projectedDistance = Vector3D.Dot(toTarget, pistonAxis);
                double clampedExtension = Clamp(projectedDistance - 1.41, 0, 10);
                pos = (float)clampedExtension;
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

        #region math

        public static float Clamp(float val, float min, float max)
        {
            return Math.Min(Math.Max(val, min), max);
        }

        public static double Clamp(double val, double min, double max)
        {
            return Math.Min(Math.Max(val, min), max);
        }

        #endregion

        #region sequentions

        public class ActionUpdater : IUpdater
        {
            public void Update(float deltaTime)
            {
                UpdateAction?.Invoke(deltaTime);
            }

            public event Action<float> UpdateAction;
        }

        public interface IUpdater
        {
            event Action<float> UpdateAction;
        }

        public class ActionSequence
        {
            private List<Action> actions = new List<Action>();
            private List<float> delays = new List<float>();
            private int currentIndex = 0;
            private float currentTimer = 0f;
            private bool isRunning = false;
            private bool isPaused = false;
            private IUpdater updater;

            public bool IsPlaying => isRunning;

            public ActionSequence(IUpdater updater, params object[] sequenceSteps)
            {
                this.updater = updater;
                updater.UpdateAction += Update;
                ParseSteps(sequenceSteps);
            }

            public void Play()
            {
                if (isRunning && !isPaused)
                {
                    return;
                }

                if (isPaused)
                {
                    isPaused = false;
                    return;
                }

                currentIndex = 0;
                currentTimer = 0f;
                isRunning = true;
                isPaused = false;

                ExecuteCurrentStep();
            }

            public void TryPlay()
            {
                if (!isRunning)
                {
                    Play();
                }
            }

            public void Pause()
            {
                if (isRunning)
                {
                    isPaused = true;
                }
            }

            public void Stop()
            {
                isRunning = false;
                isPaused = false;
                currentIndex = 0;
                currentTimer = 0f;
            }

            public void Restart()
            {
                Stop();
                Play();
            }

            public void Update(float deltaTime)
            {
                if (!isRunning || isPaused)
                {
                    return;
                }

                if (currentIndex >= actions.Count)
                {
                    isRunning = false;
                    return;
                }

                if (delays[currentIndex] > 0f)
                {
                    currentTimer += deltaTime;
                    if (currentTimer < delays[currentIndex])
                    {
                        return;
                    }
                    currentTimer = 0f;
                }

                currentIndex++;
                ExecuteCurrentStep();
            }

            private void ExecuteCurrentStep()
            {
                while (currentIndex < actions.Count)
                {
                    actions[currentIndex]?.Invoke();

                    if (delays[currentIndex] > 0f)
                    {
                        currentTimer = 0f;
                        break;
                    }

                    currentIndex++;
                }

                if (currentIndex >= actions.Count)
                {
                    isRunning = false;
                }
            }

            private void ParseSteps(object[] steps)
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    object step = steps[i];

                    if (step is Action)
                    {
                        actions.Add((Action)step);
                        delays.Add(0f);
                    }
                    else if (step is float)
                    {
                        actions.Add(null);
                        delays.Add((float)step);
                    }
                    else if (step is int)
                    {
                        actions.Add(null);
                        delays.Add((int)step);
                    }
                }
            }
        }

        #endregion

        #endregion
    }
}