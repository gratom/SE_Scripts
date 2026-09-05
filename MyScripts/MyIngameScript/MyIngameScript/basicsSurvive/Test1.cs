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

namespace Test1
{
    internal partial class Program : MyGridProgram
    {
                /*
        * R e a d m e
        * -----------
        * By Patrick Hansen  - https://www.youtube.com/patrickhansen101
        * 
        * Script for controlling up to 6 pistons/rotors/hinges or groups thereof, using normal control inputs while in a cockpit
        * 
        * The comments should explain what to do, the script will throw errors on the PB if you've done something wrong!
        */

         //*** If you want to reverse individual pistons'/rotors' movement, add "rev" to their custom data ***

        double AD_Movespeed = 10.0;             //A & D speed multiplier, negative values will invert input
        double WS_Movespeed = -2.0;             //W & S speed multiplier, negative values will invert input
        double QE_Movespeed = 0.3;              //Q & E speed multiplier, negative values will invert input
        double SpaceC_Movespeed = 10.0;      //Space & C speed multiplier, negative values will invert input
        double MouseY_Movespeed = 0.1;       //MouseX speed multiplier, negative values will invert input
        double MouseX_Movespeed = -0.1;      //MouseX speed multiplier, negative values will invert input

        string Controller_name = "Cockpit"; //Cockpit/remote name, if the name is invalid/empty then the first controller found will be used.

        //Block/group names (eg. "Piston 2"), leave empty (eg. "", NO SPACES) to disable that output
        string AD_Name = "";                         //Name of A & D Block / Block group
        string WS_Name = "";                        //Name of W & S Block / Block group
        string QE_Name = "Mast";                        //Name of Q & E Block / Block group
        string SpaceC_Name = "";                //Name of Space & E Block / Block group
        string MouseX_Name = "";                //Name of MouseY/UpDown Arrow Block / Block group
        string MouseY_Name = "";                //Name of MouseX/LeftRight Arrow Block / Block group    




//DON'T Change anything below this line :P

        bool initialzied = false;

        bool Use_AD = false;                //Are we mapping a piston to A&D? (true/false)
        bool Use_WS = false;                //Are we mapping a piston to W&S? (true/false)
        bool Use_QE = false;                //Are we mapping a piston to Q&E? (true/false)
        bool Use_SpaceC = false;            //Are we mapping a piston to Space&C? (true/false)
        bool Use_MouseY = false;            //Are we mapping a piston to MouseY/UpDown Arrow (true/false)
        bool Use_MouseX = false;            //Are we mapping a piston to MouseX/LeftRight Arrow? (true/false)

        bool ADPist = false;
        bool WSPist = false;
        bool QEPist = false;
        bool SCPist = false;
        bool MYPist = false;
        bool MXPist = false;

        IMyShipController controller;
        List<IMyShipController> controllers = new List<IMyShipController>();
        List<IMyPistonBase> ADPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> WSPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> QEPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> SpaceCPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> MouseYPistons = new List<IMyPistonBase>();
        List<IMyPistonBase> MouseXPistons = new List<IMyPistonBase>();

        List<IMyMotorStator> ADRotors = new List<IMyMotorStator>();
        List<IMyMotorStator> WSRotors = new List<IMyMotorStator>();
        List<IMyMotorStator> QERotors = new List<IMyMotorStator>();
        List<IMyMotorStator> SpaceCRotors = new List<IMyMotorStator>();
        List<IMyMotorStator> MouseYRotors = new List<IMyMotorStator>();
        List<IMyMotorStator> MouseXRotors = new List<IMyMotorStator>();


        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            if (AD_Name.Length > 0) { Use_AD = true; }
            if (WS_Name.Length > 0) { Use_WS = true; }
            if (QE_Name.Length > 0) { Use_QE = true; }
            if (SpaceC_Name.Length > 0) { Use_SpaceC = true; }
            if (MouseY_Name.Length > 0) { Use_MouseY = true; }
            if (MouseX_Name.Length > 0) { Use_MouseX = true; }

