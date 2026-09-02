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

namespace Stabilizer
{
    internal partial class Program : MyGridProgram
    {
        private static float STRENGHT = 2f;


        public List<SCR> InitAllSCR(IMyGridTerminalSystem gridTerminalSystem = null, bool autoinit = true)
        {
            if (gridTerminalSystem == null)
            {
                gridTerminalSystem = GridTerminalSystem;
            }

            List<SCR> screens = new List<SCR>();
            IMyCubeGrid currentGrid = Me.CubeGrid;
            List<IMyTextPanel> panels = new List<IMyTextPanel>();
            gridTerminalSystem.GetBlocksOfType<IMyTextPanel>(panels, b => b.CubeGrid == currentGrid);
            for (int i = 0; i < panels.Count; i++)
            {
                screens.Add(new SCR(panels[i], panels[i].CustomName));
            }

            List<IMyCockpit> providers = new List<IMyCockpit>();
            gridTerminalSystem.GetBlocksOfType<IMyCockpit>(providers, cockpit => cockpit.CubeGrid == currentGrid);
            for (int i = 0; i < providers.Count; i++)
            {
                for (int j = 0; j < providers[i].SurfaceCount; j++)
                {
                    screens.Add(new SCR(providers[i].GetSurface(j), j));
                }
            }

            if (autoinit)
            {
                foreach (SCR scr in screens)
                {
                    scr.SetAsTXT();
                }
            }

            return screens;
        }


        public class SCR
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


        private List<IMyGyro> gyros = new List<IMyGyro>();
        private List<SCR> screens = new List<SCR>();
        private IMyShipController controller;

        private float sensitivity = 5.0f;

        public Program()
        {
            // Run every tick for smooth stabilization
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            InitGrid();
        }

        public void InitGrid()
        {
            screens = InitAllSCR();
            gyros.Clear();

            // Get ship controller (cockpit, remote control) on the same grid
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(blocks);
            if (blocks.Count > 0)
            {
                controller = blocks[0] as IMyShipController;
            }

            // Get all gyroscopes on the same grid
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyros);

            // Enable override by default
            foreach (IMyGyro gyro in gyros)
            {
                gyro.GyroOverride = true;
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (gyros.Count == 0)
            {
                return;
            }

            SetGyrosOverride(true);

            // Get natural gravity vector
            Vector3D gravity = controller.GetNaturalGravity();
            if (gravity.LengthSquared() == 0)
            {
                // No gravity, stop stabilization
                ApplyGyroValues(Vector3D.Zero);
                return;
            }

            // Current orientation of the controller block
            MatrixD worldMatrix = controller.WorldMatrix;

            // Current orientation and target (upwards)
            Vector3D upVector = worldMatrix.Up;
            Vector3D targetUp = -gravity;
            targetUp.Normalize();

// 1. Find the rotation axis using Cross product (this is fine for direction)
            Vector3D correctionAxis = Vector3D.Cross(upVector, targetUp);
            double axisLength = correctionAxis.Length();

            if (axisLength > 0.0001)
            {
                correctionAxis.Normalize();
            }
            else
            {
                // If axisLength is 0, we are either perfectly aligned (0 deg) or perfectly upside down (180 deg)
                correctionAxis = worldMatrix.Forward; // fallback axis
            }

// 2. Use Dot product to find how "upside down" we are.
// Dot is 1.0 when normal, 0.0 at 90 degrees, -1.0 when completely upside down.
            double dot = Vector3D.Dot(upVector, targetUp);

// 3. Calculate an error magnitude that handles the full 180 degrees correctly.
// When dot = 1 (0 error), error = 0. 
// When dot = -1 (max error at 180 deg), error is max.
            double errorMagnitude = Math.Acos(MathHelper.Clamp(dot, -1.0, 1.0)); // Angle from 0 to Pi (0 to 180 degrees)

// Normalize error to 0.0 - 1.0 range for easier scaling
            double normalizedError = errorMagnitude / Math.PI;

// Apply your non-linear curve here (e.g., squared or cubed for aggressive response on big tilts)
            double shapedIntensity = Math.Pow(normalizedError, 2.0) * STRENGHT;

// Convert correction vector to local space of gyroscopes
            Vector3D localAxis = Vector3D.TransformNormal(correctionAxis, MatrixD.Transpose(worldMatrix));

// Calculate final gyro control values
            Vector3D rotation = localAxis * shapedIntensity * sensitivity;

// Zero out yaw so it doesn't spin around the vertical axis
            rotation.Y = 0;

            ApplyGyroValues(rotation);
        }

        private void SetGyrosOverride(bool enabled)
        {
            foreach (IMyGyro gyro in gyros)
            {
                if (gyro.GyroOverride != enabled)
                {
                    gyro.GyroOverride = enabled;
                }
            }
        }

        private void ApplyGyroValues(Vector3D rotation)
        {
            rotation *= STRENGHT;

            //rotation = new Vector3D(Math.Sign(rotation.X) * Math.Pow(Math.Abs(rotation.X), STRENGHT), rotation.Y, Math.Sign(rotation.Z) * Math.Pow(Math.Abs(rotation.Z), STRENGHT));
            foreach (IMyGyro gyro in gyros)
            {
                gyro.Pitch = (float)rotation.X;
                gyro.Yaw = (float)rotation.Y;
                gyro.Roll = (float)rotation.Z;
            }

            screens[0].Text = $"gyros : {rotation.X:F2} {rotation.Y:F2} {rotation.Z:F2}";

        }
    }
}