            if (GridTerminalSystem.GetBlockWithName(Controller_name) as IMyShipController != null)
            {
                controller = GridTerminalSystem.GetBlockWithName(Controller_name) as IMyShipController;
            }
            else
            {
                GridTerminalSystem.GetBlocksOfType(controllers);
                if (controllers.Count > 0) { controller = controllers[0]; } else { throw new System.InvalidOperationException("No controllers found!\nAdd a cockpit or remote"); }
            }

            if (Use_AD) { if (IsPiston(AD_Name)) { GetPistons(AD_Name, ADPistons); ADPist = true; } else { GetRotors(AD_Name, ADRotors); } }
            if (Use_WS) { if (IsPiston(WS_Name)) { GetPistons(WS_Name, WSPistons); WSPist = true; } else { GetRotors(WS_Name, WSRotors); } }
            if (Use_QE) { if (IsPiston(QE_Name)) { GetPistons(QE_Name, QEPistons); QEPist = true; } else { GetRotors(QE_Name, QERotors); } }
            if (Use_SpaceC) { if (IsPiston(SpaceC_Name)) { GetPistons(SpaceC_Name, SpaceCPistons); SCPist = true; } else { GetRotors(SpaceC_Name, SpaceCRotors); } }
            if (Use_MouseY) { if (IsPiston(MouseY_Name)) { GetPistons(MouseY_Name, MouseYPistons); MYPist = true; } else { GetRotors(MouseY_Name, MouseYRotors); } }
            if (Use_MouseX) { if (IsPiston(MouseX_Name)) { GetPistons(MouseX_Name, MouseXPistons); MXPist = true; } else { GetRotors(MouseX_Name, MouseXRotors); } }
        }


        public void Main(string argument, UpdateType updateSource)
        {
            if (Use_AD)
            {
                if (ADPist) { for (int i = 0; i < ADPistons.Count; i++) { ADPistons[i].Velocity = controller.MoveIndicator.X * (ADPistons[i].CustomData.ToLower().Contains("rev") ? -(float)AD_Movespeed : (float)AD_Movespeed); } }
                else { for (int i = 0; i < ADRotors.Count; i++) { ADRotors[i].TargetVelocityRPM = controller.MoveIndicator.X * (ADRotors[i].CustomData.ToLower().Contains("rev") ? -(float)AD_Movespeed : (float)AD_Movespeed); } }
            }
            if (Use_WS)
            {
                if (WSPist) { for (int i = 0; i < WSPistons.Count; i++) { WSPistons[i].Velocity = controller.MoveIndicator.Z * (WSPistons[i].CustomData.ToLower().Contains("rev") ? -(float)WS_Movespeed : (float)WS_Movespeed); } }
                else { for (int i = 0; i < WSRotors.Count; i++) { WSRotors[i].TargetVelocityRPM = controller.MoveIndicator.Z * (WSRotors[i].CustomData.ToLower().Contains("rev") ? -(float)WS_Movespeed : (float)WS_Movespeed); } }
            }
            if (Use_QE)
            {
                if (QEPist) { for (int i = 0; i < QEPistons.Count; i++) { QEPistons[i].Velocity = controller.RollIndicator * (QEPistons[i].CustomData.ToLower().Contains("rev") ? -(float)QE_Movespeed : (float)QE_Movespeed); } }
                else { for (int i = 0; i < QERotors.Count; i++) { QERotors[i].TargetVelocityRPM = controller.RollIndicator * (QERotors[i].CustomData.ToLower().Contains("rev") ? -(float)QE_Movespeed : (float)QE_Movespeed); } }
            }
            if (Use_SpaceC)
            {
                if (SCPist) { for (int i = 0; i < SpaceCPistons.Count; i++) { SpaceCPistons[i].Velocity = controller.MoveIndicator.Y * (SpaceCPistons[i].CustomData.ToLower().Contains("rev") ? -(float)SpaceC_Movespeed : (float)SpaceC_Movespeed); } }
                else { for (int i = 0; i < SpaceCRotors.Count; i++) { SpaceCRotors[i].TargetVelocityRPM = controller.MoveIndicator.Y * (SpaceCRotors[i].CustomData.ToLower().Contains("rev") ? -(float)SpaceC_Movespeed : (float)SpaceC_Movespeed); } }
            }
            if (Use_MouseY)
            {
                if (MYPist) { for (int i = 0; i < MouseYPistons.Count; i++) { MouseYPistons[i].Velocity = controller.RotationIndicator.Y * (MouseYPistons[i].CustomData.ToLower().Contains("rev") ? -(float)MouseY_Movespeed : (float)MouseY_Movespeed); } }
                else { for (int i = 0; i < MouseYRotors.Count; i++) { MouseYRotors[i].TargetVelocityRPM = controller.RotationIndicator.Y * (MouseYRotors[i].CustomData.ToLower().Contains("rev") ? -(float)MouseY_Movespeed : (float)MouseY_Movespeed); } }
            }
            if (Use_MouseX)
            {
                if (MXPist) { for (int i = 0; i < MouseXPistons.Count; i++) { MouseXPistons[i].Velocity = controller.RotationIndicator.X * (MouseXPistons[i].CustomData.ToLower().Contains("rev") ? -(float)MouseX_Movespeed : (float)MouseX_Movespeed); } }
                else { for (int i = 0; i < MouseXRotors.Count; i++) { MouseXRotors[i].TargetVelocityRPM = controller.RotationIndicator.X * (MouseXRotors[i].CustomData.ToLower().Contains("rev") ? -(float)MouseX_Movespeed : (float)MouseX_Movespeed); } }
            }
            
        }

        public bool IsPiston(string name)
        {
            if (GridTerminalSystem.GetBlockWithName(name)!= null)
            {
                return GridTerminalSystem.GetBlockWithName(name).GetType().ToString().Contains("Piston");
            }
            else
            {
                if (GridTerminalSystem.GetBlockGroupWithName(name) != null)
                {
                    IMyBlockGroup tempGroup = GridTerminalSystem.GetBlockGroupWithName(name);
                    List<IMyTerminalBlock> tempList = new List<IMyTerminalBlock>();
                    tempGroup.GetBlocks(tempList);
                    if (tempList.Count > 0) { return tempList[0].GetType().ToString().Contains("Piston"); } else { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
                }
                else { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
            }
        }

        public void GetPistons(string name, List<IMyPistonBase> PistList)
        {
            if (GridTerminalSystem.GetBlockWithName(name) as IMyPistonBase != null)
            {
                PistList.Add(GridTerminalSystem.GetBlockWithName(name) as IMyPistonBase);
            }
            else
            {
                if(GridTerminalSystem.GetBlockGroupWithName(name) != null)
                {
                    IMyBlockGroup tempGroup = GridTerminalSystem.GetBlockGroupWithName(name);
                    List<IMyTerminalBlock> tempList = new List<IMyTerminalBlock>();
                    tempGroup.GetBlocks(tempList);
                    for (int i = 0; i < tempList.Count; i++)
                    {
                        PistList.Add(tempList[i] as IMyPistonBase);
                    }
                    if (PistList.Count == 0) { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
                }
                else
                {
                    throw new System.InvalidOperationException(name + " not found, correct or clear name");
                }
            }
        }

        public void GetRotors(string name, List<IMyMotorStator> RotList)
        {
            if (GridTerminalSystem.GetBlockWithName(name) as IMyMotorStator != null)
            {
                RotList.Add(GridTerminalSystem.GetBlockWithName(name) as IMyMotorStator);
            }
            else
            {
                if (GridTerminalSystem.GetBlockGroupWithName(name) != null)
                {
                    IMyBlockGroup tempGroup = GridTerminalSystem.GetBlockGroupWithName(name);
                    List<IMyTerminalBlock> tempList = new List<IMyTerminalBlock>();
                    tempGroup.GetBlocks(tempList);
                    for (int i = 0; i < tempList.Count; i++)
                    {
                        RotList.Add(tempList[i] as IMyMotorStator);
                    }
                    if (RotList.Count == 0) { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
                }
                else { throw new System.InvalidOperationException(name + " not found, correct or clear name"); }
            }
        }

    }
}